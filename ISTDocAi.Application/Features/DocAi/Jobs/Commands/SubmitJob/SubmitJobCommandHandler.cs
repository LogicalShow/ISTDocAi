using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Commands.SubmitJob;
public sealed class SubmitJobCommandHandler(ISTDocAi.Application.Services.DocAi.Intake.SubmitJob service) : ICommandHandler<SubmitJobCommand, SubmitJobCommandResponse>
{
    public async Task<Result<SubmitJobCommandResponse>> Handle(SubmitJobCommand request, CancellationToken cancellationToken)
    {
        var submission = await service.ExecuteAsync(request.Consumer, request.File, cancellationToken);
        var b = submission.Body;
        return Result.Success(new SubmitJobCommandResponse(b.JobId, b.RunNo, b.RereadSkipped, b.RunReason, b.Changes, b.Status, b.StatusUrl, b.PageCount) { Accepted = submission.Accepted });
    }
}
