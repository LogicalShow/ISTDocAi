using MapsterMapper;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Services;
using ISTDocAi.Domain.Common.Shared;
using ISTDocAi.Domain.Entities.Public;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentAdd;

public class AttachmentAddCommandHandler(
    IRepository<Pub_Attachment> repository,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IActionLogService actionLogService
    ) : ICommandHandler<AttachmentAddCommand, AttachmentAddCommandResponse>
{
    public async Task<Result<AttachmentAddCommandResponse>> Handle(AttachmentAddCommand request, CancellationToken cancellationToken)
    {
        var entity = mapper.Map<Pub_Attachment>(request);
        entity.SetEntityId();

        await repository.AddAsync(entity, cancellationToken);
        await actionLogService.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_AttachmentAdd, entity.Id, null, entity,cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AttachmentAddCommandResponse(entity.Id));
    }
}
