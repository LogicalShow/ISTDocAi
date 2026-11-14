using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Lookup.Queries.LookupGetList;

public sealed record LookupGetListQuery() : IQuery<IEnumerable<LocalizedLookupItem>>;
