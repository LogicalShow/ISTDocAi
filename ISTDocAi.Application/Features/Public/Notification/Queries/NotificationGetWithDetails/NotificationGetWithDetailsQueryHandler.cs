using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails.Contracts;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails;

public class NotificationGetWithDetailsQueryHandler(
    IRepository<Pub_Notification> repository,
    IUserQueryService userQueryService) : IQueryHandler<NotificationGetWithDetailsQuery, NotificationGetWithDetailsQueryResponse>
{
    public async Task<Result<NotificationGetWithDetailsQueryResponse>> Handle(NotificationGetWithDetailsQuery request, CancellationToken cancellationToken)
    {


        var notification = await repository.GetAsync(
            request.NotificationId,
            [entity => entity.NotificationUsers],
            cancellationToken);

        if (notification == null) {
            return Result.Failure<NotificationGetWithDetailsQueryResponse>(Error.NotFound(ErrorCodes.NotificationNotFound, "Notification not found."));
        }
        // Un-assigned rows are kept only to preserve their notification history; they must
        // render on the unselected side so the user can be re-assigned.
        var assignedNotificationUsers = notification.NotificationUsers?
            .Where(x => x.IsAssigned)
            .ToList() ?? [];

        var selectedUserIds = assignedNotificationUsers.Select(x => x.UserId).ToList();

        var allUsers = await userQueryService.GetAllUsersAsync(cancellationToken);

        var selectedUserLookups = allUsers.Where(x => selectedUserIds.Contains(x.Id)).ToList();

        var unselectedUsers = allUsers.Where(x => !selectedUserIds.Contains(x.Id)).Select(x => new UserLookup
       ( x.Id,
           x.UserName
     
        )).ToList();

        var selectedUsers = assignedNotificationUsers.Select(x => new User
        {
            Id = x.Id,
            RowVersion = x.RowVersion,
            UserId = x.UserId,
            UserName = selectedUserLookups.FirstOrDefault(u => u.Id == x.UserId)?.UserName ?? string.Empty,
            SendingWayId = (int)x.SendingWay,
            SendingWayName = x.SendingWay.ToString()

        }


            ).ToList();

        var response = new NotificationGetWithDetailsQueryResponse()
        {
            Id = notification.Id,
            Name = notification.Name,
            SelectedUsers = selectedUsers,
            UnSelectedUsers = unselectedUsers

        };

        return Result.Success(response);
    }
}
