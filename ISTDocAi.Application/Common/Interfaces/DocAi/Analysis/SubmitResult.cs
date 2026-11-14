using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;

public abstract record SubmitResult
{
    public sealed record Accepted(string OperationLocation, TimeSpan? RetryAfter) : SubmitResult;
    public sealed record Rejected(int Status, string? Code, string? Message, TimeSpan? RetryAfter) : SubmitResult;
    public sealed record NoAnswer : SubmitResult;
}
