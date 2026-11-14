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
using Microsoft.Extensions.DependencyInjection;
namespace ISTDocAi.Infrastructure.Services.DocAi.Analysis;

public sealed class DocumentIntelligenceReadiness(IServiceScopeFactory scopes, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset expires;
    private bool ready;

    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (clock.GetUtcNow() < expires) return ready;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                ready = await scope.ServiceProvider.GetRequiredService<IDocumentAnalyzer>().CheckAccessAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { ready = false; } // Never log exception messages, URLs or tokens.
            expires = clock.GetUtcNow().AddSeconds(60);
            return ready;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
