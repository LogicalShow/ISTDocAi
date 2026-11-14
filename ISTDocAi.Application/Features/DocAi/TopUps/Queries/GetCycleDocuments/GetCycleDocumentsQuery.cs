using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetCycleDocuments;
public sealed record GetCycleDocumentsQuery(short ConsumerId, long GrantId, int Limit, long? AfterChargeId) : IQuery<GetCycleDocumentsResponse>;
