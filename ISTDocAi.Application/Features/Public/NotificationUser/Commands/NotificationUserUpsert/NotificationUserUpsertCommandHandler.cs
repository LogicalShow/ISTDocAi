using MapsterMapper;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Services;
using ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert.Contracts;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert;

public class NotificationUserUpsertCommandHandler(
    IRepository<Pub_Notification> notificationRepository,
    IRepository<Pub_NotificationUser> repository,
    IRepository<Pub_NotificationUserDetail> detailRepository,
    IMapper mapper,
    IUnitOfWork unitOfWork,
    IActionLogService actionLogService,
    INotificationService notificationService) : ICommandHandler<NotificationUserUpsertCommand, NotificationUserUpsertCommandResponse>
{
    public async Task<Result<NotificationUserUpsertCommandResponse>> Handle(NotificationUserUpsertCommand request, CancellationToken cancellationToken)
    {
        string message = string.Empty;

       var notificationId = string.IsNullOrEmpty(request.ExistedNotificationId)
           ? request.NewNotificationId
           : request.ExistedNotificationId;
       var notification = await notificationRepository.GetAsync(
           notificationId,
           [entity => entity.NotificationUsers],
           cancellationToken);

        if (notification == null)
        {
            return Result<NotificationUserUpsertCommandResponse>.Failure(Error.NotFound(ErrorCodes.NotificationUserUpsertFailed, $"Notification with ID {request.ExistedNotificationId} not found."));
        }

        var existingUserIds = notification.NotificationUsers.Select(nu => nu.Id).ToList();

        // Rows already dealt with by the add/update passes, so the un-assign pass below
        // cannot pick the same row up again.
        var handledRowIds = new HashSet<string>();

        // Every user whose subscriptions changed - added, re-assigned, updated, un-assigned or
        // deleted. All of them need their client to re-subscribe, not just the selected ones.
        var affectedUserIds = new List<string>();

        var deletedIds = new List<string>();

        var entitiesToAdd = request.SelectedNotificationUsers
            .Where(snu => !existingUserIds.Contains(snu.Id) && string.IsNullOrEmpty(snu.Id))
            .ToList();

        if (entitiesToAdd.Any())
        {
            // A previously un-assigned user comes back from the assignment editor with no Id,
            // because the editor lists them as unselected even though their row still exists to
            // hold their notification history. Match on (NotificationId, UserId) so a re-assign
            // flips IsAssigned back on instead of inserting a row the unique index would reject.
            var incomingUserIds = entitiesToAdd.Select(snu => snu.UserId).ToList();
            var existingRowsByUserId = (await repository.QueryAsync(
                    query => query.Where(x => x.NotificationId == request.NewNotificationId
                                              && incomingUserIds.Contains(x.UserId)),
                    cancellationToken: cancellationToken))
                .GroupBy(x => x.UserId)
                .ToDictionary(group => group.Key, group => group.First());

            var AddedNotificationUsers = mapper.Map<List<AddedNotificationUser>>(entitiesToAdd);
            foreach (var AddedNotificationUser in AddedNotificationUsers)
            {
                if (existingRowsByUserId.TryGetValue(AddedNotificationUser.UserId, out var revivedNotificationUser))
                {
                    revivedNotificationUser.IsAssigned = true;
                    revivedNotificationUser.SendingWay = (SendingWay)AddedNotificationUser.SendingWayId;

                    await repository.UpdateAsync(revivedNotificationUser, cancellationToken);
                    await actionLogService.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_NotificationUserUpdate, revivedNotificationUser.Id, null, revivedNotificationUser, cancellationToken);

                    handledRowIds.Add(revivedNotificationUser.Id);
                    affectedUserIds.Add(revivedNotificationUser.UserId);
                    message += $"Re-assigned NotificationUser for UserId {revivedNotificationUser.UserId}. ";
                    continue;
                }

                var entity = mapper.Map<Pub_NotificationUser>(AddedNotificationUser);
                entity.NotificationId = request.NewNotificationId;
                entity.IsAssigned = true;
                entity.SetEntityId();

                await repository.AddAsync(entity, cancellationToken);
                await actionLogService.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_NotificationUserAdd, entity.Id, null, entity, cancellationToken);

                handledRowIds.Add(entity.Id);
                affectedUserIds.Add(entity.UserId);
                message += $"Added NotificationUser for UserId {entity.UserId}. ";
            }
        }

        var entitiesToUpdate = request.SelectedNotificationUsers
            .Where(snu => existingUserIds.Contains(snu.Id) && !string.IsNullOrEmpty(snu.Id))
            .ToList();

        if (entitiesToUpdate.Any())
        {
            var UpdatedNotificationUsers = mapper.Map<List<UpdatedNotificationUser>>(entitiesToUpdate);
            foreach (var UpdatedNotificationUser in UpdatedNotificationUsers)
            {
                var existingNotificationUser = await repository.GetAsync(UpdatedNotificationUser.Id, null, cancellationToken);

                if (existingNotificationUser !=null)
                {
                    mapper.Map(UpdatedNotificationUser, existingNotificationUser);
                    existingNotificationUser.NotificationId = request.NewNotificationId;
                    existingNotificationUser.IsAssigned = true;

                    await repository.UpdateAsync(existingNotificationUser, cancellationToken);
                    await actionLogService.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_NotificationUserUpdate, existingNotificationUser.Id, null, existingNotificationUser, cancellationToken);

                    handledRowIds.Add(existingNotificationUser.Id);
                    affectedUserIds.Add(existingNotificationUser.UserId);
                    message += $"Updated NotificationUser for UserId {existingNotificationUser.UserId}. ";
                }
            }
        }

        // Rows that are currently assigned but absent from the submitted selection. Rows already
        // carrying IsAssigned == false are history-only and must be left alone.
        var entitiesToRemove = notification.NotificationUsers
            .Where(nu => nu.IsAssigned
                         && !handledRowIds.Contains(nu.Id)
                         && !request.SelectedNotificationUsers.Any(snu => snu.Id == nu.Id))
            .ToList();

        if (entitiesToRemove.Any())
        {
            var ids = entitiesToRemove.Select(x => x.Id).Distinct().ToList();
            var referencedDetails = await detailRepository.QueryAsync(
                query => query.Where(x => ids.Contains(x.NotificationUserId)),
                cancellationToken: cancellationToken);

            var referenceCounts = referencedDetails
                .GroupBy(x => x.NotificationUserId)
                .ToDictionary(x => x.Key, x => x.Count());

            // Nothing references the row, so there is no history to preserve - take it out of the table.
            var deletableItems = entitiesToRemove
                .Where(x => !referenceCounts.ContainsKey(x.Id))
                .ToList();

            // The row owns Pub_NotificationUserDetail history. Deleting it would take the past
            // notifications with it, so keep the row and only switch the subscription off.
            var unassignableIds = entitiesToRemove
                .Where(x => referenceCounts.ContainsKey(x.Id))
                .Select(x => x.Id)
                .ToList();

            if (deletableItems.Count > 0)
            {
                await repository.BulkDeleteAsync(
                    deletableItems.Select(x => (x.Id, x.RowVersion)),
                    cancellationToken);
            }

            foreach (var item in deletableItems)
            {
                await actionLogService.LogAction(
                    Pub_ActionTypeEnum.Pub_NotificationUserDelete,
                    item.Id,
                    null,
                    item,
                    cancellationToken);

                affectedUserIds.Add(item.UserId);
            }

            deletedIds.AddRange(deletableItems.Select(x => x.Id));

            if (unassignableIds.Count > 0)
            {
                // Re-fetch rather than reusing the instances hanging off `notification`: those have
                // their Notification navigation fixed up, and UpdateAsync would walk the graph and
                // mark the parent Pub_Notification modified too.
                var itemsToUnassign = await repository.QueryAsync(
                    query => query.Where(x => unassignableIds.Contains(x.Id)),
                    cancellationToken: cancellationToken);

                foreach (var item in itemsToUnassign)
                {
                    item.IsAssigned = false;

                    await repository.UpdateAsync(item, cancellationToken);
                    await actionLogService.LogAction(
                        Pub_ActionTypeEnum.Pub_NotificationUserUpdate,
                        item.Id,
                        null,
                        item,
                        cancellationToken);

                    affectedUserIds.Add(item.UserId);
                }
            }

            if (deletableItems.Count > 0)
                message += "Deleted successfully. ";

            if (unassignableIds.Count > 0)
                message += "Un-assigned users that already have notification history; their old notifications are still available to them. ";
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var userId in affectedUserIds.Where(x => !string.IsNullOrEmpty(x)).Distinct())
        {
            await notificationService.NotifySubscriptionsChangedAsync(userId, cancellationToken);
        }

        return Result.Success(new NotificationUserUpsertCommandResponse(message.Trim(), deletedIds, []));
    }
}
