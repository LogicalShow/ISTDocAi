using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
public sealed record GetJobQuery(Consumer Consumer, Guid JobId, short? RunNo) : IQuery<GetJobResponse>;
