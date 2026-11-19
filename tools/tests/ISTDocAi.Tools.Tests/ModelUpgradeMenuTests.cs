using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using ISTDocAi.Provisioning;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class ModelUpgradeMenuTests
{
    private sealed class Input(IEnumerable<string> lines, Action<string>? wrote = null) : IOperatorConsole
    {
        private readonly Queue<string> lines = new(lines);
        public bool IsInputRedirected => false;
        public List<string> Output { get; } = [];
        public string? ReadLine() => lines.TryDequeue(out var line) ? line : null;
        public ConsoleKeyInfo ReadKey() => throw new AssertionException("Unexpected key input");
        public void Write(string text) { Output.Add(text); wrote?.Invoke(text); }
        public void WriteLine(string text) { Output.Add(text); wrote?.Invoke(text); }
    }
    private static string[] Start => ["4", "2", "MANIFEST", "new", ModelReviewTests.Endpoint, "", "", "SQL01", "", "no", "REF", ""];
    private static void Edit(string folder, string colour)
    {
        var path = Path.Combine(folder, "draft-mapping.json"); var draft = JsonNode.Parse(File.ReadAllText(path))!;
        if (colour == "BLUE") draft["Rows"]!.AsArray().Add(ModelVerdictTests.Row("Header", "newKey"));
        if (colour == "YELLOW") draft["Rows"]!.AsArray().RemoveAt(0);
        if (colour == "RED") draft["Rows"]!.AsArray().Add(ModelVerdictTests.Row("Container", "unsupported", "Number"));
        File.WriteAllText(path, draft.ToJsonString());
    }
    [TestCase("GREEN", "1")][TestCase("GREEN", "2")][TestCase("BLUE", "1")][TestCase("YELLOW", "1")][TestCase("RED", "1")]
    public async Task Final_edited_draft_controls_run_script_or_refusal(string colour, string apply)
    {
        var folder = ModelVerdictTests.Folder(); var (active, _, _) = ModelVerdictTests.Data();
        var tail = colour == "RED" ? new[] { "", "0" } : (colour is "BLUE" or "YELLOW" ? new[] { "yes" } : []).Concat(new[] { "0.99", "TEST-42", apply }).Concat(apply == "2" ? new[] { Path.Combine(folder, "sql") } : []).Concat(new[] { "", "0" }).ToArray();
        var c = new Input(Start.Concat(tail), text => { if (text == "Review/edit the draft, then press Enter") Edit(folder, colour); });
        using var http = new HttpClient(new ModelReviewTests.Handler(_ => ModelReviewTests.Response(ModelReviewTests.Model(ModelReviewTests.Fields()))));
        var client = new DocumentModelClient(http, new ModelReviewTests.Tokens(), ModelReviewTests.Endpoint);
        var menu = new OperatorMenu(c)
        {
            PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new System.Text.Json.Nodes.JsonObject())),
            ReviewFolder = () => folder,
            DraftRunner = (args, console) => ModelReviewCli.Execute(args, console, client, () => Task.FromResult((active, new string('A', 64))))
        };
        var calls = new List<string[]>();
        try
        {
            Assert.That(await menu.Run(args => { calls.Add(args); return Task.FromResult(0); }), Is.Zero);
            Assert.That(calls.Count, Is.EqualTo(colour == "RED" ? 0 : 1));
            Assert.That(c.Output.Count(x => x == OperatorMenu.Text), Is.EqualTo(2));
            Assert.That(string.Join("\n", c.Output), Does.Contain("Overall verdict: " + colour));
            if (colour == "RED") return;
            var a = OperatorArguments.Parse(calls.Single());
            Assert.That(a.Action, Is.EqualTo("upgrade-extract"));
            Assert.That(a.Get("model-id"), Is.EqualTo("new")); Assert.That(a.Get("document-type-code"), Is.EqualTo("MANIFEST"));
            Assert.That(a.Get("expected-snapshot"), Is.EqualTo(new string('A', 64)));
            Assert.That(a.Get("test-field-accuracy"), Is.EqualTo("0.99")); Assert.That(a.Get("evidence"), Is.EqualTo("TEST-42"));
            Assert.That(a.Has("generate-sql"), Is.EqualTo(apply == "2"));
            Assert.That(a.Has("sql-server"), Is.EqualTo(apply == "1"));
            var final = JsonNode.Parse(File.ReadAllText(a.Get("mapping-file")))!;
            Assert.That(JsonNode.DeepEquals(final, JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "draft-mapping.json")))), Is.True);
            Assert.That(string.Join("\n", c.Output), Does.Contain(ModelUpgradeSql.ChargingNote).And.Contain("Azure estimate: unavailable"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    [TestCase("BLUE", "no")][TestCase("YELLOW", "no")][TestCase("YELLOW", "YES")]
    public async Task Refused_confirmation_does_not_reach_command(string colour, string answer)
    {
        var folder = ModelVerdictTests.Folder();
        var c = new Input(Start.Take(Start.Length - 2).Concat(["", answer, "", "0"]), text => { if (text == "Review/edit the draft, then press Enter") Edit(folder, colour); });
        var menu = new OperatorMenu(c) { PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new System.Text.Json.Nodes.JsonObject())), ReviewFolder = () => folder, DraftRunner = (_, _) => { var (_, draft, review) = ModelVerdictTests.Data(); ModelVerdictTests.Save(folder, draft, review); return Task.FromResult(0); } };
        try { await menu.Run(_ => throw new AssertionException("Activation reached")); Assert.That(string.Join("\n", c.Output), Does.Contain("CANCELLED")); }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    [TestCase("unreachable")][TestCase("denied")][TestCase("expired")]
    public async Task Azure_failure_refuses_and_returns_to_menu(string failure)
    {
        var c = new Input(Start.Take(Start.Length - 2).Concat(["", "0"]));
        using var http = new HttpClient(new ModelReviewTests.Handler(_ =>
        {
            if (failure == "unreachable") throw new HttpRequestException("secret");
            if (failure == "denied") return new(System.Net.HttpStatusCode.Forbidden);
            var model = ModelReviewTests.Model(ModelReviewTests.Fields()); model["expirationDateTime"] = "2000-01-01T00:00:00Z"; return ModelReviewTests.Response(model);
        }));
        var client = new DocumentModelClient(http, new ModelReviewTests.Tokens(), ModelReviewTests.Endpoint);
        var menu = new OperatorMenu(c) { PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new System.Text.Json.Nodes.JsonObject())), DraftRunner = (a, console) => ModelReviewCli.Execute(a, console, client, () => throw new AssertionException("SQL reached")) };
        await menu.Run(_ => throw new AssertionException("Activation reached"));
        Assert.That(string.Join("\n", c.Output), Does.Contain("REFUSED").And.Not.Contain("secret"));
        Assert.That(c.Output.Count(x => x == OperatorMenu.Text), Is.EqualTo(2));
    }
    [TestCase("yes", true)][TestCase("no", false)]
    public async Task Accuracy_warning_requires_confirmation_and_displays_both_numbers(string answer, bool runs)
    {
        var folder = ModelVerdictTests.Folder(); var (_, draft, review) = ModelVerdictTests.Data();
        DocumentModelClient.Fields(review["AzureModel"]!.AsObject())["Name"]!["confidence"] = .8m;
        var c = new Input(Start.Take(Start.Length - 2).Concat(["", "0.99", "TEST", answer]).Concat(runs ? ["1", "", "0"] : new[] { "", "0" }));
        var menu = new OperatorMenu(c) { PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new System.Text.Json.Nodes.JsonObject())), ReviewFolder = () => folder, DraftRunner = (_, _) => { ModelVerdictTests.Save(folder, draft, review); return Task.FromResult(0); } };
        var calls = 0;
        try
        {
            await menu.Run(_ => { calls++; return Task.FromResult(0); });
            Assert.That(calls, Is.EqualTo(runs ? 1 : 0));
            Assert.That(string.Join("\n", c.Output), Does.Contain("Measured: 0.99; Azure estimate: 0.8").And.Contain("5 percentage points"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    [Test]
    public async Task Auto_passes_final_routes_and_white_confirmation_to_existing_command()
    {
        var folder = ModelVerdictTests.Folder(); var (_, draft, review) = ModelVerdictTests.Auto();
        var c = new Input(["4", "2", "AUTO", "new", ModelReviewTests.Endpoint, "", "SQL01", "", "no", "", "yes", "0.99", "TEST", "0.7", "1", "", "0"]);
        var menu = new OperatorMenu(c) { PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new System.Text.Json.Nodes.JsonObject())), ReviewFolder = () => folder, DraftRunner = (_, _) => { ModelVerdictTests.Save(folder, draft, review, true); return Task.FromResult(0); } };
        string[]? call = null;
        try
        {
            await menu.Run(args => { call = args; return Task.FromResult(0); });
            Assert.That(call, Is.Not.Null); var a = OperatorArguments.Parse(call!);
            Assert.That(a.Action, Is.EqualTo("upgrade-classifier"));
            foreach (var name in ModelVerdict.RouteNames) Assert.That(a.Get(name), Is.EqualTo(draft[name]!.ToString()));
            Assert.That(a.Get("bill-shipping-line"), Is.EqualTo("LINE")); Assert.That(a.Get("min-confidence"), Is.EqualTo("0.7"));
            Assert.That(a.Has("mapping-file"), Is.False);
            Assert.That(string.Join("\n", c.Output), Does.Contain("WHITE").And.Contain("Azure estimate: unavailable"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    [Test]
    public async Task Endpoint_and_SQL_are_remembered_across_upgrades_in_session()
    {
        var first = ModelVerdictTests.Folder(); var second = ModelVerdictTests.Folder(); var n = 0;
        var c = new Input(Start.Take(Start.Length - 2).Concat(["", "0.99", "TEST", "1", "", "2", "MANIFEST", "new", "", "", "0.99", "TEST", "1", "", "0"]));
        var menu = new OperatorMenu(c) { PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new System.Text.Json.Nodes.JsonObject())), ReviewFolder = () => ++n == 1 ? first : second, DraftRunner = (a, _) => { Assert.That(a.Get("endpoint"), Is.EqualTo(ModelReviewTests.Endpoint)); Assert.That(a.Get("sql-server"), Is.EqualTo("SQL01")); var (_, draft, review) = ModelVerdictTests.Data(); ModelVerdictTests.Save(a.Get("output"), draft, review); return Task.FromResult(0); } };
        var calls = 0;
        try { await menu.Run(_ => { calls++; return Task.FromResult(0); }); Assert.That(calls, Is.EqualTo(2)); Assert.That(c.Output.Count(t => t.StartsWith("Document Intelligence endpoint", StringComparison.Ordinal)), Is.EqualTo(1)); }
        finally { foreach (var f in new[] { first, second }) if (Directory.Exists(f)) Directory.Delete(f, true); }
    }
}
