using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Application.Features.Public.Lookup;

namespace ISTDocAi.Application.Features.Public.Lookup.Queries.LookupGetList;

public sealed class LookupGetListQueryHandler(ICultureContext culture)
    : IQueryHandler<LookupGetListQuery, IEnumerable<LocalizedLookupItem>>
{
    public Task<Result<IEnumerable<LocalizedLookupItem>>> Handle(
        LookupGetListQuery request,
        CancellationToken cancellationToken)
    {
        var items = StaticLookupData.Items
            .Select(item => item.ToLocalized(culture))
            .ToList();

        return Task.FromResult(Result.Success<IEnumerable<LocalizedLookupItem>>(items));
    }
}
