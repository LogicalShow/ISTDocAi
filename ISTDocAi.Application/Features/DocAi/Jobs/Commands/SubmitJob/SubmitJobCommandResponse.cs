using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Commands.SubmitJob;
public sealed record SubmitJobCommandResponse(Guid JobId, int RunNo, string? RereadSkipped, string RunReason, RunChanges? Changes, string Status, string StatusUrl, int PageCount)
{
    [System.Text.Json.Serialization.JsonIgnore] public bool Accepted { get; init; }
}
