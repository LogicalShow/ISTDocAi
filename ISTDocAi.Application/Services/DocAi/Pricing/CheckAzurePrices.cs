using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Pricing;
namespace ISTDocAi.Application.Services.DocAi.Pricing;
public sealed class CheckAzurePrices(IAzureRetailPriceSource source, IAzurePriceCheckStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<AzurePriceCheckResult>> RunAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var started = clock.GetUtcNow();
        AzurePriceCheck check;
        try
        {
            var rows = await source.ReadAsync(ct);
            var now = clock.GetUtcNow();
            var decisions = AzurePricePolicy.Evaluate(rows, now);
            var reason = decisions.FirstOrDefault(x => x.ReasonCode is not null)?.ReasonCode;
            check = new(id, started, now, reason is null ? "NoChange" : "Refused", reason, decisions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            check = new(id, started, clock.GetUtcNow(), "Failed", "PRICE_SOURCE_FAILED",
                AzurePricePolicy.MeterCodes.Select(code => new AzurePriceDecision(code, null, 0, "PRICE_SOURCE_FAILED", null)).ToArray());
        }
        // Store retries retain this exact identity, including after an uncertain commit.
        return await store.SaveAsync(check, ct);
    }
}
