using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetTopUpCycles;
public sealed record GetTopUpCyclesQuery(short ConsumerId, long? GrantId) : IQuery<GetTopUpCyclesResponse>;
