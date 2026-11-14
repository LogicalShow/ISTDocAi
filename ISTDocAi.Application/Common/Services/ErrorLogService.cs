using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.Serialization;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Application.Common.Services;

public interface IErrorLogService
{
    Task Log(
        string errorTypeName,
        string errorMessage,
        string? stackTrace,
        object? details,
        CancellationToken cancellationToken);
}

public class ErrorLogService(
    IRepository<Pub_ErrorLog> repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogPayloadSerializer logPayloadSerializer) : IErrorLogService
{
    public async Task Log(
        string errorTypeName,
        string errorMessage,
        string? stackTrace,
        object? details,
        CancellationToken cancellationToken)
    {
        var errorLog = new Pub_ErrorLog
        {
            Id = Extensions.GenerateEntityId(),
            ErrorTime = timeProvider.GetLocalDateTime(),
            ErrorTypeName = errorTypeName,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
            Details = logPayloadSerializer.Serialize(details)
        };

        await repository.AddAsync(errorLog, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
