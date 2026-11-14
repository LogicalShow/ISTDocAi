using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Services;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentDelete;

public class AttachmentDeleteCommandHandler(
    IRepositoryOp<Pub_Attachment> opRepoOp,
    IRepository<Pub_Attachment> opRepo,
    IUnitOfWork unitOfWork,
    IActionLogService actionLog) : ICommandHandler<AttachmentDeleteCommand, AttachmentDeleteCommandResponse>
{
    public async Task<Result<AttachmentDeleteCommandResponse>> Handle(AttachmentDeleteCommand request, CancellationToken cancellationToken)
    {
        var requested = request.Items
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().RowVersion);

        var attachments = await opRepoOp.GetListAsync(
            x => requested.Keys.Contains(x.Id),
            orderBy: null, pageNo: 1, pageRowCount: int.MaxValue,
            cancellationToken: cancellationToken);

        if (attachments.Items.Any())
        {
            await opRepo.BulkDeleteAsync(
                attachments.Items.Select(x => (x.Id, requested[x.Id])),
                cancellationToken);
        }

        foreach (var entity in attachments.Items)
            await actionLog.LogAction(Pub_ActionTypeEnum.Pub_AttachmentDelete, entity.Id, null, entity, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttachmentDeleteCommandResponse
        {
            Message = "Deleted successfully.",
            DeletedIds = attachments.Items.Select(x => x.Id).ToList(),
            Blocked = []
        };
    }
}
