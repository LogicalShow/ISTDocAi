using System.Net;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ISTDocAi.UI.Server.Tests.DocAi;

public sealed class WebhookAliveTests
{
    private sealed class Events : IConsumerWebhookEvents
    {
        public List<short> Consumers { get; } = [];
        public bool Throw { get; init; }
        public void NotifyAlive(short consumerId)
        {
            Consumers.Add(consumerId);
            if (Throw) throw new InvalidOperationException("test failure");
        }
    }
    private sealed class Factory(Events events) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(s => s.AddSingleton<IConsumerWebhookEvents>(events));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public async Task Valid_consumer_authentication_notifies_and_notification_failure_cannot_fail_request(bool throws)
    {
        var events = new Events { Throw = throws };
        using var app = new Factory(events); using var client = app.Client();
        using var response = await client.GetAsync("/api/v2/catalog");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(events.Consumers, Is.EqualTo(new[] { app.Ports.Consumer.ConsumerId }));
    }
    [TestCase(null)] [TestCase("invalid")]
    public async Task Unauthenticated_request_never_notifies(string? key)
    {
        var events = new Events();
        using var app = new Factory(events); using var client = app.Client(key);
        using var response = await client.GetAsync("/api/v2/catalog");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(events.Consumers, Is.Empty);
    }
}
