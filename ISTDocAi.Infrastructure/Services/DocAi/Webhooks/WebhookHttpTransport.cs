using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
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
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
namespace ISTDocAi.Infrastructure.Services.DocAi.Webhooks;

public sealed class WebhookHttpTransport(IHttpClientFactory clients) : IWebhookTransport, IDisposable
{
    private sealed class PrivateClient(string thumbprint, HttpClient client, X509Certificate2 ca) : IDisposable
    {
        public string Thumbprint { get; } = thumbprint;
        public HttpClient Client { get; } = client;
        public int Users;
        public bool Retired;
        public void Dispose() { Client.Dispose(); ca.Dispose(); }
    }
    private readonly Dictionary<short, PrivateClient> privateClients = [];
    private readonly object gate = new();
    // A rotated handler lives only until its in-flight sends release it.
    private readonly List<PrivateClient> retired = [];
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), ConnectTimeout = TimeSpan.FromSeconds(10)
    };
    internal static SocketsHttpHandler CreatePrivateHandler(X509Certificate2 ca)
    {
        var handler = CreateHandler();
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) => ValidatePrivateCertificate(ca, certificate, chain, errors);
        return handler;
    }
    internal static bool ValidatePrivateCertificate(X509Certificate2 ca, X509Certificate? certificate, X509Chain? supplied, SslPolicyErrors errors)
    {
        if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0) return false;
        using var leaf = new X509Certificate2(certificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1"));
        if (supplied is not null)
            foreach (var element in supplied.ChainElements.Cast<X509ChainElement>().Skip(1)) chain.ChainPolicy.ExtraStore.Add(element.Certificate);
        return chain.Build(leaf);
    }
    private PrivateClient AcquirePrivate(CallbackConsumer consumer, string pem)
    {
        X509Certificate2 ca;
        try { ca = PrivateCa.Parse(pem); }
        catch (Exception) { throw new SecretUnavailableException(consumer.CallbackCaCertName!); }
        lock (gate)
        {
            if (privateClients.TryGetValue(consumer.ConsumerId, out var entry))
            {
                if (entry.Thumbprint == ca.Thumbprint) { ca.Dispose(); entry.Users++; return entry; }
                entry.Retired = true;
                if (entry.Users == 0) entry.Dispose(); else retired.Add(entry);
            }
            var client = new HttpClient(CreatePrivateHandler(ca)) { Timeout = TimeSpan.FromSeconds(15) };
            var created = new PrivateClient(ca.Thumbprint, client, ca) { Users = 1 };
            privateClients[consumer.ConsumerId] = created;
            return created;
        }
    }
    private void ReleasePrivate(PrivateClient entry)
    {
        lock (gate)
        {
            entry.Users--;
            if (entry.Retired && entry.Users == 0) { retired.Remove(entry); entry.Dispose(); }
        }
    }
    public async Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken ct)
    {
        // Private-CA configuration errors escape as SecretUnavailable, so they do not spend an attempt.
        var entry = request.CaCertificatePem is null ? null : AcquirePrivate(request.Consumer, request.CaCertificatePem);
        var client = entry?.Client ?? clients.CreateClient("webhooks");
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, request.Consumer.CallbackUrl);
            message.Options.Set(new HttpRequestOptionsKey<bool>("DocAi.SensitiveUrl"), true);
            message.Content = new ByteArrayContent(request.Body);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            message.Headers.Add("X-Timestamp", request.Timestamp);
            message.Headers.Add("X-Signature", request.Signature);
            message.Headers.Add("X-Correlation-Id", request.CorrelationId);
            message.Headers.UserAgent.ParseAdd("DocAi-Webhook/1.0");
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            return new((short)response.StatusCode);
        }
        catch (OperationCanceledException) { return new(null, WebhookFailure.Timeout); }
        catch (HttpRequestException e)
        {
            var tls = e.HttpRequestError == HttpRequestError.SecureConnectionError;
            for (Exception? cause = e; cause is not null; cause = cause.InnerException) tls |= cause is AuthenticationException;
            return new(null, tls ? WebhookFailure.TlsFailed : WebhookFailure.ConnectionFailed);
        }
        finally { if (entry is null) client.Dispose(); else ReleasePrivate(entry); }
    }
    public void Dispose()
    {
        lock (gate)
        {
            foreach (var entry in privateClients.Values.Concat(retired)) entry.Dispose();
            privateClients.Clear(); retired.Clear();
        }
    }
}
