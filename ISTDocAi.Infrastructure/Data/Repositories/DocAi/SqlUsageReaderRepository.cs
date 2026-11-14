using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlUsageReaderRepository : IUsageReader
{
    private readonly SqlExecution execution;
    private readonly ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService.IPageAllowanceStatusService allowance;
    internal SqlUsageReaderRepository(SqlExecution execution, ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService.IPageAllowanceStatusService allowance)
    {
        this.execution = execution;
        this.allowance = allowance;
    }

    public async Task<ISTDocAi.Application.Common.Contracts.DocAi.V1.UsageResponse> GetUsageAsync(short consumerId, int months, DateTimeOffset asOfUtc, CancellationToken ct)
    {
        var row = await execution.One<UsageRow>("S26", new { consumerId, months, asOfUtc = asOfUtc.UtcDateTime }, ct)
            ?? throw new InvalidOperationException("Usage consumer is missing.");
        var state = allowance.Calculate(row.QuotaMode, row.Remaining, row.CycleBase, row.WarningPercent);
        return new(row.AsOfUtc, "pages", row.QuotaMode, state.Status, row.Granted, row.Used, row.Reserved,
            row.Remaining, row.WarningPercent, row.CycleBase, state.WarningAtPages,
            row.LastTopUpAtUtc is { } at ? new(at, row.LastTopUpPages!.Value) : null,
            row.MaxPagesPerJob, row.DailyCap, row.SubmittedTodayPages, row.ChargedTodayPages, "UTC",
            System.Text.Json.JsonSerializer.Deserialize<ISTDocAi.Application.Common.Contracts.DocAi.V1.UsageMonth[]>(row.MonthsJson, ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options)!);
    }

    public async Task<ISTDocAi.Application.Common.Contracts.DocAi.V1.TopUpCyclesResponse?> GetTopUpCyclesAsync(short consumerId, long? grantId, CancellationToken ct)
    {
        var json = await execution.One<string>("S27", new { consumerId, grantId }, ct);
        return json is null ? null : System.Text.Json.JsonSerializer.Deserialize<ISTDocAi.Application.Common.Contracts.DocAi.V1.TopUpCyclesResponse>(json, ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options);
    }

    public async Task<ISTDocAi.Application.Common.Contracts.DocAi.V1.CycleDocumentsResponse?> GetCycleDocumentsAsync(short consumerId, long grantId, int limit, long? afterChargeId, CancellationToken ct)
    {
        var json = await execution.One<string>("S28", new { consumerId, grantId, limit, afterChargeId }, ct);
        return json is null ? null : System.Text.Json.JsonSerializer.Deserialize<ISTDocAi.Application.Common.Contracts.DocAi.V1.CycleDocumentsResponse>(json, ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options);
    }
    private sealed class UsageRow
    {
        public DateTimeOffset AsOfUtc { get; set; }
        public string QuotaMode { get; set; } = "";
        public int WarningPercent { get; set; }
        public long Granted { get; set; }
        public long Used { get; set; }
        public long Reserved { get; set; }
        public long Remaining { get; set; }
        public long? CycleBase { get; set; }
        public DateTimeOffset? LastTopUpAtUtc { get; set; }
        public long? LastTopUpPages { get; set; }
        public int MaxPagesPerJob { get; set; }
        public int DailyCap { get; set; }
        public long SubmittedTodayPages { get; set; }
        public long ChargedTodayPages { get; set; }
        public string MonthsJson { get; set; } = "[]";
    }
}
