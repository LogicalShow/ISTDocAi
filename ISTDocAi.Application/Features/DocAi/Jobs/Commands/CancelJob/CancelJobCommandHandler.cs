using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Commands.CancelJob;
public sealed class CancelJobCommandHandler(IJobRepository jobs) : ICommandHandler<CancelJobCommand, CancelJobResult>
{
    public async Task<Result<CancelJobResult>> Handle(CancelJobCommand request, CancellationToken cancellationToken)
    {
        var id = request.RequestId.Trim();
        if (id.Length is 0 or > 100 || id.Any(char.IsControl)) throw IntakeException.Invalid();
        var result = await jobs.CancelAsync(request.Consumer.ConsumerId, id, cancellationToken)
            ?? throw new IntakeException(404, "JOB_NOT_FOUND", "Job not found", "No such job or run exists for this consumer.");
        return Result.Success(result);
    }
}
