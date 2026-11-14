using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Application.Common.Services;

public interface IRequestLogService
{
    Task Log(
        string requestType,
        string requestParameters,
        DateTime requestInitTime,
        DateTime requestCompleteTime,
        string? responseError,
        CancellationToken cancellationToken);
}

public class RequestLogService(
    IRepository<Pub_RequestLog> repository,
    IUnitOfWork unitOfWork) : IRequestLogService
{
    public async Task Log(
        string requestType,
        string requestParameters,
        DateTime requestInitTime,
        DateTime requestCompleteTime,
        string? responseError,
        CancellationToken cancellationToken)
    {
        unitOfWork.ClearTrackedEntities(cancellationToken);
        var requestLog = new Pub_RequestLog
        {
            Id = Extensions.GenerateEntityId(),
            RequestTypeName = requestType,
            RequestParameters = requestParameters,
            RequestInitTime = requestInitTime,
            RequestCompleteTime = requestCompleteTime,
            ResponseError = responseError
        };

        await repository.AddAsync(requestLog, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}