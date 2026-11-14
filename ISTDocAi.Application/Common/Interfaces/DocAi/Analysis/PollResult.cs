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

public abstract record PollResult
{
    public sealed record Running(TimeSpan RetryAfter) : PollResult;
    public sealed record Succeeded(string RawJson, DateTimeOffset CompletedAtUtc, int Pages) : PollResult;
    public sealed record Failed(string Code, string? Message) : PollResult;
    public sealed record NotFound : PollResult;
    public sealed record HttpError(int Status, TimeSpan? RetryAfter) : PollResult;
    public sealed record NoAnswer : PollResult;
}
