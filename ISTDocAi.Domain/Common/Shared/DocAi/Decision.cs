using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi;
/// <summary>A business outcome. A rejection never contains a replacement state.</summary>
public sealed record Decision<T>
{
    private Decision(T? value, Rejection? rejection, string? detail) => (Value, Rejection, Detail) = (value, rejection, detail);
    public T? Value { get; }
    public Rejection? Rejection { get; }
    public string? Detail { get; }
    public bool IsSuccess => Rejection is null;

    public static Decision<T> Accept(T value) => new(value, null, null);
    public static Decision<T> Reject(Rejection reason, string detail) => new(default, reason, detail);
}
