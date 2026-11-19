using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Pricing;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using Microsoft.Data.SqlClient;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;
public sealed class SqlAzurePriceCheckRepository : IAzurePriceCheckStore
{
    private readonly SqlExecution execution;
    internal SqlAzurePriceCheckRepository(SqlExecution execution) => this.execution = execution;
    public Task<DateTimeOffset?> LatestCompletedAsync(CancellationToken ct) => execution.One<DateTimeOffset?>("S36", null, ct);
    public async Task<IReadOnlyList<AzurePriceCheckResult>> SaveAsync(AzurePriceCheck check, CancellationToken ct)
    {
        static string? B(string? value) => value is null ? null : value[..Math.Min(value.Length, 64)];
        static decimal? D(decimal? value) => value is >= -99999999999.99999999m and <= 99999999999.99999999m ? value : null;
        var payload = JsonSerializer.Serialize(check.Decisions.Select(d => new
        {
            d.MeterCode, d.MatchedRowCount, MeterId = B(d.Observation?.MeterId), Currency = B(d.Observation?.Currency),
            Region = B(d.Observation?.Region), Unit = B(d.Observation?.Unit), Type = B(d.Observation?.Type),
            TierMinimumUnits = D(d.Observation?.TierMinimumUnits), ObservedPrice = D(d.Observation?.Price),
            AzureEffectiveFrom = d.Observation?.AzureEffectiveFrom?.UtcDateTime, d.Source,
            // Fingerprint input deliberately excludes check/read timestamps.
            NormalizedObservation = JsonSerializer.Serialize((d.Matches ?? (d.Observation is null ? [] : new[] { d.Observation })).Select(o => JsonSerializer.Serialize(o)).Order(StringComparer.Ordinal))
        }));
        var values = new { check.CheckId, check.StartedAtUtc, check.CheckedAtUtc, check.Outcome, check.ReasonCode, PayloadJson = payload };
        for (var attempt = 0; ; attempt++)
        {
            try { return await execution.Many<AzurePriceCheckResult>(check.Outcome == "NoChange" ? "S34" : "S35", values, ct); }
            catch (SqlException e) when (attempt < 2 && e.Number is -2 or 1205 or 233 or 64 or 10053 or 10054 or 10060)
            { await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), ct); }
        }
    }
}
