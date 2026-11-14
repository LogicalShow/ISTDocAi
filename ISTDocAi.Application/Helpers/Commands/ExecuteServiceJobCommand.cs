using MediatR;

namespace ISTDocAi.Application.Helpers.Commands;

public class ExecuteServiceJobCommand : IRequest<bool>
{
    public string ServiceName { get; }
    public string JobId { get; }

    public ExecuteServiceJobCommand(string serviceName, string jobId)
    {
        ServiceName = serviceName;
        JobId = jobId;
    }
} 