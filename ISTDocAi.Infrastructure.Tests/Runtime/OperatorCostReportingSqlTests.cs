using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Text.Json;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed partial class PageQuotaSqlTests
{
    private async Task<JsonElement> OperatorReport(string label = "S29", long? grantId = null, long? afterChargeId = null, int limit = 100)
    {
        var text = await db.Scalar<string>(new SqlStatementCatalog()[label], new { consumerId=(short)1, grantId, afterChargeId, limit });
        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }
    [Test, Category("Sql")]
    public async Task Operator_cost_attribution_reversals_unpriced_and_reconciliation()
    {
        SqlTestGuard.Require();
        var job=await Claim(SqlDatabase.Request("INV") with {PageCount=6});
        await Classify(job);
        // This grant lands between classify and extract. Both belong to the classify charge's prelude.
        await Task.Delay(20);
        var grant=await Allowance(100);
        await Task.Delay(20);
        await Extract(job,"inv-line");
        await Complete(job);
        var orphan=await Claim(SqlDatabase.Request());
        var op=await db.Accepted(orphan);
        var unpriced=await Claim(SqlDatabase.Request());
        var unpricedOp=await db.Accepted(unpriced);
        await db.Execute("""
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,PricePer1000,OccurredAtUtc,CorrelationId)
            VALUES(1,@JobId,1,@op,'DI_CUSTOM_EXTRACT','bl-v1',2,10,SYSUTCDATETIME(),'orphan');
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,OccurredAtUtc,CorrelationId)
            VALUES(1,@unpricedJobId,1,@unpricedOp,'DI_CUSTOM_CLASSIFY','inv-classifier',1,SYSUTCDATETIME(),'unpriced');
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,PricePer1000,OccurredAtUtc,ReversesUsageId,CorrelationId)
            SELECT TOP(1) ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,-Pages,PricePer1000,OccurredAtUtc,UsageId,CorrelationId
            FROM dbo.UsageLedger WHERE JobId=@charged AND MeterCode='DI_CUSTOM_CLASSIFY' AND ReversesUsageId IS NULL;
            """,new {orphan.JobId,op,charged=job.JobId,unpricedJobId=unpriced.JobId,unpricedOp});
        var report=await OperatorReport();
        var totals=report.GetProperty("totals"); var prelude=report.GetProperty("prelude");
        Assert.Equal(0,prelude.GetProperty("azureClassifyPages").GetInt64());
        // Meter pages are what Azure billed (the helpers record 1), not the run's PageCount.
        Assert.Equal(1,prelude.GetProperty("azureExtractPages").GetInt64());
        Assert.Equal(1,totals.GetProperty("unpricedRows").GetInt64());
        Assert.Equal(0.02m,totals.GetProperty("unchargedAzureCostUsd").GetDecimal());
        var cycle=Assert.Single(report.GetProperty("cycles").EnumerateArray());
        Assert.Equal(0m,cycle.GetProperty("azureCostUsd").GetDecimal());
        Assert.Equal(JsonValueKind.Null,cycle.GetProperty("azureCostPerChargedPageUsd").ValueKind);
        var charged=prelude.GetProperty("azureCostUsd").GetDecimal();
        Assert.Equal(charged,totals.GetProperty("chargedRunsAzureCostUsd").GetDecimal());
        Assert.Equal(charged+0.02m,totals.GetProperty("azureCostUsd").GetDecimal());
        // Uncharged history (old tests, pre-quota runs) must not inflate the per-page cost.
        Assert.Equal(charged/6,totals.GetProperty("azureCostPerChargedPageUsd").GetDecimal(),8);
        var docs=await OperatorReport("S30",0);
        var doc=Assert.Single(docs.GetProperty("documents").EnumerateArray());
        Assert.Equal(charged,doc.GetProperty("azureCostUsd").GetDecimal());
        Assert.Equal(1,doc.GetProperty("azureExtractPages").GetInt64());
        var cursor=doc.GetProperty("chargeId").GetInt64();
        Assert.Empty((await OperatorReport("S30",0,cursor)).GetProperty("documents").EnumerateArray());
        Assert.Empty((await OperatorReport("S30",grant)).GetProperty("documents").EnumerateArray());
        foreach(var label in new[]{"S27","S28"})
        {
            var json=await db.Scalar<string>(new SqlStatementCatalog()[label],new {consumerId=(short)1,grantId=(long?)0,afterChargeId=(long?)null,limit=100});
            foreach(var forbidden in new[]{"azure","cost","meter","price","usd"}) Assert.DoesNotContain(forbidden,json,StringComparison.OrdinalIgnoreCase);
        }
    }
    [Test, Category("Sql")]
    public async Task Operator_charged_unpriced_cost_is_null_safe()
    {
        SqlTestGuard.Require();
        await ChargeAt(3,Epoch,Epoch);
        var report=await OperatorReport();
        var prelude=report.GetProperty("prelude");
        Assert.Equal(1,prelude.GetProperty("unpricedRows").GetInt64());
        Assert.Equal(0m,prelude.GetProperty("azureCostUsd").GetDecimal());
        Assert.Equal(0m,prelude.GetProperty("azureCostPerChargedPageUsd").GetDecimal());
        Assert.Equal(0m,Assert.Single((await OperatorReport("S30",0)).GetProperty("documents").EnumerateArray()).GetProperty("azureCostUsd").GetDecimal());
    }
    [Test, Category("Sql")]
    public async Task Operator_priced_run_is_assigned_to_non_prelude_cycle()
    {
        SqlTestGuard.Require();
        var grant=await Allowance(100);
        await Task.Delay(20);
        var job=await Claim(SqlDatabase.Request() with {PageCount=4});
        await Extract(job); await Complete(job);
        var report=await OperatorReport();
        var cycle=Assert.Single(report.GetProperty("cycles").EnumerateArray());
        var expected=await db.Scalar<decimal>("SELECT SUM(AzureCostUsd) FROM dbo.UsageLedger WHERE ConsumerId=1");
        Assert.True(expected>0);
        Assert.Equal(grant,cycle.GetProperty("topUpGrantId").GetInt64());
        Assert.Equal(1,cycle.GetProperty("azureExtractPages").GetInt64());
        Assert.Equal(expected,cycle.GetProperty("azureCostUsd").GetDecimal());
        Assert.Equal(expected/4,cycle.GetProperty("azureCostPerChargedPageUsd").GetDecimal());
        Assert.Equal(0m,report.GetProperty("prelude").GetProperty("azureCostUsd").GetDecimal());
        var docs=await OperatorReport("S30",grant);
        Assert.Equal(expected,Assert.Single(docs.GetProperty("documents").EnumerateArray()).GetProperty("azureCostUsd").GetDecimal());
    }
    [Test, Category("Sql")]
    public async Task Operator_empty_cost_is_zero_and_per_page_is_null()
    {
        SqlTestGuard.Require();
        var totals=(await OperatorReport()).GetProperty("totals");
        Assert.Equal(0m,totals.GetProperty("azureCostUsd").GetDecimal());
        Assert.Equal(JsonValueKind.Null,totals.GetProperty("azureCostPerChargedPageUsd").ValueKind);
        Assert.Equal(2,totals.GetProperty("currentListPrices").GetArrayLength());
    }
    [Test, Category("Sql")]
    public async Task Operator_upgrade_is_rerunnable_and_owner_reads_reports()
    {
        SqlTestGuard.Require();
        await db.DisposeAsync();
        db = new SqlDatabase { UseSchemaScript = true };
        await db.InitializeAsync();
        await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"06_page_quota_upgrade.sql")));
        var upgrade=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"08_operator_cost_report_upgrade.sql"));
        await db.Execute(upgrade);
        await db.Execute(upgrade);
        foreach(var label in new[]{"S29","S30"})
        {
            var json=await db.Scalar<string>(new SqlStatementCatalog()[label],new {consumerId=(short)1,grantId=(long?)0,afterChargeId=(long?)null,limit=100});
            Assert.StartsWith("{",json);
        }

    }
}
