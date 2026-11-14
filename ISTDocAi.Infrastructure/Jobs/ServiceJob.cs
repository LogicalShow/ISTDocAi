using ISTDocAi.Application.Helpers.Commands;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ISTDocAi.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class ServiceJob : IJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<ServiceJob> _logger;

    public ServiceJob(IMediator mediator, ILogger<ServiceJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        var jobDataMap = context.JobDetail.JobDataMap;
        var serviceName = jobDataMap.GetString("ServiceName");
        var jobId = context.JobDetail.Key.ToString();

        try
        {
            _logger.LogInformation("Starting job execution for service {ServiceName}", serviceName);
            
            var command = new ExecuteServiceJobCommand(serviceName, jobId);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Job execution completed with failures for service {ServiceName}", serviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job for service {ServiceName}", serviceName);
            throw; // Rethrowing to let Quartz handle the error according to its policies
        }
    }
}