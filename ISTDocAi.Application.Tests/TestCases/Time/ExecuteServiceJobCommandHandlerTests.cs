using ISTDocAi.Application.Helpers.Commands;
using ISTDocAi.Domain.Events;
using Microsoft.Extensions.Time.Testing;

namespace ISTDocAi.Application.Tests.TestCases.Time;

[TestFixture]
public class ExecuteServiceJobCommandHandlerTests
{
    [Test]
    public async Task Handle_ShouldWaitOnInjectedClock_AndPublishClockTimes()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(start);
        var mediator = new CapturingMediator();
        var handler = new ExecuteServiceJobCommandHandler(mediator, timeProvider);

        // Act
        var handleTask = handler.Handle(new ExecuteServiceJobCommand("ReportJob", "job-1"), CancellationToken.None);
        var completedBeforeAdvance = handleTask.IsCompleted;

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var result = await handleTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        var published = mediator.Published.OfType<ServiceJobExecutedEvent>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(completedBeforeAdvance, Is.False, "Handler must wait on the injected TimeProvider, not the real clock.");
            Assert.That(result, Is.True);
            Assert.That(published.WasSuccessful, Is.True);
            Assert.That(published.ExecutionStartTime, Is.EqualTo(start.DateTime));
            Assert.That(published.ExecutionEndTime, Is.EqualTo(start.AddSeconds(1).DateTime));
            Assert.That(published.ExecutionStartTime.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public async Task Handle_WhenCancelled_ShouldPublishFailureWithClockTimes()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(start);
        var mediator = new CapturingMediator();
        var handler = new ExecuteServiceJobCommandHandler(mediator, timeProvider);
        using var cts = new CancellationTokenSource();

        // Act
        var handleTask = handler.Handle(new ExecuteServiceJobCommand("ReportJob", "job-1"), cts.Token);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));
        cts.Cancel();
        var result = await handleTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        var published = mediator.Published.OfType<ServiceJobExecutedEvent>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(published.WasSuccessful, Is.False);
            Assert.That(published.ExecutionEndTime - published.ExecutionStartTime, Is.EqualTo(TimeSpan.FromMilliseconds(400)));
        });
    }

    private sealed class CapturingMediator : IMediator
    {
        public List<object> Published { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
