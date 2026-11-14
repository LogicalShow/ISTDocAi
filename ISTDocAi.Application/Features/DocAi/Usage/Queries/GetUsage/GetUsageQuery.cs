using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Usage.Queries.GetUsage;
public sealed record GetUsageQuery(short ConsumerId, int Months) : IQuery<GetUsageResponse>;
