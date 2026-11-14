using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi;
public sealed record DomainError
{
    public DomainError(string code, string? message) => (Code, Message) = (code, message is { Length: > 400 } ? message[..400] : message);
    public string Code { get; }
    public string? Message { get; }
}
