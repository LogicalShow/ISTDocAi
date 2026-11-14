using MapsterMapper;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Services;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Entities.Public;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentUpdate;

public class AttachmentUpdateCommandHandler(
    IRepository<Pub_Attachment> repository,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IActionLogService actionLogService
    ) : ICommandHandler<AttachmentUpdateCommand, AttachmentUpdateCommandResponse>
{
    public async Task<Result<AttachmentUpdateCommandResponse>> Handle(AttachmentUpdateCommand request, CancellationToken cancellationToken)
    {
      var entity = await repository.GetAsync(request.Id, cancellationToken:cancellationToken);
        if (entity == null)
        {
            return Result<AttachmentUpdateCommandResponse>.Failure(Error.NotFound(ErrorCodes.AttachmentNotFound, $"Attachment with Id {request.Id} was not found."));
        }

        mapper.Map(request, entity);
        await repository.UpdateAsync(entity, cancellationToken:cancellationToken);
        await actionLogService.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_AttachmentUpdate,entity.Id,null,entity,cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AttachmentUpdateCommandResponse("AttachmentUpdatedSuccessfully"));
    }
}
