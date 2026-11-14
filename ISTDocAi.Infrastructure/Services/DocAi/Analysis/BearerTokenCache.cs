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
using Azure.Core;
using System.Net.Http.Headers;
namespace ISTDocAi.Infrastructure.Services.DocAi.Analysis;

public sealed class BearerTokenCache(TokenCredential credential, TimeProvider clock) : IDisposable
{
    private sealed class Snapshot(AccessToken token)
    {
        public AccessToken Token { get; } = token;
    }
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot? cached;
    private bool Fresh(Snapshot? snapshot) => snapshot is not null &&
        !string.IsNullOrEmpty(snapshot.Token.Token) && clock.GetUtcNow() < snapshot.Token.ExpiresOn.AddMinutes(-5);
    public async ValueTask<string> GetAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref cached);
        if (Fresh(snapshot)) return snapshot!.Token.Token;
        await gate.WaitAsync(ct);
        try
        {
            snapshot = Volatile.Read(ref cached);
            if (!Fresh(snapshot))
            {
                AccessToken token;
                try
                {
                    token = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception e) when (e is not (OutOfMemoryException or StackOverflowException))
                {
                    // Do not retain credential exception text or inner exceptions in diagnostic-safe outcomes.
                    throw new AnalysisUnavailableException(AnalysisUnavailableReason.CredentialUnavailable);
                }
                snapshot = new(token);
                if (!Fresh(snapshot)) throw new AnalysisUnavailableException(AnalysisUnavailableReason.CredentialUnavailable);
                Volatile.Write(ref cached, snapshot);
            }
            return snapshot!.Token.Token;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
