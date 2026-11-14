using Microsoft.Extensions.DependencyInjection;
namespace ISTDocAi.Infrastructure.Services.DocAi.Analysis;
// Request-scoped composition retains the original check order and connection lifetime.
public static class DocAiReadiness
{
    public static async Task<bool> CheckDependenciesAsync(IServiceProvider services, CancellationToken ct)
    {
                if (!await services.GetRequiredService<ISTDocAi.Infrastructure.Services.DocAi.Mapping.MappingProfileReadiness>().CheckAsync(ct)) return false;
                if (!services.GetRequiredService<ISTDocAi.Infrastructure.Services.DocAi.Identity.CertificateIdentity>().IsReady) return false;
                var sql = services.GetRequiredService<ISTDocAi.Infrastructure.Data.Sql.DocAi.ISqlConnectionFactory>();
                var blob = services.GetRequiredService<ISTDocAi.Application.Common.Interfaces.DocAi.Storage.IDocumentStore>();
                await using var connection = await sql.OpenAsync(ct);
                await blob.CheckAsync(ct);
                if (!await services.GetRequiredService<ISTDocAi.Infrastructure.Services.DocAi.Analysis.DocumentIntelligenceReadiness>().CheckAsync(ct)) return false;
                if (!await services.GetRequiredService<ISTDocAi.Infrastructure.Services.DocAi.Webhooks.WebhookReadiness>().CheckAsync(ct)) return false;
                return true;
    }
}
