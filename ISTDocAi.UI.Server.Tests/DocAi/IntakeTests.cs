using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;



using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Writer;
namespace ISTDocAi.UI.Server.Tests.DocAi;

internal class ApiFactory : ISTDocAi.UI.Server.Tests.BackendFactory
{
    public ApiFactory() : base(false) { }
    public IDatabaseReadiness TestDatabaseGate { get; set; } = ReadyGate();
    private static DatabaseReadiness ReadyGate() { var gate = new DatabaseReadiness(); gate.MarkReady(); return gate; }
    public CapturedSerilogSink Serilog { get; } = new();
    public CapturedPayloadSerializer Serializer { get; } = new();
    public CapturedRequestLogs Logs { get; } = new();
    public FakePorts Ports { get; } = new();
    public IJobRepository? Repository { get; set; }
    public int Tokens { get; set; } = 100;
    public ManualClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["DocAi:Mapping:Source"] = "Sql",
            ["AzurePriceRefresh:Enabled"] = "false", ["Signals:Enabled"] = "false", ["Retention:Enabled"] = "false", ["Worker:Enabled"] = "false", ["Webhooks:Enabled"] = "false", ["DocAi:KeyVault:Url"] = "https://docai-test.vault.azure.net", ["DocAi:RateLimit:TokenLimit"] = Tokens.ToString(), ["DocAi:RateLimit:TokensPerPeriod"] = "1", ["DocAi:RateLimit:PeriodSeconds"] = "3600" }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDatabaseReadiness>();
            services.AddSingleton<IDatabaseReadiness>(TestDatabaseGate);
            services.RemoveAll<IHostedService>();
            services.Configure<Serilog.AspNetCore.RequestLoggingOptions>(o =>
                o.Logger = new global::Serilog.LoggerConfiguration().WriteTo.Sink(Serilog).CreateLogger());
            services.AddSingleton<ISTDocAi.Infrastructure.Data.Sql.DocAi.ISqlConnectionFactory>(new DenySql());
            services.AddSingleton<IHttpClientFactory>(new DenyHttp());
            services.AddSingleton<ISTDocAi.Application.Common.Services.IRequestLogService>(Logs);
            services.AddSingleton<ISTDocAi.Application.Common.Interfaces.Serialization.ILogPayloadSerializer>(Serializer);
            services.AddSingleton<ISTDocAi.Application.Common.Interfaces.IUnitOfWork>(new ForbiddenUnitOfWork());
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IUsageReader>(); services.AddSingleton<IUsageReader>(Ports);
            services.RemoveAll<IConsumerSource>(); services.AddSingleton<IConsumerSource>(Ports);
            services.RemoveAll<IModelCatalog>(); services.AddSingleton<IModelCatalog>(Ports);
            services.RemoveAll<IJobRepository>(); services.AddSingleton<IJobRepository>(Repository ?? Ports);
            services.RemoveAll<IDocumentStore>(); services.AddSingleton<IDocumentStore>(Ports);
        });
    }
    public HttpClient Client(string? key = "test-key-one")
    {
        var client = CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }
}
public sealed class FakePorts : IConsumerSource, IModelCatalog, IJobRepository, IDocumentStore, IUsageReader
{
    public short? UsageConsumerId { get; private set; }
    public Task<UsageResponse> GetUsageAsync(short consumerId, int months, DateTimeOffset asOfUtc, CancellationToken ct)
    {
        UsageConsumerId = consumerId;
        return Task.FromResult(new UsageResponse(asOfUtc, "pages", "Prepaid", "Low", consumerId * 100, 90, 0, 10, 10, 100,
            10, new UsageTopUp(asOfUtc, 100), 10, 100, 2, 2, "UTC",
            Enumerable.Range(0, months).Select(i => new UsageMonth(asOfUtc.AddMonths(i-months+1).ToString("yyyy-MM"), 0, 0)).ToArray()));
    }
    public long? TopUpGrantId { get; private set; }
    public int DocumentLimit { get; private set; }
    public long? DocumentAfter { get; private set; }
    public Task<TopUpCyclesResponse?> GetTopUpCyclesAsync(short consumerId, long? grantId, CancellationToken ct)
    {
        UsageConsumerId = consumerId; TopUpGrantId = grantId;
        TopUpCycle cycle = new(consumerId, null, null, null, 100, false, 100, 100, 0, 0, 0, 0, 100);
        return Task.FromResult<TopUpCyclesResponse?>(grantId is > 0 && grantId != consumerId ? null :
            new("TimestampGrantBeforeChargeV1", "RecordedCustomerCharges", new(100, 0, 0, 100, 0, 100),
                grantId is null or 0 ? cycle with { TopUpGrantId = null } : null, grantId == 0 ? [] : [cycle], false));
    }
    public Task<CycleDocumentsResponse?> GetCycleDocumentsAsync(short consumerId, long grantId, int limit, long? afterChargeId, CancellationToken ct)
    {
        UsageConsumerId = consumerId; TopUpGrantId = grantId; DocumentLimit = limit; DocumentAfter = afterChargeId;
        return Task.FromResult<CycleDocumentsResponse?>(grantId > 0 && grantId != consumerId || afterChargeId > 100 ? null : new(grantId == 0 ? null : grantId, [], null));
    }
    public Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct) => throw new NotSupportedException();
    public Consumer Consumer { get; set; } = new(1, "TEST", "TENANT", SHA256.HashData(Encoding.UTF8.GetBytes("test-key-one")), SHA256.HashData(Encoding.UTF8.GetBytes("test-key-two")), 10, 100, false);
    public List<Consumer> OtherConsumers { get; } = [];
    public List<CatalogModel> Models { get; } = [new("BL", null, "extract", "bl-v1", .8m), new("BL", "LINE", "extract", "bl-line", .8m)];
    public List<IntakeRequest> Requests { get; } = [];
    public int ConsumerReads { get; private set; }
    public int CatalogReads { get; private set; }
    public short RunConsumerId { get; set; } = 1;
    public int Uploads { get; private set; }
    public int PagesToday { get; set; }
    public IntakeOutcome FirstOutcome { get; set; } = IntakeOutcome.NeedsUpload;
    public IntakeOutcome SecondOutcome { get; set; } = IntakeOutcome.Created;
    public Guid JobId { get; } = Guid.NewGuid();
    public RunOutcome? Run { get; set; }
    public string RunReason { get; set; } = "New";
    public string? ChangesJson { get; set; }
    public IntakeException? IntakeError { get; set; }
    public string? RereadSkipped { get; set; }
    public bool Throw { get; set; }
    public bool ThrowCatalog { get; set; }
    public string? UploadedContentType { get; private set; }
    public string? UploadedPath { get; private set; }
    public List<string> Calls { get; } = [];
    public Task<IReadOnlyList<Consumer>> GetConsumersAsync(CancellationToken ct) { ConsumerReads++; return Task.FromResult<IReadOnlyList<Consumer>>([Consumer, .. OtherConsumers]); }
    public Task<IReadOnlyList<CatalogModel>> GetCatalogAsync(CancellationToken ct)
    {
        CatalogReads++;
        if (ThrowCatalog) throw new InvalidOperationException("internal exception text must be hidden");
        return Task.FromResult<IReadOnlyList<CatalogModel>>(Models.ToArray());
    }
    public Task<int> DailyPagesAsync(short consumerId, CancellationToken ct) { Calls.Add("cap"); return Task.FromResult(PagesToday); }
    public Task<IntakeResult> IntakeAsync(IntakeRequest request, CancellationToken ct)
    {
        if (IntakeError is not null) throw IntakeError;
        if (Throw) throw new InvalidOperationException("internal exception text must be hidden");
        Requests.Add(request); Calls.Add(request.BlobPath is null ? "probe" : "commit");
        var outcome = request.BlobPath is null ? FirstOutcome : SecondOutcome;
        return Task.FromResult(new IntakeResult(outcome == IntakeOutcome.NeedsUpload ? null : JobId, outcome == IntakeOutcome.NeedsUpload ? null : (short)1, outcome, outcome == IntakeOutcome.NeedsUpload ? null : "Queued") { RunReason = RunReason, ChangesJson = ChangesJson, RereadSkipped = RereadSkipped });
    }
    public int RunReads { get; private set; }
    public Task<RunOutcome?> GetRunAsync(Guid jobId, short consumerId, short? runNo, CancellationToken ct) { RunReads++; return Task.FromResult(Run?.JobId == jobId && consumerId == RunConsumerId && (runNo is null || runNo == Run.RunNo) ? Run : null); }
    public async Task PutAsync(string path, Stream content, string contentType, CancellationToken ct) { Calls.Add("upload"); Uploads++; UploadedPath = path; UploadedContentType = contentType; await content.CopyToAsync(Stream.Null, ct); }
    public Task CheckAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ActivateAsync(string id, decimal accuracy, CancellationToken ct) => throw new NotSupportedException();
    public Task<RunPosition?> GetPositionAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<WriteOutcome> CompleteAsync(Completion r, CancellationToken ct) => throw new NotSupportedException();
    public CancelJobResult? CancelResult { get; set; }
    public string? CancelRequestId { get; private set; }
    public Task<CancelJobResult?> CancelAsync(short consumerId, string requestId, CancellationToken ct)
    {
        CancelRequestId = requestId;
        return Task.FromResult(consumerId == RunConsumerId ? CancelResult : null);
    }
    public Task<WriteOutcome> FinishCancellationAsync(Lease lease, CancellationToken ct) => Task.FromResult(WriteOutcome.Applied);
    public Task<WriteOutcome> FailAsync(Failure r, CancellationToken ct) => throw new NotSupportedException();
    public Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess r, CancellationToken ct) => throw new NotSupportedException();
    public Task<ClassMapping?> GetClassMappingAsync(string id, string name, CancellationToken ct) => throw new NotSupportedException();
}
public sealed class IntakeTests
{
    public static byte[] Pdf(int pages = 1)
    {
        var builder = new PdfDocumentBuilder();
        for (int i = 0; i < pages; i++) builder.AddPage(100, 100);
        return builder.Build();
    }
    public static MultipartFormDataContent Form(byte[]? bytes = null, string? type = "BL", string? line = null, string? client = "request-1", string? reprocess = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes ?? Pdf()); file.Headers.ContentType = new("application/pdf");
        form.Add(file, "file", "display.pdf");
        if (type is not null) form.Add(new StringContent(type), "documentType");
        if (line is not null) form.Add(new StringContent(line), "shippingLine");
        if (client is not null) form.Add(new StringContent(client), "clientRequestId");
        if (reprocess is not null) form.Add(new StringContent(reprocess), "reprocess");
        return form;
    }
    internal static async Task<JsonElement> Error(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("IsSuccess").GetBoolean());
        Assert.Equal(code, json.GetProperty("Error").GetProperty("Code").GetString());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("Error").GetProperty("Message").GetString()));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), json.GetProperty("Extensions").GetProperty("CorrelationId").GetString());
        return json;
    }
     [TestCase(null)] [TestCase("unknown")]
    public async Task Missing_or_unknown_key_is_unauthorized(string? key)
    { using var app = new ApiFactory(); await Error(await app.Client(key).GetAsync("/api/v2/catalog"), 401, "UNAUTHORIZED"); }
    [Test] public async Task Second_slot_authenticates()
    { using var app = new ApiFactory(); Assert.Equal(HttpStatusCode.OK, (await app.Client("test-key-two").GetAsync("/api/v2/catalog")).StatusCode); }
     [TestCase(null,"ok",null)] [TestCase("BL",null,null)] [TestCase("BL","",null)] [TestCase("BL","ok","yes")]
    public async Task Invalid_fields(string? type, string? client, string? reprocess)
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs", Form(type:type, client:client, reprocess:reprocess)),400,"INVALID_REQUEST"); Assert.Empty(app.Ports.Requests); }
    [Test] public async Task Long_request_id_is_invalid()
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs",Form(client:new string('x',101))),400,"INVALID_REQUEST"); }
    [Test] public async Task Empty_file_is_invalid()
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs",Form([])),400,"INVALID_REQUEST"); }
    [Test] public async Task Oversize_stream_is_rejected()
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs",Form(new byte[21*1024*1024])),413,"FILE_TOO_LARGE"); Assert.Equal(0,app.Ports.Uploads); }
    [Test] public async Task Header_does_not_determine_file_type()
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs",Form("not a PDF"u8.ToArray())),415,"UNSUPPORTED_FILE_TYPE"); }
    [Test] public async Task Encrypted_pdf_is_rejected()
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs",Form(EncryptedPdf.Create())),422,"ENCRYPTED_PDF"); }
    [Test] public async Task Eleven_pages_is_rejected()
    { using var app = new ApiFactory(); await Error(await app.Client().PostAsync("/api/v2/jobs",Form(Pdf(11))),422,"TOO_MANY_PAGES"); }
     [TestCase("UNKNOWN",null)] [TestCase("BL","UNKNOWN")] [TestCase("LINEONLY",null)]
    public async Task Missing_catalog_route_is_rejected(string type,string? line)
    { using var app = new ApiFactory(); app.Ports.Models.Add(new("LINEONLY","LINE","extract","line",.8m)); await Error(await app.Client().PostAsync("/api/v2/jobs",Form(type:type,line:line)),422,"NOT_IN_CATALOG"); }
    [Test] public async Task Generic_model_accepts_empty_line_and_returns_contract()
    {
        using var app = new ApiFactory(); var bytes = Pdf(); var response = await app.Client().PostAsync("/api/v2/jobs",Form(bytes,line:""));
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
        var body = (await response.ReadValue<SubmitJobResponse>())!;
        Assert.Equal(app.Ports.JobId,body.JobId); Assert.Equal(1,body.RunNo); Assert.Equal("Queued",body.Status); Assert.Equal(1,body.PageCount);
        Assert.Equal(body.StatusUrl,response.Headers.Location!.ToString()); Assert.Equal("5",response.Headers.GetValues("Retry-After").Single());
        Assert.Equal(new[]{"probe","cap","upload","commit"},app.Ports.Calls);
        Assert.Null(app.Ports.Requests[0].BlobPath); Assert.Equal(app.Ports.UploadedPath,app.Ports.Requests[1].BlobPath);
        Assert.Equal("application/pdf",app.Ports.UploadedContentType); Assert.StartsWith("documents/TEST/",app.Ports.UploadedPath);
        Assert.All(app.Ports.Requests,r=>Assert.Equal(SHA256.HashData(bytes),r.FileSha256));
    }
    

    [Test] public async Task Existing_does_not_upload_or_check_cap()
    { using var app = new ApiFactory(); app.Ports.FirstOutcome=IntakeOutcome.Existing; app.Ports.PagesToday=1000; var response=await app.Client().PostAsync("/api/v2/jobs",Form()); Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.Equal(0,app.Ports.Uploads); Assert.Equal(new[]{"probe"},app.Ports.Calls); }
    [Test] public async Task Daily_cap_is_checked_before_upload()
    { using var app=new ApiFactory(); app.Ports.PagesToday=100; var response=await app.Client().PostAsync("/api/v2/jobs",Form()); await Error(response,429,"DAILY_CAP_REACHED"); Assert.Equal(43200,int.Parse(response.Headers.GetValues("Retry-After").Single())); Assert.Equal(0,app.Ports.Uploads); }
    [Test] public async Task Race_winner_returns_200_after_upload()
    { using var app=new ApiFactory(); app.Ports.SecondOutcome=IntakeOutcome.Existing; Assert.Equal(HttpStatusCode.OK,(await app.Client().PostAsync("/api/v2/jobs",Form())).StatusCode); Assert.Equal(1,app.Ports.Uploads); }
     [TestCase("good-123",true)] [TestCase("bad_id",false)] [TestCase("",false)]
    public async Task Correlation_is_validated(string id,bool valid)
    { using var app=new ApiFactory(); var client=app.Client(); client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-Id",id); var response=await client.GetAsync("/api/v2/catalog"); var actual=response.Headers.GetValues("X-Correlation-Id").Single(); if(valid) Assert.Equal(id,actual); else {Assert.NotEqual(id,actual);Assert.Matches("^[A-Za-z0-9-]{1,64}$",actual);} }
    [Test] public async Task Rate_limit_returns_retry_after()
    { using var app=new ApiFactory {Tokens=2}; var client=app.Client(); await client.GetAsync("/api/v2/catalog"); await client.GetAsync("/api/v2/catalog"); var response=await client.GetAsync("/api/v2/catalog"); await Error(response,429,"RATE_LIMITED"); Assert.True(int.Parse(response.Headers.GetValues("Retry-After").Single())>0); }
    
    
    
    
    [Test] public async Task Catalog_includes_generic_and_auto_detect()
    { using var app=new ApiFactory(); var body=(await app.Client().GetValue<CatalogResponse>("/api/v2/catalog"))!; Assert.Equal("TEST",body.ConsumerCode); Assert.Contains(body.Items,i=>i.DocumentTypeCode=="BL"&&i.ShippingLineCode is null&&i.MinFieldConfidence==.8); Assert.Single(body.AutoDetect); Assert.Equal("BL",body.AutoDetect[0].DocumentTypeCode); }
    [Test] public async Task Unhandled_error_hides_internal_details()
    { using var app=new ApiFactory(); app.Ports.Throw=true; var response=await app.Client().PostAsync("/api/v2/jobs",Form()); await Error(response,500,"INTERNAL_ERROR"); Assert.DoesNotContain("internal exception text",await response.Content.ReadAsStringAsync()); }
    [Test] public async Task Consumers_and_catalog_refresh_at_sixty_seconds()
    {
        using var app=new ApiFactory(); var client=app.Client();
        await client.GetAsync("/api/v2/catalog");
        app.Clock.Now=app.Clock.Now.AddSeconds(59);
        await client.GetAsync("/api/v2/catalog");
        Assert.Equal(1,app.Ports.ConsumerReads); Assert.Equal(1,app.Ports.CatalogReads);
        app.Clock.Now=app.Clock.Now.AddSeconds(1);
        await client.GetAsync("/api/v2/catalog");
        Assert.Equal(2,app.Ports.ConsumerReads); Assert.Equal(2,app.Ports.CatalogReads);
    }
    [Test] public async Task Revoked_key_disappears_after_cache_expiry()
    {
        using var app=new ApiFactory(); var client=app.Client();
        await client.GetAsync("/api/v2/catalog");
        app.Ports.Consumer=app.Ports.Consumer with { ApiKeyHash1 = SHA256.HashData("rotated-test-key"u8) };
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v2/catalog")).StatusCode);
        app.Clock.Now=app.Clock.Now.AddSeconds(60);
        await Error(await client.GetAsync("/api/v2/catalog"),401,"UNAUTHORIZED");
    }
    [Test] public async Task Classifier_allows_empty_line_but_is_not_a_catalog_item()
    {
        using var app=new ApiFactory(); app.Ports.Models.Clear(); app.Ports.Models.Add(new("BL",null,"classify","classifier",.8m));
        var client=app.Client(); Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsync("/api/v2/jobs",Form())).StatusCode);
        var catalog=(await client.GetValue<CatalogResponse>("/api/v2/catalog"))!;
        Assert.Empty(catalog.Items); Assert.Equal("BL",Assert.Single(catalog.AutoDetect).DocumentTypeCode);
    }
    
     
    
    [Test] public async Task Temp_file_is_deleted_on_success_and_exception()
    {
        using var app=new ApiFactory();
        using var scope=app.Services.CreateScope();
        var original=scope.ServiceProvider.GetRequiredService<IFileInspector>();
        // Captured by a decorator in a separate factory so HTTP cleanup is checked at its real boundary.
        using var captured=new CaptureFactory(original);
        var client=captured.Client();
        await client.PostAsync("/api/v2/jobs",Form());
        Assert.False(File.Exists(captured.Path));
        captured.Ports.Throw=true;
        await client.PostAsync("/api/v2/jobs",Form());
        Assert.False(File.Exists(captured.Path));
    }
    [Test] public async Task Readiness_returns_503_when_SQL_is_not_configured()
    { using var app=new ApiFactory(); var response=await app.Client(null).GetAsync("/health/ready"); Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode); Assert.True(response.Headers.Contains("X-Correlation-Id")); }
    [Test] public async Task Liveness_does_not_require_authentication_or_dependencies()
    { using var app=new ApiFactory(); Assert.Equal(HttpStatusCode.OK,(await app.Client(null).GetAsync("/health/live")).StatusCode); }    
    [TestCase("AZURE_THROTTLED")] [TestCase("AZURE_UNAVAILABLE")] [TestCase("MAPPING_FAILED")]
    [TestCase("STORAGE_UNAVAILABLE")] [TestCase("INTERNAL_ERROR")] [TestCase("AZURE_AUTH")]
    public async Task Active_job_diagnostics_are_never_exposed_by_the_API(string code)
    {
        using var app = new ApiFactory();
        foreach (var status in new[] { "Queued", "Processing" })
        {
            app.Ports.Run = new(app.Ports.JobId, 1, "request-1", status, code, "internal diagnostic message",
                DateTimeOffset.UtcNow, null, 1, null, null, null);
            var response = await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("Value").GetProperty("Error").ValueKind);
            Assert.DoesNotContain(code, json); Assert.DoesNotContain("internal diagnostic message", json);
        }
    }
    [Test] public async Task Another_consumers_job_is_not_found()
    { using var app=new ApiFactory(); app.Ports.RunConsumerId=2; app.Ports.Run=new(app.Ports.JobId,1,"foreign","Queued",null,null,DateTimeOffset.UtcNow,null,1,null,null,null); await Error(await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}"),404,"JOB_NOT_FOUND"); }
    [Test] public async Task Completed_result_is_deserialized_into_contract()
    {
        using var app=new ApiFactory();
        var result=new CanonicalResult { ContractVersion="1.0", DocumentTypeCode="BL", ShippingLineCode=null,ModelId="generic",PageCount=1,Fields=[],Containers=[],Flags=[] };
        app.Ports.Run=new(app.Ports.JobId,2,"req","Completed",null,null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,1,"1.0",JsonSerializer.Serialize(result,new JsonSerializerOptions(JsonSerializerDefaults.Web)),0);
        var response=await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=2");
        var body=(await response.ReadValue<JobResponse>())!;
        Assert.Equal("generic",body.Result!.ModelId); Assert.False(response.Headers.Contains("Retry-After"));
    }
     [TestCase("0")] [TestCase("bogus")] [TestCase("32768")]
    public async Task Invalid_run_number_is_invalid_request(string run)
    { using var app=new ApiFactory(); await Error(await app.Client().GetAsync($"/api/v2/jobs/{Guid.NewGuid()}?runNo={run}"),400,"INVALID_REQUEST"); }
    [Test] public async Task Nonexistent_run_is_not_found()
    { using var app=new ApiFactory(); await Error(await app.Client().GetAsync($"/api/v2/jobs/{Guid.NewGuid()}?runNo=2"),404,"JOB_NOT_FOUND"); }
    [Test]
    public async Task Post_and_get_expose_durable_model_change_metadata()
    {
        using var app = new ApiFactory();
        app.Ports.SecondOutcome = IntakeOutcome.Requeued;
        app.Ports.RunReason = "ModelChanged";
        app.Ports.ChangesJson = """{"extractModel":{"oldModelId":"v1","newModelId":"v2"},"mapping":true}""";
        var client = app.Client();
        var response = await client.PostAsync("/api/v2/jobs", Form());
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var submitted = (await response.ReadValue<SubmitJobResponse>())!;
        Assert.Equal("ModelChanged", submitted.RunReason);
        Assert.Equal(new ModelIdChange("v1", "v2"), submitted.Changes!.ExtractModel);
        Assert.True(submitted.Changes.Mapping == true);
        Assert.Null(submitted.Changes.Classifier);
        app.Ports.Run = new(app.Ports.JobId, 1, "request-1", "Queued", null, null, DateTimeOffset.UtcNow, null, 1, null, null, null)
            { RunReason = "ModelChanged", ChangesJson = app.Ports.ChangesJson };
        var current = (await client.GetValue<JobResponse>($"/api/v2/jobs/{app.Ports.JobId}"))!;
        Assert.Equal(submitted.RunReason, current.RunReason);
        Assert.Equal(submitted.Changes, current.Changes);
        Assert.DoesNotContain("fingerprint", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
    [Test] public async Task Queued_run_returns_retry_after()
    { using var app=new ApiFactory(); app.Ports.Run=new(app.Ports.JobId,1,"request-1","Queued",null,null,DateTimeOffset.UtcNow,null,1,null,null,null); var response=await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=1"); Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.Equal("5",response.Headers.GetValues("Retry-After").Single()); var body=(await response.ReadValue<JobResponse>())!; Assert.Equal("Queued",body.Status); Assert.Null(body.Result); }

}

public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026,10,25,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class CaptureFactory(IFileInspector inspector) : ApiFactory
{
    public string? Path { get; private set; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => { services.RemoveAll<IFileInspector>(); services.AddSingleton<IFileInspector>(new CaptureInspector(inspector,p=>Path=p)); });
    }
    private sealed class CaptureInspector(IFileInspector inner, Action<string> capture) : IFileInspector
    {
        public FileInspection Inspect(string path, CancellationToken ct) {capture(path);return inner.Inspect(path,ct);}
    }

}


