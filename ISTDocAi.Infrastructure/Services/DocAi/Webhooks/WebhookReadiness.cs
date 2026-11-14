using ISTDocAi.Application.Services.DocAi.Webhooks;
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
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
public sealed class WebhookReadiness(ICallbackDirectory callbacks, ISecretStore secrets, SecretFailureLog failures)
{
    private string? preferredSecret;
    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        var consumers = await callbacks.ReadAsync(ct);
        if (consumers.Count == 0) return true;
        var preferred = Volatile.Read(ref preferredSecret);
        foreach (var consumer in consumers.OrderByDescending(c => c.WebhookSecretName == preferred))
        {
            try
            {
                _ = await secrets.GetSigningKeyAsync(consumer.WebhookSecretName, ct);
                Volatile.Write(ref preferredSecret, consumer.WebhookSecretName);
                return true;
            }
            catch (SecretUnavailableException e) { failures.Report(e.SecretName); }
        }
        return false;
    }
}
