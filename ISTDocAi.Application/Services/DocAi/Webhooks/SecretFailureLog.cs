using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Application.Services.DocAi.Webhooks;
public sealed class SecretFailureLog(TimeProvider clock, ILogger<SecretFailureLog> logger)
{
    private readonly Dictionary<string, DateTimeOffset> next = new(StringComparer.Ordinal);
    public void Report(string name)
    {
        lock (next)
        {
            var now = clock.GetUtcNow();
            if (next.TryGetValue(name, out var until) && until > now) return;
            next[name] = now.AddMinutes(1);
            logger.LogCritical("{event}", "keyvault.unavailable");
        }
    }
}
