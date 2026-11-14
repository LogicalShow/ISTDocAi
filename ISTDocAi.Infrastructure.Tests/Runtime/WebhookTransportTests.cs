using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using ISTDocAi.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class WebhookTransportTests
{

    [Test]
    public void Handler_has_redirects_disabled_and_bounded_connections()
    {
        using var handler = WebhookHttpTransport.CreateHandler();
        PortAssert.False(handler.AllowAutoRedirect); PortAssert.Equal(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);
        PortAssert.Equal(TimeSpan.FromSeconds(10), handler.ConnectTimeout);
        PortAssert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }
    [Test]
    [Category("Machine")]
    public void Private_CA_handler_accepts_only_its_CA_and_matching_host_default_validation_rejects_it()
    {
        MachineGate.Require();
        using var ca = CreateCa("Test root"); using var other = CreateCa("Other root"); using var leaf = CreateLeaf(ca);
        using var handler = WebhookHttpTransport.CreatePrivateHandler(ca);
        using var otherHandler = WebhookHttpTransport.CreatePrivateHandler(other);
        using var defaultHandler = WebhookHttpTransport.CreateHandler();
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        PortAssert.False(chain.Build(leaf)); // This is the default client's system trust, with no private root installed.
        PortAssert.Null(defaultHandler.SslOptions.RemoteCertificateValidationCallback);
        var errors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
        PortAssert.True(leaf.MatchesHostname("localhost"));
        PortAssert.True(handler.SslOptions.RemoteCertificateValidationCallback!(null!, leaf, chain, errors));
        PortAssert.False(leaf.MatchesHostname("wrong.example"));
        PortAssert.False(handler.SslOptions.RemoteCertificateValidationCallback!(null!, leaf, chain,
            errors | System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch));
        PortAssert.False(otherHandler.SslOptions.RemoteCertificateValidationCallback!(null!, leaf, chain, errors));
        PortAssert.False(handler.SslOptions.RemoteCertificateValidationCallback!(null!, null, null, System.Net.Security.SslPolicyErrors.None));
        // The other consumer's handler has not changed the first one's trust.
        PortAssert.True(handler.SslOptions.RemoteCertificateValidationCallback!(null!, leaf, chain, errors));
    }
    
    [TestCase("localhost", true)]
    [TestCase("wrong.example", false)]
    [Category("Machine")]
    public async Task Real_TLS_handshake_uses_consumer_CA_and_rejects_untrusted_or_wrong_host_certificates(string certificateHost, bool accepted)
    {
        MachineGate.Require();
        using var ca = CreateCa("Handshake root");
        using var leaf = CreateLeaf(ca, certificateHost);
        // Schannel needs an imported key, rather than CertificateRequest's ephemeral key.
        // MachineKeySet also avoids depending on a writable user key profile in a CI host.
        using var serverCertificate = X509CertificateLoader.LoadPkcs12(leaf.Export(X509ContentType.Pfx),
            null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
        await using var app = Server(serverCertificate);
        var posts = 0;
        app.MapPost("/callback", () => { Interlocked.Increment(ref posts); return Results.Ok(); });
        await app.StartAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDocAiAzure(builder.Environment, builder.Configuration);
        using var provider = builder.Services.BuildServiceProvider();
        using var transport = new WebhookHttpTransport(provider.GetRequiredService<IHttpClientFactory>());
        var consumer = new CallbackConsumer(1, "TEST", $"https://localhost:{new Uri(Address(app)).Port}/callback", "secret", "private-ca");
        var request = new WebhookRequest(consumer, "{}"u8.ToArray(), "123", "sha256=test", "correlation", ca.ExportCertificatePem());
        var privateResponse = await transport.SendAsync(request, default);
        // This is the registered named client, with the platform's default certificate validation.
        var defaultResponse = await transport.SendAsync(request with { CaCertificatePem = null }, default);
        PortAssert.Equal(new WebhookResponse(null, WebhookFailure.TlsFailed), defaultResponse);
        if (accepted && privateResponse.StatusCode != 200)
        {
            // Preserve the exact failing TLS stage in test output; production still returns only TLS_FAILED.
            using var probeHandler = WebhookHttpTransport.CreatePrivateHandler(ca);
            var validationReached = false;
            var validate = probeHandler.SslOptions.RemoteCertificateValidationCallback!;
            probeHandler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                validationReached = true;
                return validate(sender, certificate, chain, errors);
            };
            using var probe = new HttpClient(probeHandler);
            try { using var reply = await probe.PostAsync(consumer.CallbackUrl, new StringContent("{}")); }
            catch (HttpRequestException error)
            {
                var stages = new List<string>();
                for (Exception? cause = error; cause is not null; cause = cause.InnerException)
                    stages.Add(cause is System.ComponentModel.Win32Exception native
                        ? $"{cause.GetType().Name} native=0x{native.NativeErrorCode:X8}"
                        : $"{cause.GetType().Name} HRESULT=0x{cause.HResult:X8}");
                PortAssert.Fail($"PFX import and Kestrel startup succeeded; TLS certificate callback reached={validationReached}; {string.Join(" -> ", stages)}");
            }
        }
        PortAssert.Equal(accepted ? new WebhookResponse(200) : new WebhookResponse(null, WebhookFailure.TlsFailed), privateResponse);
        PortAssert.Equal(accepted ? 1 : 0, Volatile.Read(ref posts));
    }
    
    [TestCase(HttpRequestError.ConnectionError, WebhookFailure.ConnectionFailed)]
    [TestCase(HttpRequestError.SecureConnectionError, WebhookFailure.TlsFailed)]
    public async Task Local_failures_are_normalized_without_consumer_text(HttpRequestError error, WebhookFailure expected)
    {
        using var handler = new ReplyHandler(() => throw new HttpRequestException(error, "PRIVATE_CONSUMER_TEXT"));
        using var transport = new WebhookHttpTransport(new Factory(handler));
        var f = new WebhookFakes();
        var response = await transport.SendAsync(new(f.Callback!, [], "123", "sha256=test", "correlation", null), default);
        PortAssert.Null(response.StatusCode); PortAssert.Equal(expected, response.Failure);
    }
    [Test]
    public async Task Timeout_is_normalized_and_response_body_is_never_read_but_is_disposed()
    {
        using var failing = new ReplyHandler(() => throw new TaskCanceledException());
        using var timed = new WebhookHttpTransport(new Factory(failing));
        var f = new WebhookFakes(); var request = new WebhookRequest(f.Callback!, [], "123", "sha256=test", "correlation", null);
        PortAssert.Equal(WebhookFailure.Timeout, (await timed.SendAsync(request, default)).Failure);
        using var content = new UnreadContent();
        using var handler = new ReplyHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var transport = new WebhookHttpTransport(new Factory(handler));
        PortAssert.Equal((short?)200, (await transport.SendAsync(request, default)).StatusCode);
        PortAssert.False(content.Read); PortAssert.True(content.Disposed);
    }
    private sealed class ReplyHandler(Func<HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(reply());
    }
    private sealed class UnreadContent : HttpContent
    {
        public bool Read, Disposed;
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) { Read = true; throw new InvalidOperationException("Must not read body."); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    internal static X509Certificate2 CreateCa(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
    }
    private static X509Certificate2 CreateLeaf(X509Certificate2 ca, string host = "localhost")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + host, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName(host); request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var certificate = request.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        return certificate.CopyWithPrivateKey(key);
    }
    private static WebApplication Server(X509Certificate2? certificate = null)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, endpoint => { if (certificate is not null) endpoint.UseHttps(certificate); }));
        return builder.Build();
    }
    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { Timeout = TimeSpan.FromSeconds(15) };
    }

    [Test, Category("Machine")]
    public async Task Real_http_302_is_recorded_as_failure_and_location_is_never_requested_and_headers_are_exact()
    {
        MachineGate.Require();
        var redirected = 0; byte[]? received = null; IHeaderDictionary? headers = null;
        await using var app = Server();
        app.MapPost("/callback", async context =>
        {
            using var body = new MemoryStream(); await context.Request.Body.CopyToAsync(body); received = body.ToArray(); headers = new HeaderDictionary(context.Request.Headers.ToDictionary(p => p.Key, p => p.Value));
            context.Response.StatusCode = 302; context.Response.Headers.Location = "/target";
        });
        app.MapGet("/target", () => Interlocked.Increment(ref redirected));
        await app.StartAsync();
        using var handler = WebhookHttpTransport.CreateHandler();
        using var transport = new WebhookHttpTransport(new Factory(handler));
        var f = new WebhookFakes();
        f.Send = (request, ct) => transport.SendAsync(request with { Consumer = request.Consumer with { CallbackUrl = Address(app) + "/callback" } }, ct);
        await f.Processor().ExecuteAsync(f.Delivery);
        Assert.Equal(((short?)302, "HTTP 302"), Assert.Single(f.Attempts)); Assert.Equal(0, redirected);
        var sent = Assert.Single(f.Requests); Assert.Equal(sent.Body, received);
        Assert.NotNull(headers); Assert.Equal("application/json; charset=utf-8", headers.ContentType);
        Assert.Equal("DocAi-Webhook/1.0", headers.UserAgent);
        Assert.Equal(sent.Timestamp, headers["X-Timestamp"]); Assert.Equal(sent.Signature, headers["X-Signature"]);
        Assert.Equal(sent.CorrelationId, headers["X-Correlation-Id"]);
        Assert.False(headers.ContainsKey("Authorization"));
    }
}
