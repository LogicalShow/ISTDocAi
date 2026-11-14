using System;
using System.Threading;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Domain.Events;
using MediatR;

namespace ISTDocAi.Application.Helpers.Commands;

public class ExecuteServiceJobCommandHandler : IRequestHandler<ExecuteServiceJobCommand, bool>
{
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;

    public ExecuteServiceJobCommandHandler(
        IMediator mediator,
        TimeProvider timeProvider)
    {
        _mediator = mediator;
        _timeProvider = timeProvider;
    }

    public async Task<bool> Handle(ExecuteServiceJobCommand request, CancellationToken cancellationToken)
    {
        var startTime = _timeProvider.GetLocalDateTime();
        try
        {
            // TODO: Implement your actual service job logic here
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken); // Placeholder for actual work

            var endTime = _timeProvider.GetLocalDateTime();
            await _mediator.Publish(new ServiceJobExecutedEvent(
                request.ServiceName,
                startTime,
                endTime,
                true
            ), cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            var endTime = _timeProvider.GetLocalDateTime();
            await _mediator.Publish(new ServiceJobExecutedEvent(
                request.ServiceName,
                startTime,
                endTime,
                false,
                ex.Message
            ), cancellationToken);

            return false;
        }
    }
}