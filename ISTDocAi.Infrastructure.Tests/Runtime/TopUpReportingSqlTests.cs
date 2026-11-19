using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using Dapper;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed partial class PageQuotaSqlTests
{
    private Task<TopUpCyclesResponse?> Cycles(long? grant = null, short consumer = 1) => db.Store.GetTopUpCyclesAsync(consumer, grant, default);
    private Task<CycleDocumentsResponse?> Documents(long grant = 0, int limit = 100, long? after = null, short consumer = 1) =>
        db.Store.GetCycleDocumentsAsync(consumer, grant, limit, after, default);
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private async Task<long> Allowance(long pages, string kind = "TopUp", long? original = null, DateTimeOffset? at = null, Guid? request = null)
    {
        var requestId = request ?? Guid.NewGuid();
        await db.Execute("""
            INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OriginalGrantId,OperatorIdentity,Note,RequestId)
            VALUES(1,@pages,@kind,@original,'private operator','private note',@requestId);
            """, new { pages, kind, original, requestId });
        var id = await db.Scalar<long>("SELECT GrantId FROM dbo.PageAllowanceGrants WHERE RequestId=@requestId", new { requestId });
        if (at is { } time)
        {
            // Test-owned database only: deterministic timestamps, including ties. Production accounting stays append-only.
            var args = new DynamicParameters(new { id }); args.Add("at", time.UtcDateTime, DbType.DateTime2);
            await db.Execute("""
                BEGIN TRANSACTION;
                DISABLE TRIGGER dbo.TR_Allowance_AppendOnly ON dbo.PageAllowanceGrants;
                UPDATE dbo.PageAllowanceGrants SET RecordedAtUtc=@at WHERE GrantId=@id;
                ENABLE TRIGGER dbo.TR_Allowance_AppendOnly ON dbo.PageAllowanceGrants;
                COMMIT;
                """, args);
        }
        return id;
    }
    private static void Reconcile(TopUpCyclesResponse report)
    {
        var all = report.Cycles.Prepend(report.Prelude!).ToArray();
        Assert.Equal(report.Totals.ChargedPages, all.Sum(x => x.ChargedPages));
        Assert.Equal(report.Totals.ChargedRuns, all.Sum(x => x.ChargedRuns));
        Assert.Equal(report.Totals.GrantedPages, all.Sum(x => x.TopUpPages + x.AdjustmentPages + x.ReversalPages));
        foreach (var cycle in all)
            Assert.Equal(cycle.OpeningLedgerBalance + cycle.AdjustmentPages + cycle.ReversalPages - cycle.ChargedPages, cycle.ClosingLedgerBalance);
        var chronological = all.OrderBy(x => x.StartAtUtc).ThenBy(x => x.TopUpGrantId).ToArray();
        Assert.Equal(report.Totals.LedgerBalance, chronological[^1].ClosingLedgerBalance);
    }

    [Test, Category("Sql")]
    public async Task Topup_reporting_carry_reservation_release_and_settlement_are_distinct()
    {
        SqlTestGuard.Require();
        await ChargeAt(2, Epoch, Epoch);
        var held = await Claim(SqlDatabase.Request() with { PageCount=3 });
        var first = await Allowance(100, at:Epoch.AddDays(1));
        await ChargeAt(10, Epoch.AddDays(1), Epoch.AddDays(2));
        var second = await Allowance(50, at:Epoch.AddDays(3));
        var report = (await Cycles())!;
        Assert.Equal(new long?[] { second,first }, report.Cycles.Select(x=>x.TopUpGrantId));
        var cycle = report.Cycles[1];
        Assert.Equal(98, cycle.OpeningLedgerBalance);
        Assert.Equal(95, cycle.RecordedAvailableAfterTopUp);
        Assert.Equal(10, cycle.ChargedPages);
        Assert.Equal(second, cycle.NextTopUpGrantId);
        Assert.Equal(Epoch.AddDays(3), cycle.EndAtUtc);
        Assert.Equal(TimeSpan.Zero, cycle.StartAtUtc!.Value.Offset);
        Assert.Equal(138, report.Cycles[0].OpeningLedgerBalance);
        Assert.Equal(138, report.Totals.LedgerBalance);
        Assert.Equal(3, report.Totals.CurrentReservedPages);
        Assert.Equal(135, report.Totals.CurrentAvailablePages);
        Reconcile(report);
        await Fail(held);
        report = (await Cycles())!;
        Assert.Equal(0, report.Totals.CurrentReservedPages);
        Assert.Equal(138, report.Totals.CurrentAvailablePages);
        Assert.Equal(12, report.Totals.ChargedPages);
        Reconcile(report);
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_settlement_moves_reservation_into_charged_consumption()
    {
        SqlTestGuard.Require();
        var held=await Claim(SqlDatabase.Request() with {PageCount=3});
        var grant=await Allowance(10);
        var before=(await Cycles(grant))!;
        Assert.Equal(10,before.Cycles[0].OpeningLedgerBalance);
        Assert.Equal(7,before.Cycles[0].RecordedAvailableAfterTopUp);
        Assert.Equal(3,before.Totals.CurrentReservedPages);
        Assert.Equal(0,before.Totals.ChargedPages);
        await Extract(held); await Complete(held);
        var after=(await Cycles(grant))!;
        Assert.Equal(0,after.Totals.CurrentReservedPages);
        Assert.Equal(3,after.Totals.ChargedPages);
        Assert.Equal(7,after.Totals.CurrentAvailablePages);
        Assert.Equal(7,after.Cycles[0].ClosingLedgerBalance);
        Assert.Single((await Documents(grant))!.Documents);
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_reversal_keeps_boundary_and_is_recorded_in_later_cycle()
    {
        SqlTestGuard.Require();
        var first = await Allowance(100, at:Epoch);
        await ChargeAt(3, Epoch, Epoch.AddDays(1));
        var second = await Allowance(20, at:Epoch.AddDays(2));
        await Allowance(-100,"Reversal",first,Epoch.AddDays(3));
        await Allowance(-30,"Adjustment",at:Epoch.AddDays(4));
        var report = (await Cycles())!;
        Assert.True(report.Cycles[1].IsReversed);
        Assert.Equal(100,report.Cycles[1].TopUpPages);
        Assert.Equal(100,report.Cycles[1].RecordedAvailableAfterTopUp);
        Assert.Equal(0,report.Cycles[1].ReversalPages);
        Assert.Equal(-100,report.Cycles[0].ReversalPages);
        Assert.Equal(-13,report.Totals.LedgerBalance);
        Assert.Equal(117,report.Cycles[0].OpeningLedgerBalance);
        Assert.Equal(second,report.Cycles[0].TopUpGrantId);
        Reconcile(report);
        var filtered = (await Cycles(first))!;
        Assert.Single(filtered.Cycles); Assert.Null(filtered.Prelude);
        Assert.Equal(report.Totals,filtered.Totals);
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_empty_and_unlimited_history_use_prelude()
    {
        SqlTestGuard.Require();
        var empty = (await Cycles())!;
        Assert.Empty(empty.Cycles); Assert.Null(empty.Prelude!.StartAtUtc); Assert.Null(empty.Prelude.EndAtUtc);
        Reconcile(empty);
        await Allowance(5,"Adjustment",at:Epoch);
        await ChargeAt(8,Epoch,Epoch.AddDays(1));
        var report = (await Cycles())!;
        Assert.Empty(report.Cycles); Assert.Equal(-3,report.Prelude!.ClosingLedgerBalance);
        Assert.Equal(8,report.Prelude.ChargedPages); Assert.Equal(1,report.Prelude.ChargedRuns);
        Assert.Single((await Documents())!.Documents);
        Assert.Empty((await Cycles(0))!.Cycles);
        Assert.Null(await Cycles(999)); Assert.Null(await Documents(999));
        Reconcile(report);
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_ties_grants_before_charges_and_grant_id_order()
    {
        SqlTestGuard.Require();
        await ChargeAt(2,Epoch,Epoch);
        var first = await Allowance(10,at:Epoch);
        await Allowance(3,"Adjustment",at:Epoch);
        var last = await Allowance(20,at:Epoch);
        await Allowance(-10,"Reversal",first,Epoch);
        var report = (await Cycles())!;
        Assert.Equal(0,report.Prelude!.ChargedPages);
        Assert.Equal(3,report.Cycles[1].AdjustmentPages);
        Assert.Equal(10,report.Cycles[1].OpeningLedgerBalance);
        Assert.Equal(33,report.Cycles[0].OpeningLedgerBalance);
        Assert.Equal(-10,report.Cycles[0].ReversalPages);
        Assert.Equal(2,report.Cycles[0].ChargedPages);
        Assert.Empty((await Documents(first))!.Documents);
        Assert.Single((await Documents(last))!.Documents);
        Reconcile(report);
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_documents_page_after_assignment_and_use_run_metadata()
    {
        SqlTestGuard.Require();
        await ChargeAt(1,Epoch,Epoch);
        var prelude = (await Documents())!.Documents.Single().ChargeId;
        var first = await Allowance(100,at:Epoch.AddDays(1));
        for (var i=0;i<12;i++) await ChargeAt(1,Epoch,Epoch.AddDays(2));
        var second = await Allowance(100,at:Epoch.AddDays(3));
        await db.Execute("UPDATE dbo.JobRuns SET ResolvedDocumentTypeCode=NULL; UPDATE dbo.Jobs SET ResolvedDocumentTypeCode='INV';");
        var all = (await Documents(first))!.Documents;
        Assert.Equal(12,all.Count);
        Assert.All(all,x=> { Assert.Null(x.ResolvedDocumentTypeCode); Assert.Equal("New",x.RunReason); Assert.Equal(TimeSpan.Zero,x.ChargedAtUtc.Offset); });
        List<ChargedRun> paged=[]; long? after=null;
        do {
            var page=(await Documents(first,5,after))!; paged.AddRange(page.Documents); after=page.NextAfterChargeId;
        } while (after is not null);
        Assert.Equal(all,paged); Assert.Equal(12,paged.Select(x=>x.ChargeId).Distinct().Count());
        Assert.Empty((await Documents(first,5,all[^1].ChargeId))!.Documents);
        Assert.Null(await Documents(first,5,prelude));
        Assert.Null(await Documents(second,5,all[0].ChargeId));
        Assert.Null(await Documents(first,5,long.MaxValue));
        // >2033 characters must still be one scalar result cell, parseable by Dapper and sqlcmd.
        var json = await db.Scalar<string>(new SqlStatementCatalog()["S28"],new {consumerId=(short)1,grantId=first,limit=100,afterChargeId=(long?)null});
        Assert.True(json.Length>2033);
        Assert.Equal(12,System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("documents").GetArrayLength());
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_reprocessed_job_counts_two_charged_runs()
    {
        SqlTestGuard.Require();
        var grant = await Allowance(20);
        var request=SqlDatabase.Request();
        var job=await Claim(request); await Extract(job); await Complete(job);
        await db.Store.IntakeAsync(request with {ClientRequestId="reprocess",Reprocess=true},default);
        var next=(await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync)); await Extract(next); await Complete(next);
        var report=(await Cycles())!;
        Assert.Equal(2,report.Totals.ChargedRuns); Assert.Equal(4,report.Totals.ChargedPages);
        var docs=(await Documents(grant))!.Documents;
        Assert.Equal(new short[] {1,2},docs.Select(x=>x.RunNo));
        Assert.Equal(new[] {"New","Reprocess"},docs.Select(x=>x.RunReason));
        Assert.Single(docs.Select(x=>x.JobId).Distinct());
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_consumer_isolation_and_foreign_cursors()
    {
        SqlTestGuard.Require();
        var grant=await Allowance(10,at:Epoch);
        await ChargeAt(1,Epoch,Epoch);
        var charge=(await Documents(grant))!.Documents.Single().ChargeId;
        await db.Execute("INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1) VALUES('OTHER','Other','FWD','OTHER',CONVERT(binary(32),2));");
        var report=(await Cycles(consumer:2))!;
        Assert.Empty(report.Cycles); Assert.Equal(0,report.Totals.ChargedRuns);
        Assert.Null(await Cycles(grant,2)); Assert.Null(await Documents(grant,consumer:2));
        Assert.Null(await Documents(0,after:charge,consumer:2));
        Assert.Null(await Cycles(consumer:30000));
    }
    [Test, Category("Sql")]
    public async Task Topup_reporting_caps_cycles_but_totals_and_single_lookup_cover_all_history()
    {
        SqlTestGuard.Require();
        long first=0;
        for (var i=0;i<205;i++) {
            var id=await Allowance(1,at:Epoch.AddSeconds(i)); if(i==0)first=id;
        }
        var report=(await Cycles())!;
        Assert.Equal(200,report.Cycles.Count); Assert.True(report.HasMoreCycles);
        Assert.Equal(205,report.Totals.GrantedPages);
        Assert.Single((await Cycles(first))!.Cycles);
        Assert.False((await Cycles(first))!.HasMoreCycles);
        Assert.Null(await Cycles(await Allowance(1,"Adjustment")));
    }
    [Test, Category("Sql")]
    public async Task Topup_reversal_guard_direct_insert_replay_and_non_topup()
    {
        SqlTestGuard.Require();
        var original=await Allowance(10);
        var adjustment=await Allowance(3,"Adjustment");
        Assert.Equal(50017,(await Assert.ThrowsAsync<SqlException>(()=>Allowance(-3,"Reversal",adjustment))).Number);
        var id=Guid.NewGuid(); var reversal=await Allowance(-10,"Reversal",original,request:id);
        Assert.Equal(reversal,await Allowance(-10,"Reversal",original,request:id));
        Assert.Equal(50017,(await Assert.ThrowsAsync<SqlException>(()=>Allowance(-10,"Reversal",original))).Number);
        Assert.Equal(50017,(await Assert.ThrowsAsync<SqlException>(()=>Allowance(10,"Reversal",reversal))).Number);
        Assert.Equal(1,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageAllowanceGrants WHERE Kind='Reversal'"));
    }
    [Test, Category("Sql")]
    public async Task Topup_reversal_concurrency_allows_only_one_new_request()
    {
        SqlTestGuard.Require();
        var original=await Allowance(10);
        async Task<bool> Reverse() {try {await Allowance(-10,"Reversal",original);return true;}catch(SqlException){return false;}}
        Assert.Equal(1,(await Task.WhenAll(Reverse(),Reverse())).Count(x=>x));
    }
    [Test, Category("Sql")]
    public async Task Topup_upgrade_is_rerunnable_and_reinstalls_final_trigger_and_indexes()
    {
        SqlTestGuard.Require();
        await db.DisposeAsync();
        db = new SqlDatabase { UseSchemaScript = true };
        await db.InitializeAsync();
        var upgrade=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"07_topup_reporting_upgrade.sql"));
        const string meta="""
            SELECT (SELECT name,is_unique,filter_definition FROM sys.indexes WHERE name IN ('UX_Allowance_Reversal','IX_PageCharges_Consumer') ORDER BY name FOR JSON PATH) indexes
            FOR JSON PATH
            """;
        var before=await db.Scalar<string>(meta);
        var trigger=(await db.Scalar<string>("SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.TR_Allowance_Insert'))")).Trim();
        await db.Execute(upgrade); await db.Execute(upgrade);
        Assert.Equal(before,await db.Scalar<string>(meta));
        Assert.Equal(trigger,(await db.Scalar<string>("SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.TR_Allowance_Insert'))")).Trim());
        await Topup_reversal_guard_direct_insert_replay_and_non_topup();
    }
    [Test, Category("Sql")]
    public async Task Topup_upgrade_prechecks_duplicate_reversals_without_deleting_data()
    {
        SqlTestGuard.Require();
        await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"06_page_quota_upgrade.sql")));
        await db.Execute("DROP INDEX UX_Allowance_Reversal ON dbo.PageAllowanceGrants;");
        var original=await Allowance(10);
        var one=await Allowance(-10,"Reversal",original); var two=await Allowance(-10,"Reversal",original);
        var upgrade=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"07_topup_reporting_upgrade.sql"));
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.Execute(upgrade));
        Assert.Equal(50019,error.Number); Assert.Contains($"{one},{two}",error.Message);
        Assert.Equal(2,await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageAllowanceGrants WHERE Kind='Reversal'"));
    }
    [Test, Category("Sql")]
    public async Task Database_owner_can_read_both_reports_after_legacy_upgrade()
    {
        SqlTestGuard.Require();
        await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"06_page_quota_upgrade.sql")));
        await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"07_topup_reporting_upgrade.sql")));
        await db.Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"08_operator_cost_report_upgrade.sql")));
        foreach(var label in new[] {"S27", "S28"})
        {
            var batch="SET NOCOUNT ON; DECLARE @consumerId smallint=1, @grantId bigint="+(label=="S27"?"NULL":"0")+", @limit int=100, @afterChargeId bigint=NULL;\n"+new SqlStatementCatalog()[label];
            var text=await db.Scalar<string>(batch);
            PortAssert.StartsWith("{",text);
        }
    }
}
