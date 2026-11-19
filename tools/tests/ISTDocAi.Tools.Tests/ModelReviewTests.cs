using Azure;
using Azure.Core;
using Dapper;
using ISTDocAi.OperatorTool;
using ISTDocAi.Tools.Tests.Provisioning;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using System.Net;
using System.Text.Json.Nodes;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class ModelReviewTests
{
    internal const string Endpoint = "https://fake.cognitiveservices.azure.com";
    internal sealed class Tokens : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) { Assert.That(requestContext.Scopes, Is.EqualTo(new[] { DocumentModelClient.Scope })); return new("secret-fake-token", DateTimeOffset.UtcNow.AddHours(1)); }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
    internal sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri!.Host, Is.EqualTo("fake.cognitiveservices.azure.com"));
            Assert.That(request.RequestUri.Query, Is.EqualTo("?api-version=2024-11-30"));
            Assert.That(request.Headers.Authorization!.Parameter, Is.EqualTo("secret-fake-token"));
            return Task.FromResult(response(request));
        }
    }
    internal sealed class Input(params string[] lines) : IOperatorConsole
    {
        private readonly Queue<string> input = new(lines);
        public bool IsInputRedirected { get; init; }
        public List<string> Output { get; } = [];
        public string? ReadLine() { Assert.That(IsInputRedirected, Is.False); return input.TryDequeue(out var line) ? line : null; }
        public ConsoleKeyInfo ReadKey() => throw new AssertionException("Unexpected prompt");
        public void Write(string text) => Output.Add(text);
        public void WriteLine(string text) => Output.Add(text);
    }
    internal static HttpResponseMessage Response(JsonObject model) => new(HttpStatusCode.OK) { Content = new StringContent(model.ToJsonString()) };
    internal static JsonObject Scalar(string type = "string") => new() { ["type"] = type };
    internal static JsonObject Fields() => new() { ["Name"] = Scalar(), ["Containers"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["Number"] = Scalar(), ["Seal"] = Scalar() } } }, ["Bills"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["Bill"] = Scalar() } } } };
    internal static JsonObject Model(JsonObject fields) => new() { ["modelId"] = "new", ["apiVersion"] = "2024-11-30", ["docTypes"] = new JsonObject { ["doc"] = new JsonObject { ["fieldSchema"] = fields } } };
    internal static JsonObject Snapshot() => new() { ["DocumentTypeCode"] = "MANIFEST", ["Model"] = new JsonObject { ["ModelId"] = "old" }, ["MappingProfile"] = new JsonObject { ["MappingShape"] = "ModelProfile", ["ContainersField"] = "Containers", ["ContainerNumberField"] = "Number", ["SealNumberField"] = "Seal", ["BillsOfLadingField"] = "Bills", ["MappingFields"] = new JsonArray(new JsonObject { ["MapKind"] = "Header", ["CanonicalKey"] = "name", ["AzureName"] = "Name" }, new JsonObject { ["MapKind"] = "Bill", ["CanonicalKey"] = "bill", ["AzureName"] = "Bill" }, new JsonObject { ["MapKind"] = "Container", ["CanonicalKey"] = "containerNumber", ["AzureName"] = "Number" }, new JsonObject { ["MapKind"] = "Container", ["CanonicalKey"] = "sealNumber", ["AzureName"] = "Seal" }) } };
    private static DraftResult Draft(JsonObject fields, Func<string, string[], string?>? choose = null) => ModelDraft.Extract(Snapshot(), "TOKEN", Fields(), fields, "new", "REF", choose ?? ((_, _) => null));
    [TestCase("http://fake.cognitiveservices.azure.com")][TestCase("https://evil.com")][TestCase("https://fake.cognitiveservices.azure.com/path")][TestCase("https://fake.cognitiveservices.azure.com/?q=x")][TestCase("https://user@fake.cognitiveservices.azure.com")][TestCase("https://fake.cognitiveservices.azure.com/#x")][TestCase("https://fake.cognitiveservices.azure.com.evil.com")]
    public void Endpoint_refused(string endpoint) => Assert.Throws<ArgumentException>(() => DocumentModelClient.ValidateEndpoint(endpoint));
    [TestCase(404, "Model not found")][TestCase(401, "Cognitive Services User")][TestCase(403, "Cognitive Services User")]
    public void Azure_refusal(int status, string message)
    {
        using var http = new HttpClient(new Handler(_ => new((HttpStatusCode)status)));
        Assert.That(Assert.ThrowsAsync<ArgumentException>(async () => await new DocumentModelClient(http, new Tokens(), Endpoint).Fetch("new", false))!.Message, Does.Contain(message));
    }
    [TestCase(true)][TestCase(false)]
    public void Unreachable(bool timeout)
    {
        using var http = new HttpClient(new Handler(_ => throw (timeout ? new TaskCanceledException() : new HttpRequestException("secret"))));
        Assert.That(Assert.ThrowsAsync<ActionNeededException>(async () => await new DocumentModelClient(http, new Tokens(), Endpoint).Fetch("new", false))!.Message, Is.EqualTo("Azure is unreachable; upgrade refused."));
    }
    [Test]
    public async Task Schema_selection_expiration_and_nested_fields()
    {
        var model = Model(Fields()); using var http = new HttpClient(new Handler(_ => Response(model))); var client = new DocumentModelClient(http, new Tokens(), Endpoint);
        var result = await client.Fetch("new", false); Assert.That(DocumentModelClient.Fields(result)["Containers"]!["items"]!["properties"]!["Number"]!["type"]!.ToString(), Is.EqualTo("string"));
        Assert.That(ModelDraft.Confidence(DocumentModelClient.Fields(result)["Name"]), Is.EqualTo("unavailable"));
        model["docTypes"]!["second"] = model["docTypes"]!["doc"]!.DeepClone();
        Assert.ThrowsAsync<ArgumentException>(async () => await client.Fetch("new", false));
        Assert.That((await client.Fetch("new", false, "second"))["selectedDocType"]!.ToString(), Is.EqualTo("second"));
        model["expirationDateTime"] = "2000-01-01T00:00:00Z";
        Assert.That(Assert.ThrowsAsync<ArgumentException>(async () => await client.Fetch("new", false, "second"))!.Message, Does.Contain("expired"));
    }
    [Test]
    public void Carry_keeps_exact_rows()
    {
        var result = Draft(Fields()); Assert.That(result.Draft, Is.Not.Null);
        Assert.That(JsonNode.DeepEquals(result.Draft!["Rows"], Snapshot()["MappingProfile"]!["MappingFields"]), Is.True);
        Assert.That(result.Review["Items"]!.AsArray().All(r => r!["Status"]!.ToString() == "carried"), Is.True);
    }
    [TestCase("ignore")][TestCase("newName")][TestCase(null)]
    public void New_field_requires_explicit_choice(string? answer)
    {
        var fields = Fields(); fields["Added"] = Scalar(); var result = Draft(fields, (_, _) => answer);
        Assert.That(result.Draft is null, Is.EqualTo(answer is null));
        Assert.That(result.Review.ToJsonString(), Does.Contain(answer == "ignore" ? "ignored" : "new"));
    }
    [Test]
    public void Removed_and_type_changed_are_never_renamed()
    {
        var fields = Fields(); fields.Remove("Name"); Assert.That(Draft(fields).Review.ToJsonString(), Does.Contain("removed"));
        fields["Name"] = Scalar("number"); var changed = Draft(fields); Assert.That(changed.Draft, Is.Null); Assert.That(changed.Review.ToJsonString(), Does.Contain("Type changed").And.Contain("new"));
    }
    [TestCase("Containers")][TestCase("Bills")]
    public void Missing_selector_blocks(string name) { var fields = Fields(); fields.Remove(name); var result = Draft(fields, (_, _) => "ignore"); Assert.That(result.Draft, Is.Null); Assert.That(result.Review.ToJsonString(), Does.Contain("blocked")); }
    [Test]
    public void Container_override_is_structural_and_literal_names_are_preserved()
    {
        var fields = Fields(); fields["Containers"]!["items"]!["properties"]!.AsObject().Remove("Number");
        Assert.That(Draft(fields).Review.ToJsonString(), Does.Contain("Expected scalar"));
        fields = Fields(); fields["literal.dot"] = Scalar(); var result = Draft(fields, (_, _) => "literal"); Assert.That(result.Draft!["Rows"]!.ToJsonString(), Does.Contain("literal.dot"));
    }
    [Test]
    public void Ignored_changed_override_cannot_expose_a_missing_fallback_selector()
    {
        var snapshot = Snapshot(); snapshot["MappingProfile"]!["ContainerNumberField"] = "MissingFallback";
        var fields = Fields(); fields["Containers"]!["items"]!["properties"]!["Number"] = Scalar("number");
        var result = ModelDraft.Extract(snapshot, "TOKEN", Fields(), fields, "new", "REF", (_, _) => "ignore");
        Assert.That(result.Draft, Is.Null); Assert.That(result.Review.ToJsonString(), Does.Contain("Final effective selector"));
    }
    [Test]
    public void Class_changes_preserve_shipping_line()
    {
        var snapshot = new JsonObject { ["Model"] = new JsonObject { ["ModelId"] = "old" }, ["ClassifierClasses"] = new JsonArray(new JsonObject { ["ClassName"] = "bill", ["TargetDocumentTypeCode"] = "BL", ["ShippingLineCode"] = "LINE" }, new JsonObject { ["ClassName"] = "manifest", ["TargetDocumentTypeCode"] = "MANIFEST" }, new JsonObject { ["ClassName"] = "other" }) };
        var classes = new JsonObject { ["bill"] = new JsonObject(), ["renamed"] = new JsonObject(), ["other"] = new JsonObject() };
        Assert.That(ModelDraft.Classifier(snapshot, "TOKEN", classes, "new", (_, _) => null).Draft, Is.Null);
        var result = ModelDraft.Classifier(snapshot, "TOKEN", classes, "new", (_, _) => "renamed"); Assert.That(result.Draft!["bill-shipping-line"]!.ToString(), Is.EqualTo("LINE")); Assert.That(result.Draft["manifest-class-name"]!.ToString(), Is.EqualTo("renamed"));
    }
    [Test]
    public async Task Inspect_reports_confidence_and_never_reads_SQL()
    {
        var model = Model(Fields());
        model["docTypes"]!["doc"]!["fieldConfidence"] = new JsonObject { ["Name"] = .97, ["Containers.*.Number"] = .91 };
        model["docTypes"]!["doc"]!["buildMode"] = "neural";
        using var http = new HttpClient(new Handler(r => { Assert.That(r.RequestUri!.AbsolutePath, Does.Contain("documentModels/new")); return Response(model); }));
        var client = new DocumentModelClient(http, new Tokens(), Endpoint);
        var a = OperatorArguments.Parse(["models", "inspect", "--document-type-code", "MANIFEST", "--model-id", "new", "--endpoint", Endpoint]);
        var c = new Input(); Assert.That(await ModelReviewCli.Execute(a, c, client, () => throw new AssertionException("SQL called")), Is.Zero);
        Assert.That(string.Join("\n", c.Output), Does.Contain("0.97").And.Contain("0.91").And.Contain("unavailable").And.Contain("neural"));
        a.Values["json"] = "true"; c.Output.Clear(); await ModelReviewCli.Execute(a, c, client, () => throw new AssertionException("SQL called"));
        var parsed = JsonNode.Parse(c.Output.Single())!; Assert.That(parsed["docTypes"]!["doc"]!["fieldSchema"]!["Name"]!["confidence"]!.GetValue<double>(), Is.EqualTo(.97));
    }
    [Test]
    public async Task Classifier_inspect_uses_classifier_route()
    {
        using var http = new HttpClient(new Handler(r => { Assert.That(r.RequestUri!.AbsolutePath, Does.Contain("documentClassifiers/new")); return Response(new JsonObject { ["classifierId"] = "new", ["docTypes"] = new JsonObject { ["bill"] = new JsonObject() } }); }));
        var c = new Input(); var a = OperatorArguments.Parse(["models", "inspect", "--document-type-code", "AUTO", "--model-id", "new", "--endpoint", Endpoint]);
        Assert.That(await ModelReviewCli.Execute(a, c, new DocumentModelClient(http, new Tokens(), Endpoint), () => throw new AssertionException("SQL called")), Is.Zero);
        Assert.That(string.Join("\n", c.Output), Does.Contain("Classes: bill"));
    }
    [Test]
    public void Empty_output_folder_is_accepted_and_existing_files_are_preserved()
    {
        var folder = Path.Combine(RepositoryPaths.ToolsRoot, "tests", "ISTDocAi.Tools.Tests", "TestResults", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { Assert.That(ModelReviewCli.ValidateFolder(folder), Is.EqualTo(folder)); File.WriteAllText(Path.Combine(folder, "keep"), "original"); Assert.Throws<ArgumentException>(() => ModelReviewCli.ValidateFolder(folder)); Assert.That(File.ReadAllText(Path.Combine(folder, "keep")), Is.EqualTo("original")); }
        finally { Directory.Delete(folder, true); }
    }
    [TestCase("name")][TestCase("bad-key")]
    public void New_keys_cannot_duplicate_or_violate_binding_contract(string answer)
    {
        var fields = Fields(); fields["New"] = Scalar(); Assert.That(Draft(fields, (_, _) => answer).Draft, Is.Null);
    }
    [Test]
    public void Nested_types_and_legacy_manifest_Field_rows_are_compared_in_their_real_scope()
    {
        var fields = Fields(); fields["Name"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["nested"] = Scalar() } };
        Assert.That(Draft(fields).Draft, Is.Null);
        var snapshot = Snapshot(); snapshot["MappingProfile"]!["MappingFields"]![0]!["MapKind"] = "Field";
        var result = ModelDraft.Extract(snapshot, "TOKEN", Fields(), Fields(), "new", "REF", (_, _) => throw new AssertionException("Carried Field reoffered as Header"));
        Assert.That(result.Draft, Is.Not.Null);
        Assert.That(JsonNode.DeepEquals(result.Draft!["Rows"], snapshot["MappingProfile"]!["MappingFields"]), Is.True);
    }
    [TestCase(true)][TestCase(false)]
    public async Task Workflow_output_and_redirected_input(bool redirected)
    {
        var folder = Path.Combine(RepositoryPaths.ToolsRoot, "tests", "ISTDocAi.Tools.Tests", "TestResults", Guid.NewGuid().ToString("N"));
        try
        {
            var a = OperatorArguments.Parse(["models", "draft", "--document-type-code", "MANIFEST", "--model-id", "new", "--endpoint", Endpoint, "--output", folder]);
            using var http = new HttpClient(new Handler(_ => Response(Model(Fields())))); var c = new Input("REF") { IsInputRedirected = redirected };
            var exit = await ModelReviewCli.Execute(a, c, new DocumentModelClient(http, new Tokens(), Endpoint), () => Task.FromResult((Snapshot(), new string('A', 64))));
            Assert.That(exit, Is.EqualTo(redirected ? 2 : 0)); Assert.That(File.Exists(Path.Combine(folder, "draft-mapping.json")), Is.EqualTo(!redirected)); Assert.That(File.Exists(Path.Combine(folder, "review.json")), Is.True);
            Assert.Throws<ArgumentException>(() => ModelReviewCli.ValidateFolder(folder)); Assert.That(string.Join("\n", c.Output), Does.Not.Contain("secret-fake-token"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}

[Category("Sql")]
public sealed class ModelDraftSqlTests : GuardedToolTests
{
    [Test]
    public async Task Seeded_manifest_fake_schema_preserves_mapping()
    {
        await using var db = new ProvisioningDatabase(); await db.Initialize();
        await using var sql = new SqlConnection(db.ConnectionString); await sql.OpenAsync();
        // Register the frozen manifest seed only inside this disposable integration database. New models enter as
        // Testing: a sealed mapping profile cannot be bound to a model that is already Active.
        await sql.ExecuteAsync("INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status,TestFieldAccuracy,MinFieldConfidence) VALUES('manifest-v1','MANIFEST','extract',1,'Testing',.99,.8); INSERT dbo.ModelMappingBindings(ModelId,ProfileId) SELECT 'manifest-v1',ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='manifest-v1'; UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME() WHERE ModelId='manifest-v1' AND Status='Testing';");
        var row = await sql.QuerySingleAsync(ModelUpgradeSql.Active("MANIFEST"));
        var snapshot = JsonNode.Parse((string)row.SnapshotJson)!.AsObject(); var profile = snapshot["MappingProfile"]!.AsObject();
        var fields = new JsonObject();
        foreach (var selector in new[] { "ContainersField", "BillsOfLadingField" }) fields[profile[selector]!.ToString()] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() } };
        var container = fields[profile["ContainersField"]!.ToString()]!["items"]!["properties"]!.AsObject();

        foreach (var mapping in profile["MappingFields"]!.AsArray())
        {
            var kind = mapping!["MapKind"]!.ToString(); var scope = kind == "Container" ? container : kind == "Bill" ? fields[profile["BillsOfLadingField"]!.ToString()]!["items"]!["properties"]!.AsObject() : fields;
            scope[mapping["AzureName"]!.ToString()] = ModelReviewTests.Scalar();
        }
        // Both fetches (old and new) get their own copy: a JsonNode can belong to only one parent.
        using var http = new HttpClient(new ModelReviewTests.Handler(_ => ModelReviewTests.Response(ModelReviewTests.Model(fields.DeepClone().AsObject()))));
        var client = new DocumentModelClient(http, new ModelReviewTests.Tokens(), ModelReviewTests.Endpoint);
        var old = await client.Fetch("old", false); var next = await client.Fetch("new", false);
        var result = ModelDraft.Extract(snapshot, (string)row.SNAPSHOT_TOKEN, DocumentModelClient.Fields(old), DocumentModelClient.Fields(next), "draft-new", "REF", (_, _) => "ignore");
        Assert.That(JsonNode.DeepEquals(result.Draft!["Rows"], profile["MappingFields"]), Is.True);
        Assert.That(result.Review["Items"]!.AsArray().Count(r => r!["Status"]!.ToString() is "new" or "removed"), Is.Zero);
    }
}
