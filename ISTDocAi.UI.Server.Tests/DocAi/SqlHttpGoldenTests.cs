using System.Net.Http.Json;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using Microsoft.Extensions.Options;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests.DocAi;
[Category("SqlIntegration")]
internal sealed class SqlHttpGoldenTests
{
    [Test]
    [Category("Sql")]
    public async Task Sql_golden_sequence_preserves_catalog_intake_replay_reprocess_quota_cap_and_mapping_preflight()
    {
        var db = new SqlDatabase();
        try
        {
            await db.InitializeAsync();
            await db.Execute("""
                UPDATE dbo.Consumers SET ApiKeyHash1=HASHBYTES('SHA2_256','test-key-one'),ApiKeyHash2=NULL,QuotaMode='Unlimited',DailyPageCap=1000 WHERE ConsumerId=1;
                INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId,RecordedAtUtc)
                VALUES(1,100,'TopUp','baseline-operator','Fake baseline','44444444-4444-4444-8444-444444444444','2026-10-25T12:00:00');
                """);
            var bytes = File.ReadAllBytes(HttpGoldenTests.Fixture("baseline.pdf"));
            var second = File.ReadAllBytes(HttpGoldenTests.Fixture("baseline-second.pdf"));
            using var app = await Factory(db, db.Store);
            using var client = app.Client(); client.DefaultRequestHeaders.Add("X-Correlation-Id", "baseline-correlation-1");
            await HttpGoldenTests.AssertGolden("sql-catalog", await client.GetAsync("/api/v2/catalog"), true);
            var intake = await client.PostAsync("/api/v2/jobs", HttpGoldenTests.Form(bytes));
            await HttpGoldenTests.AssertGolden("sql-intake", intake, true);
            await HttpGoldenTests.AssertGolden("sql-replay", await client.PostAsync("/api/v2/jobs", HttpGoldenTests.Form(bytes)), true);
            Assert.That(app.Ports.Uploads, Is.EqualTo(1));
            var job = (await SqlClaimTestHelper.ExpectedAsync(db.Store.ClaimAsync));
            var operation = await db.Accepted(job);
            await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 1, DateTimeOffset.UtcNow, "raw.json"), default);
            var canonical = new CanonicalResult { ContractVersion="1.0", DocumentTypeCode="BL", ShippingLineCode=null, ModelId="bl-v1", PageCount=1,
                Fields=new() { ["shipperName"] = new() { Value="PRIVATE_FIELD_VALUE", Confidence=.85, Page=1 } }, Containers=[], Flags=[new("shipperName", "LOW_CONFIDENCE")] };
            await db.Store.CompleteAsync(new(job.JobId, job.LeaseToken, CanonicalResultCodec.Serialize(canonical), 1, Guid.NewGuid(), "{}"), default);
            foreach (var (name, path) in new[] { ("sql-usage", "/api/v2/usage?months=3"), ("sql-topup-cycles", "/api/v2/usage/topups"),
                ("sql-topup-cycle", "/api/v2/usage/topups/1"), ("sql-cycle-documents-paging", "/api/v2/usage/topups/1/documents?limit=1") })
                await HttpGoldenTests.AssertGolden(name, await client.GetAsync(path), true);
            await HttpGoldenTests.AssertGolden("sql-reprocess", await client.PostAsync("/api/v2/jobs", HttpGoldenTests.Form(bytes, reprocess:"true", requestId:"baseline-reprocess-1")), true);
            foreach (var (name, suffix) in new[] { ("sql-job-owner", ""), ("sql-job-specific-run", "?runNo=1"), ("sql-job-not-found", "") })
            {
                var id = name == "sql-job-not-found" ? HttpGoldenTests.FixedJob : job.JobId;
                using var read = await client.GetAsync($"/api/v2/jobs/{id}{suffix}");
                await HttpGoldenTests.AssertGolden(name, read, true);
                if (read.IsSuccessStatusCode)
                {
                    var b = await new ISTDocAi.Application.Services.DocAi.Intake.GetJob(db.Store).ExecuteAsync(id, app.Ports.Consumer, suffix.Length == 0 ? null : (short)1, default);
                    using var old = JsonDocument.Parse(JsonSerializer.Serialize(b, ContractJsonContext.Default.JobResponse));
                    var actual = JsonSerializer.Deserialize<JsonElement>(await read.Content.ReadAsStringAsync());
                    Assert.That(actual.GetProperty("Value").GetProperty("Result").GetRawText(), Is.EqualTo(old.RootElement.GetProperty("result").GetRawText()));
                }
            }
            Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"), Is.EqualTo(2));
            Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"), Is.EqualTo(1));
            var options = Options.Create(new DocAiSqlOptions { ConnectionString = db.ConnectionString });
            var connection = new SqlConnectionFactory(options);
            var sqlStore = new SqlDocAiStore(connection, new SqlStatementCatalog(), options,
                mappingResolver: new SqlMappingProfileResolver(connection, options, app.Clock));
            using (var mapping = await Factory(db, sqlStore))
            using (var mappingClient = mapping.Client())
            {
                mappingClient.DefaultRequestHeaders.Add("X-Correlation-Id", "baseline-correlation-1");
                await HttpGoldenTests.AssertGolden("sql-mapping-unavailable", await mappingClient.PostAsync("/api/v2/jobs", HttpGoldenTests.Form(bytes)), true);
                Assert.That(mapping.Ports.Uploads, Is.Zero);
            }
            await db.Execute("""
                UPDATE dbo.Consumers SET QuotaMode='Prepaid' WHERE ConsumerId=1;
                INSERT dbo.PageAllowanceGrants(ConsumerId,Pages,Kind,OperatorIdentity,Note,RequestId,RecordedAtUtc)
                SELECT 1,-AvailablePages,'Adjustment','baseline-operator','Fake quota exhaustion',
                '55555555-5555-4555-8555-555555555555','2026-10-25T12:00:01' FROM dbo.ConsumerPageBalance WHERE ConsumerId=1 AND AvailablePages>0;
                """);
            await HttpGoldenTests.AssertGolden("sql-quota", await client.PostAsync("/api/v2/jobs", HttpGoldenTests.Form(second,requestId:"baseline-quota-1")), true);
            await db.Execute("UPDATE dbo.Consumers SET DailyPageCap=1 WHERE ConsumerId=1");
            using var cap = await Factory(db, db.Store);
            using var capClient = cap.Client(); capClient.DefaultRequestHeaders.Add("X-Correlation-Id", "baseline-correlation-1");
            await HttpGoldenTests.AssertGolden("sql-daily-cap", await capClient.PostAsync("/api/v2/jobs", HttpGoldenTests.Form(second,requestId:"baseline-cap-1")), true);
            Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns"), Is.EqualTo(2));
        }
        finally { await db.DisposeAsync(); }
    }
    private static async Task<ApiFactory> Factory(SqlDatabase db, IJobRepository repository)
    {
        var factory = new SqlReadFactory(db.Store) { Repository = repository };
        factory.Ports.Consumer = (await db.Store.GetConsumersAsync(default)).Single();
        factory.Ports.Models.Clear(); factory.Ports.Models.AddRange(await db.Store.GetCatalogAsync(default));
        return factory;
    }
    private sealed class SqlReadFactory(IUsageReader reader) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => { services.RemoveAll<IUsageReader>(); services.AddSingleton(reader); });
        }
    }
}
