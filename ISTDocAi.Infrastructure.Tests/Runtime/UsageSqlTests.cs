using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed partial class PageQuotaSqlTests
{
    private Task<ISTDocAi.Application.Common.Contracts.DocAi.V1.UsageResponse> Usage(int months = 6, DateTimeOffset? at = null, short consumer = 1) =>
        db.Store.GetUsageAsync(consumer, months, at ?? DateTimeOffset.UtcNow, default);

    private async Task ChargeAt(int pages, DateTimeOffset submitted, DateTimeOffset charged)
    {
        var job = await Claim(SqlDatabase.Request() with { PageCount = checked((short)pages) });
        var op = await db.Accepted(job);
        await db.Execute("""
            UPDATE dbo.JobRuns SET CreatedAtUtc=@submitted WHERE JobId=@JobId;
            UPDATE dbo.AzureOperations SET State='Succeeded',Pages=@pages,CompletedAtUtc=@charged WHERE OperationId=@op;
            INSERT dbo.UsageLedger(ConsumerId,JobId,RunNo,OperationId,MeterCode,ModelId,Pages,OccurredAtUtc,CorrelationId)
            VALUES(1,@JobId,1,@op,'DI_CUSTOM_EXTRACT','bl-v1',@pages,@charged,'test');
            INSERT dbo.CustomerPageCharges(JobId,RunNo,ConsumerId,Pages,UsageId,OperationId,RecordedAtUtc)
            VALUES(@JobId,1,1,@pages,SCOPE_IDENTITY(),@op,@charged);
            UPDATE dbo.PageReservations SET State='Charged',SettledAtUtc=@charged,OperationId=@op WHERE JobId=@JobId;
            """, Times(new { job.JobId, op, pages }, submitted, charged));
    }
    // Dapper sends DateTime as legacy datetime (3.33 ms), which rounds 23:59:59.999 into the next day.
    private static Dapper.DynamicParameters Times(object values, DateTimeOffset submitted, DateTimeOffset charged)
    {
        var p = new Dapper.DynamicParameters(values);
        p.Add("submitted", submitted.UtcDateTime, System.Data.DbType.DateTime2);
        p.Add("charged", charged.UtcDateTime, System.Data.DbType.DateTime2);
        return p;
    }

    [Test, Category("Sql")]
    public async Task Usage_totals_latest_topup_cycle_and_consumer_isolation()
    {
        SqlTestGuard.Require();
        await Prepaid(); await Grant(100);
        await ChargeAt(80, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await Grant(100);
        await db.Execute("""
            INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId)
            VALUES(1,10,'Adjustment','private operator','private note',NEWID());
            INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId)
            VALUES(1,-10,'Adjustment','private operator','private note',NEWID());
            INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1)
            VALUES('SECOND','Second','FWD','SECOND',CONVERT(binary(32),2));
            INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId)
            VALUES(2,999,'TopUp','private operator','private note',NEWID());
            """);
        await db.Store.IntakeAsync(SqlDatabase.Request() with { PageCount=3 }, default);
        var usage = await Usage();
        Assert.Equal(200, usage.Granted); Assert.Equal(80, usage.Used);
        Assert.Equal(3, usage.Reserved); Assert.Equal(117, usage.Remaining);
        Assert.Equal(120, usage.CycleBase); Assert.Equal(12, usage.WarningAtPages);
        Assert.Equal(100, usage.LastTopUp!.Pages); Assert.Equal("Ok", usage.Status);
        Assert.Equal(999, (await Usage(consumer:2)).Remaining);
    }

    [Test, Category("Sql")]
    public async Task Usage_zero_fills_year_boundary_and_counts_UTC_days()
    {
        SqlTestGuard.Require();
        var midnight = new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);
        await ChargeAt(2, midnight.AddMilliseconds(-1), midnight.AddMilliseconds(-1));
        await ChargeAt(3, midnight, midnight);
        await ChargeAt(4, midnight.AddDays(1), midnight.AddDays(1));
        var usage = await Usage(3, midnight.AddHours(12));
        Assert.Equal(new[] { "2025-11", "2025-12", "2026-01" }, usage.Months.Select(x=>x.Month));
        Assert.Equal(new long[] { 0,2,7 }, usage.Months.Select(x=>x.ChargedPages));
        Assert.Equal(new long[] { 0,1,2 }, usage.Months.Select(x=>x.ChargedDocuments));
        Assert.Equal(3, usage.SubmittedTodayPages); Assert.Equal(3, usage.ChargedTodayPages);
        Assert.Equal("UTC", usage.DayBoundary); Assert.Equal(midnight.AddHours(12), usage.AsOfUtc);
        Assert.Equal("Unlimited", usage.Status);
    }

    [Test, Category("Sql")]
    public async Task Usage_status_changes_as_document_pages_are_charged()
    {
        SqlTestGuard.Require();
        await Prepaid();
        Assert.Equal("Exhausted", (await Usage()).Status);
        Assert.Null((await Usage()).CycleBase);
        await Grant(10);
        Assert.Equal("Ok", (await Usage()).Status);
        await ChargeAt(9, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var low = await Usage();
        Assert.Equal("Low", low.Status); Assert.Equal(1, low.Remaining);
        await ChargeAt(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.Equal("Exhausted", (await Usage()).Status);
    }
}
