using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Domain.Common.Shared.DocAi;
public sealed record ManifestPosition(int Page, double Top) : IComparable<ManifestPosition>
{
    public int CompareTo(ManifestPosition? other) => other is null ? 1 : Page != other.Page ? Page.CompareTo(other.Page) : Top.CompareTo(other.Top);
}
