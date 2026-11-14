using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.Serialization;
using ISTDocAi.Application.Common.Services;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Application.Features.IAM.Auth.Commands.Login;
using MediatR;

namespace ISTDocAi.Application.Common.Behaviors;

public class RequestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestLogService _requestLogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogPayloadSerializer _logPayloadSerializer;

    private static readonly HashSet<string> _excludedRequests = new(StringComparer.OrdinalIgnoreCase)
    {
       nameof(LoginCommand)
    };

    private static bool ShouldSkipLogging()
    {
        var requestName = typeof(TRequest).Name;
        return typeof(TRequest).Namespace?.StartsWith("ISTDocAi.Application.Features.DocAi.", StringComparison.Ordinal) == true
            || requestName.EndsWith("Query") || _excludedRequests.Contains(requestName);
    }

    public RequestLoggingBehavior(
        IRequestLogService technicalLogService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogPayloadSerializer logPayloadSerializer)
    {
        _requestLogService = technicalLogService;
        _timeProvider = timeProvider;
        _logPayloadSerializer = logPayloadSerializer;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (ShouldSkipLogging())
        {
            return await next(cancellationToken);
        }

        TResponse response;
        var requestInitTime = _timeProvider.GetLocalDateTime();

        try
        {
            response = await next(cancellationToken);
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            // log request error
            await _requestLogService.Log(
                    requestType: typeof(TRequest).Name,
                    requestParameters: _logPayloadSerializer.Serialize(request),
                    requestInitTime: requestInitTime,
                    requestCompleteTime: _timeProvider.GetLocalDateTime(),
                    responseError: ex.Message,
                    cancellationToken: cancellationToken);
            throw;
        }
        finally
        {
            // log request details
            await _requestLogService.Log(
                    requestType: typeof(TRequest).Name,
                    requestParameters: _logPayloadSerializer.Serialize(request),
                    requestInitTime: requestInitTime,
                    requestCompleteTime: _timeProvider.GetLocalDateTime(),
                    responseError: null,
                    cancellationToken: cancellationToken);
        }

        // Proceed to the next behavior in the pipeline
        return response;
    }
}
