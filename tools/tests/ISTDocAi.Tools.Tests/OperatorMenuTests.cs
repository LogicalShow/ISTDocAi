using System.Text.Json.Nodes;
using ISTDocAi.OperatorTool;
using ISTDocAi.Provisioning;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

[Category("Pure")]
public sealed class OperatorMenuTests
{
    private sealed class Fake(params string[] lines) : IOperatorConsole
    {
        private readonly Queue<string> input = new(lines);
        public bool IsInputRedirected { get; init; }
        public List<string> Output { get; } = [];
        public int Reads { get; private set; }
        public string? ReadLine() { Reads++; return input.TryDequeue(out var line) ? line : null; }
        public ConsoleKeyInfo ReadKey() => throw new AssertionException("Unexpected secret prompt");
        public void Write(string text) => Output.Add(text);
        public void WriteLine(string text) => Output.Add(text);
    }
    private static readonly string[] Connection = ["c", "SQL01", "", "yes", ""];
    private static OperatorMenu Menu(Fake c, JsonNode? cycles = null) => new(c) { PriceStatusReader = (_, _, _) => Task.FromResult(new AzurePriceStatus(new JsonObject())), CycleReader = _ => cycles is null ? throw new Exception("secret reader failure") : Task.FromResult(cycles) };
    private static JsonNode Cycles(bool more = false) => JsonNode.Parse("""
        {"prelude":{},"cycles":[
          {"topUpGrantId":11,"startAtUtc":"2026-01-01T11:00:00Z","topUpPages":100,"isReversed":false},
          {"topUpGrantId":42,"startAtUtc":"2026-02-02T12:30:00Z","topUpPages":200,"isReversed":true}],"hasMoreCycles":false}
        """)!.WithMore(more);

    [TestCase("warning")][TestCase("stale")][TestCase("empty")][TestCase("missing")][TestCase("error")]
    public async Task Price_banner_cache_refresh_and_route(string mode)
    {
        var c = new Fake(Connection.Concat(["3", "4", "", "0", "c", "SQL02", "", "no", "", "0"]).ToArray());
        var reads = 0; var calls = new List<string[]>();
        var data = AzurePriceStatusTests.Fixture();
        if (mode == "missing") data["historyAvailable"] = false;
        if (mode is "warning" or "stale") AzurePriceStatusTests.Pair(data, mode == "warning" ? "Failed" : "NoChange", AzurePriceStatusTests.Now.AddHours(mode == "stale" ? -27 : 0));
        var menu = new OperatorMenu(c) { PriceStatusReader = (server, _, _) => {
            reads++; Assert.That(server,Is.EqualTo(reads == 3 ? "SQL02" : "SQL01"));
            if(mode == "error") throw new InvalidOperationException("secret");
            return Task.FromResult(new AzurePriceStatus(data,new AzurePriceStatusTests.Clock(AzurePriceStatusTests.Now)));
        }};
        await menu.Run(a => { calls.Add(a); return Task.FromResult(0); });
        Assert.That(reads,Is.EqualTo(3)); Assert.That(calls.Single().Take(2),Is.EqualTo(new[]{"prices","show"}));
        Assert.That(c.Output.Any(x=>x.StartsWith("!! Azure price check")),Is.EqualTo(mode is "warning" or "stale"));
        Assert.That(string.Join(" ",c.Output),Does.Not.Contain("secret"));
    }

    [Test]
    public async Task Azure_dates_all_consumers_zone_every_time_output_retry_and_shared_consumer_preserved()
    {
        var folder = ModelVerdictTests.Folder();
        try
        {
            var c = new Fake(Connection.Concat(new[] { "1", "1", "TEST", "", "0", "3", "1", "", "bad", "1", "2026-11-15", "2026-11-01", "2026-11-01", "2026-11-15", "bad", "2", Path.Combine(folder,"first.csv"), "", "1", "", "2", "2026-11-01", "2026-11-15", "1", Path.Combine(folder,"second.pdf"), "", "0", "1", "1", "", "", "0", "0" }).ToArray());
            var calls = new List<string[]>();
            await Menu(c).Run(a => { calls.Add(a); return Task.FromResult(0); });
            Assert.That(calls.Count, Is.EqualTo(4));
            var reports = calls.Skip(1).Take(2).Select(OperatorArguments.Parse).ToArray();
            Assert.That(reports.All(a => !a.Has("consumer")), Is.True);
            Assert.That(reports.Select(a => a.Get("time-zone")), Is.EqualTo(new[] { "egypt", "saudi" }));
            Assert.That(reports[0].Has("csv"), Is.True); Assert.That(reports[1].Has("pdf"), Is.True);
            Assert.That(OperatorArguments.Parse(calls[3]).Get("consumer"), Is.EqualTo("TEST"));
            Assert.That(c.Output.Count(x => x == "Time zone:"), Is.EqualTo(2));
            Assert.That(c.Output.Count(x => x == "From date (yyyy-MM-dd): "), Is.EqualTo(3));
            Assert.That(c.Output.Count(x => x == ISTDocAi.OperatorTool.Reports.InternalWarning), Is.EqualTo(2));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Test]
    public async Task Priced_route_asks_zone_currency_every_time_and_retries_invalid_inputs()
    {
        var folder = ModelVerdictTests.Folder();
        try
        {
            var c = new Fake(Connection.Concat(new[] { "2", "3", "TEST", "bad", "1", "2026-11-15", "2026-11-01", "2026-11-01", "2026-11-15", "bad", "1", "0", "0.5", "bad", "1", Path.Combine(folder,"one.pdf"), "", "3", "", "2", "2026-11-01", "2026-11-15", "2", "1", "2", Path.Combine(folder,"two.csv"), "", "0", "0" }).ToArray());
            var calls = new List<OperatorArguments>();
            await Menu(c).Run(a => { calls.Add(OperatorArguments.Parse(a)); return Task.FromResult(0); });
            Assert.That(calls.Count, Is.EqualTo(2));
            Assert.That(calls.Select(a => a.Get("currency")), Is.EqualTo(new[] { "EGP", "USD" }));
            Assert.That(calls.Select(a => a.Get("time-zone")), Is.EqualTo(new[] { "egypt", "saudi" }));
            Assert.That(calls.All(a => a.Get("consumer") == "TEST" && !a.Has("generate-sql")), Is.True);
            Assert.That(c.Output.Count(x => x == "Currency:"), Is.EqualTo(2));
            Assert.That(c.Output.Count(x => x == "Time zone:"), Is.EqualTo(2));
            Assert.That(c.Output, Does.Contain("Rate 0.50 EGP per page, 2026-11-01..2026-11-15 (Egypt time)").And.Not.Contain(ISTDocAi.OperatorTool.Reports.InternalWarning));
            Assert.That(c.Output.Any(x => x.StartsWith("Price per page must")), Is.True);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestCase(false, true)][TestCase(true, false)]
    public async Task Entry_requires_interactive_empty_arguments(bool redirected, bool menu)
    {
        var c = new Fake("0") { IsInputRedirected = redirected };
        Assert.That(OperatorMenu.ShouldStart([], c), Is.EqualTo(menu));
        Assert.That(await OperatorCli.Run([], c), Is.Zero);
        Assert.That(c.Output.Contains(OperatorMenu.Text), Is.EqualTo(menu));
        Assert.That(c.Output.Any(x => x.Contains(OperatorCli.Help)), Is.EqualTo(!menu));
    }
    [TestCase("--help")][TestCase("-h")][TestCase("invalid")]
    public async Task Arguments_never_enter_menu(string argument)
    {
        var c = new Fake("0");
        Assert.That(OperatorMenu.ShouldStart([argument], c), Is.False);
        Assert.That(await OperatorCli.Run([argument], c), Is.EqualTo(argument == "invalid" ? 1 : 0));
        Assert.That(c.Reads, Is.Zero);
    }

    private sealed record Route(string Section, string Choice, string[] Fields, string[] Prefix, bool Script = false, bool Sql = false);
    private static IEnumerable<Route> Routes(string folder) => [
        new("3", "4", [], ["prices", "show"], Sql: true),
        new("2", "3", ["TEST", "1", "2026-01-01", "2026-01-31", "1", "0.5", "1", Path.Combine(folder,"priced.pdf")], ["report", "customer-price"], Sql: true),
        new("2", "1", ["TEST", "1", "2026-01-01", "2026-01-31", Path.Combine(folder,"range.pdf")], ["pages", "statement"], Sql: true),
        new("2", "2", ["TEST", "2", "2026-01-01", "2026-01-31", Path.Combine(folder,"range.csv")], ["pages", "documents-by-date"], Sql: true),
        new("1", "1", ["TEST"], ["pages", "status"], Sql: true),
        new("1", "2", ["TEST", "100", "reason", "REF"], ["pages", "topup"], true),
        new("1", "3", ["TEST", "-10", "reason", "", "yes"], ["pages", "adjust"], true),
        new("1", "4", ["TEST", "2", "reason", "", "no"], ["pages", "reverse"], true),
        new("1", "5", ["TEST", "", "reason", ""], ["pages", "set-warning"], true),
        new("1", "6", ["TEST", "Prepaid", "100", "reason", ""], ["pages", "set-mode"], true),
        new("2", "4", ["TEST", Path.Combine(folder,"customer.pdf")], ["pages", "cycles"], Sql: true),
        new("2", "5", ["TEST", "0", Path.Combine(folder,"customer.csv")], ["pages", "documents"], Sql: true),
        new("2", "6", ["https://example.invalid", "kit.xml", "prelude", "yes", "json", "Company"], ["report", "customer"]),
        new("3", "1", ["", "1", "2026-01-01", "2026-01-31", "bad", "2", Path.Combine(folder,"azure.csv")], ["report", "azure-cost"], Sql: true),
        new("3", "2", ["TEST", Path.Combine(folder,"ist.pdf")], ["pages", "cycles"], Sql: true),
        new("3", "3", ["TEST", "1", Path.Combine(folder,"ist.csv")], ["pages", "documents"], Sql: true),
        new("4", "1", ["BL", "LINE"], ["models", "active"], true),
        new("4", "3", ["BL", "new", ModelReviewTests.Endpoint, "", ""], ["models", "inspect"]),
        new("4", "4", ["BL", "new", ModelReviewTests.Endpoint, "", "", "", folder], ["models", "draft"], Sql: true),
        new("4", "5", [folder, ""], ["models", "check"]),
        new("4", "7", ["BL", "new", "mapping.json", "yes"], ["models", "mapping"], true),
        new("4", "8", ["new", "0.99", "", "", "", "yes"], ["models", "register-bl"], true),
        new("4", "9", new[] { "new", "0.99" }.Concat(Enumerable.Repeat("",15)).Concat(["yes"]).ToArray(), ["models", "register-set"], true),
        new("5", "1", ["", "dev", "", "", ""], ["azure", "identity", "create"]),
        new("5", "2", [Guid.Empty.ToString(), "", "", ""], ["azure", "identity", "renew"]),
        new("5", "3", [Guid.Empty.ToString(), "ABCD", "", "yes"], ["azure", "identity", "remove-old"]),
        new("5", "4", [""], ["azure", "infra"]),
        new("5", "5", [folder], ["--export-sql"])
    ];

    [TestCase("1", "1", new string[0], new[] { "pages", "status", "--consumer", "TEST" })]
    [TestCase("1", "2", new[] { "100", "reason", "REF", "1" }, new[] { "pages", "topup", "--consumer", "TEST", "--pages", "100", "--note", "reason", "--reference", "REF" })]
    [TestCase("1", "4", new[] { "42", "reason", "", "no", "1" }, new[] { "pages", "reverse", "--consumer", "TEST", "--grant-id", "42", "--note", "reason" })]
    [TestCase("1", "5", new[] { "", "reason", "", "1" }, new[] { "pages", "set-warning", "--consumer", "TEST", "--percent", "10", "--note", "reason" })]
    [TestCase("2", "4", new[] { "statement.pdf" }, new[] { "pages", "cycles", "--consumer", "TEST", "--audience", "customer", "--pdf", "statement.pdf" })]
    [TestCase("2", "5", new[] { "prelude", "docs.csv" }, new[] { "pages", "documents", "--consumer", "TEST", "--grant-id", "prelude", "--audience", "customer", "--csv", "docs.csv" })]
    [TestCase("3", "2", new[] { "internal.pdf" }, new[] { "pages", "cycles", "--consumer", "TEST", "--audience", "ist", "--pdf", "internal.pdf" })]
    public async Task Existing_page_routes_preserve_exact_arguments(string section, string choice, string[] fields, string[] expected)
    {
        var picker = section == "1" && choice == "4" || section == "2" && choice == "5";
        // A failed injected reader exercises the original manual grant input without SQL access.
        var lines = picker
            ? Connection.Concat([section, choice, "TEST"]).Concat(fields)
            : new[] { section, choice, "TEST" }.Concat(fields).Concat(["SQL01", "", "yes"]);
        var c = new Fake(lines.Concat(["", "0", "0"]).ToArray());
        string[]? call = null;
        await Menu(c).Run(a => { call = a; return Task.FromResult(0); });
        Assert.That(call, Is.EqualTo(expected.Concat(["--sql-server", "SQL01", "--database", "ISTDocAi", "--trust-server-certificate"])));
        Assert.That(OperatorArguments.Parse(call!).Page("test").Consumer, Is.EqualTo("TEST"));
    }

    [Test]
    public async Task Every_help_signature_has_a_menu_route_and_only_supported_commands_offer_scripts()
    {
        var folder = ModelVerdictTests.Folder(); var (_, draft, review) = ModelVerdictTests.Data();
        ModelVerdictTests.Save(folder, draft, review);
        var reached = new HashSet<string>();
        try
        {
            foreach (var route in Routes(folder))
            {
                var lines = Connection.Concat([route.Section, route.Choice]).Concat(route.Fields)
                    .Concat(route.Script ? new[] { "2", Path.Combine(folder,"sql") } : []).Concat(["", "0", "0"]).ToArray();
                var c = new Fake(lines); string[]? call = null;
                Assert.That(await Menu(c, Cycles()).Run(a => { call = a; return Task.FromResult(0); }), Is.Zero);
                Assert.That(call, Is.Not.Null, route.Prefix.Last() + ": " + string.Join("\n",c.Output));
                Assert.That(call!.Take(route.Prefix.Length), Is.EqualTo(route.Prefix));
                Assert.That(c.Output.Count(x => x == "How do you want to apply this?"), Is.EqualTo(route.Script ? 1 : 0));
                Assert.That(call.Contains("--generate-sql"), Is.EqualTo(route.Script));
                Assert.That(call.Contains("--sql-server"), Is.EqualTo(route.Sql));
                Assert.That(c.Output.Contains(ISTDocAi.OperatorTool.Reports.InternalWarning), Is.EqualTo(route.Section == "3" && route.Prefix[0] != "prices"));
                if (route.Prefix[0] == "azure") Assert.That(call, Does.Not.Contain("--yes"));
                reached.Add(string.Join(" ",route.Prefix));
            }
            foreach (var auto in new[] { false, true })
            {
                var (_, finalDraft, finalReview) = auto ? ModelVerdictTests.Auto() : ModelVerdictTests.Data();
                ModelVerdictTests.Save(folder, finalDraft, finalReview, auto);
                var c = new Fake(new[] { "4", "6", folder }.Concat(auto ? new[] { "yes" } : []).Concat(["0.99", "TEST"]).Concat(auto ? new[] { "0.7" } : []).Concat(["2", Path.Combine(folder,"sql"), "", "0", "0"]).ToArray());
                string[]? call = null;
                await Menu(c).Run(a => { call = a; return Task.FromResult(0); });
                Assert.That(call, Is.Not.Null, string.Join("\n", c.Output));
                var a = OperatorArguments.Parse(call!);
                Assert.That(a.Action, Is.EqualTo(auto ? "upgrade-classifier" : "upgrade-extract"));
                Assert.That(a.Get("expected-snapshot"), Is.EqualTo(ModelVerdict.S(finalReview,"SNAPSHOT_TOKEN")));
                Assert.That(a.Has("generate-sql"), Is.True); reached.Add("models " + a.Action);
            }
            // Enumerate command declarations from both help texts, expanding the pages shorthand.
            var help = OperatorCli.Help + "\n" + AzureCli.Help;
            var signatures = help.Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith("prices ") || x.StartsWith("pages ") || x.StartsWith("models ") || x.StartsWith("report ") || x.StartsWith("azure ") || x.StartsWith("--export-sql "))
                .SelectMany(x => x.StartsWith("pages ") ? x.Split(' ')[1].Split('|').Select(a => "pages " + a) : new[] { string.Join(" ", x.Split(' ').Take(x.StartsWith("azure identity ") ? 3 : x.StartsWith("--export-sql ") ? 1 : 2)) });
            Assert.That(reached, Is.SupersetOf(signatures));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestCase("1", "4", new[] { "1", "2", "reason", "", "no", "2", "sql" }, "11")]
    [TestCase("2", "5", new[] { "1", "docs.csv" }, "42")]
    [TestCase("2", "5", new[] { "0", "docs.csv" }, "prelude")]
    public async Task Picker_orders_flags_refuses_reversed_only_for_reverse(string section, string choice, string[] tail, string grant)
    {
        var c = new Fake(Connection.Concat([section, choice, "TEST"]).Concat(tail).ToArray()); string[]? call = null;
        await Menu(c, Cycles()).Run(a => { call = a; return Task.FromResult(0); });
        Assert.That(call, Is.Not.Null);
        Assert.That(OperatorArguments.Parse(call!).Get("grant-id"), Is.EqualTo(grant));
        Assert.That(c.Output, Does.Contain("1) 2026-02-02 12:30 UTC  +200 pages  (grant 42)  [REVERSED]"));
        Assert.That(c.Output, Does.Contain("2) 2026-01-01 11:00 UTC  +100 pages  (grant 11)"));
        Assert.That(c.Output.Contains("Already reversed."), Is.EqualTo(section == "1"));
        Assert.That(c.Output.Contains("0) Prelude (before the first top-up)"), Is.EqualTo(section == "2"));
    }
    [TestCase(false)][TestCase(true)]
    public async Task Picker_empty_cycles(bool documents)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var c = new Fake(Connection.Concat([documents ? "2" : "1", documents ? "5" : "4", "TEST"]).Concat(documents ? new[] { "0", Path.Combine(folder,"docs.csv") } : []).ToArray());
        var calls = new List<string[]>();
        try
        {
            await Menu(c, JsonNode.Parse("{\"cycles\":[]}")!).Run(a => { calls.Add(a); return Task.FromResult(0); });
            Assert.That(calls.Count, Is.EqualTo(documents ? 1 : 0));
            Assert.That(c.Output.Contains("No top-ups to reverse."), Is.EqualTo(!documents));
            if (documents) Assert.That(OperatorArguments.Parse(calls[0]).Get("grant-id"), Is.EqualTo("prelude"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    [TestCase(false)][TestCase(true)]
    public async Task Picker_manual_id_for_pagination_or_reader_failure(bool fails)
    {
        var c = new Fake(Connection.Concat(["1", "4", "TEST", fails ? "123" : "id 123", "reason", "", "yes", "2", "sql"]).ToArray()); string[]? call = null;
        await Menu(c, fails ? null : Cycles(true)).Run(a => { call = a; return Task.FromResult(0); });
        Assert.That(OperatorArguments.Parse(call!).Get("grant-id"), Is.EqualTo("123"));
        Assert.That(call, Does.Contain("--allow-debt"));
        Assert.That(string.Join("\n", c.Output), Does.Contain(fails ? "Could not read top-ups." : "Showing the latest 200 top-ups.").And.Not.Contain("secret"));
    }

    [Test]
    public async Task Picker_empty_input_cancels_without_running()
    {
        var c = new Fake(Connection.Concat(["1", "4", "TEST", "", "", "0", "0"]).ToArray()); var calls = 0;
        await Menu(c, Cycles()).Run(_ => { calls++; return Task.FromResult(0); });
        Assert.That(calls, Is.Zero);
        Assert.That(c.Output, Does.Contain("CANCELLED: No further action taken."));
    }
    [TestCase("5")][TestCase("6")]
    public async Task Review_folder_without_review_json_is_refused_clearly(string choice)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(folder);
        try
        {
            var c = new Fake("4", choice, folder, "", "0", "0"); var calls = 0;
            await Menu(c).Run(_ => { calls++; return Task.FromResult(0); });
            Assert.That(calls, Is.Zero);
            Assert.That(c.Output, Does.Contain("REFUSED: No review.json in this folder. Run Draft first and use its review folder."));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestCase("yes", true)][TestCase(" yes ", true)][TestCase("y", false)][TestCase("YES ", false)][TestCase("", false)]
    public async Task Typed_yes_is_case_sensitive_and_trimmed(string answer, bool proceeds)
    {
        var c = new Fake("1", "6", "TEST", "Unlimited", answer, "reason", "", "2", "sql");
        string[]? call = null; await Menu(c).Run(a => { call = a; return Task.FromResult(0); });
        Assert.That(call is not null, Is.EqualTo(proceeds));
        Assert.That(c.Output, Does.Contain("Unlimited = no page limit for this customer"));
        if (proceeds) Assert.That(call, Does.Not.Contain("--confirm-blocked"));
        else Assert.That(string.Join("\n", c.Output), Does.Contain("CANCELLED"));
    }
    [TestCase("", "yes", true)][TestCase("", "no", false)][TestCase("100", "", false)]
    public async Task Prepaid_blocking_consent_is_only_offered_without_positive_topup(string pages, string answer, bool blocked)
    {
        var lines = new[] { "1", "6", "TEST", "Prepaid", pages }.Concat(pages.Length == 0 ? new[] { answer } : []).Concat(["reason", "", "2", "sql"]);
        var c = new Fake(lines.ToArray()); string[]? call = null;
        await Menu(c).Run(a => { call = a; return Task.FromResult(0); });
        Assert.That(call, Is.Not.Null); Assert.That(call!.Contains("--confirm-blocked"), Is.EqualTo(blocked));
        Assert.That(c.Output.Any(x => x.StartsWith("Customer may be blocked")), Is.EqualTo(pages.Length == 0));
        OperatorArguments.Parse(call).Page("test");
    }
    [Test]
    public async Task Defaults_invalid_choices_connection_reuse_and_safe_errors()
    {
        var c = new Fake("bad", "1", "bad", "1", "TEST", "SQL01", "", "no", "", "1", "", "", "0", "c", "SQL02", "Other", "yes", "", "1", "1", "", "", "0", "0");
        var calls = new List<string[]>();
        await Menu(c).Run(a => { calls.Add(a); if (calls.Count == 1) throw new Exception("secret connection string"); return Task.FromResult(2); });
        Assert.That(calls.Count, Is.EqualTo(3)); Assert.That(calls[1], Is.EqualTo(calls[0]));
        Assert.That(calls[2], Is.EqualTo(new[] { "pages", "status", "--consumer", "TEST", "--sql-server", "SQL02", "--database", "Other", "--trust-server-certificate" }));
        Assert.That(c.Output.Count(x => x.StartsWith("SQL Server")), Is.EqualTo(2));
        Assert.That(c.Output.Count(x => x == "Invalid choice. Try again."), Is.EqualTo(2));
        Assert.That(string.Join("\n",c.Output), Does.Not.Contain("secret connection"));
        Assert.That(c.Output, Does.Contain("Action ended with exit code 2."));
        Assert.That(c.Output.Count(x => x == "Press Enter to return to the menu"), Is.EqualTo(4));
    }
    [Test]
    public async Task Validation_errors_are_refused_and_required_note_is_reprompted()
    {
        var c = new Fake("1", "2", "TEST", "100", "", "reason", "", "bad", "2", "", "", "0", "0"); string[]? call = null;
        await Menu(c).Run(a => { call = a; throw new ArgumentException("safe validation"); });
        Assert.That(c.Output, Does.Contain("Note is required.").And.Contain("Invalid choice. Try again.").And.Contain("REFUSED: safe validation"));
        var a = OperatorArguments.Parse(call!); Assert.That(a.Get("note"), Is.EqualTo("reason")); Assert.That(a.Has("reference"), Is.False);
        Assert.That(a.Get("generate-sql"), Does.Match(@"\d{8}-\d{6}-topup$"));
        Assert.That(Path.GetDirectoryName(a.Get("generate-sql")), Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","ISTDocAi","sql")));
        Assert.That(c.Output.Any(x => x.StartsWith("SQL Server")), Is.False);
    }
    [TestCase(false)][TestCase(true)]
    public async Task Output_folder_creation_and_overwrite_confirmation(bool confirm)
    {
        var folder = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()); var path = Path.Combine(folder,"nested","docs.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,"");
        var c = new Fake(Connection.Concat(["2", "5","TEST","0",path,confirm ? "yes" : "no"]).ToArray()); var calls = new List<string[]>();
        try
        {
            await Menu(c, Cycles()).Run(a => { calls.Add(a); return Task.FromResult(0); });
            Assert.That(calls.Count, Is.EqualTo(confirm ? 1 : 0)); if (confirm) Assert.That(calls[0], Does.Contain("--force"));
            Assert.That(File.ReadAllBytes(path), Is.Empty);
            var newPath = Path.Combine(folder,"new","statement.pdf");
            var fresh = new Fake(Connection.Concat(["2", "4","TEST",newPath]).ToArray());
            await Menu(fresh).Run(_ => Task.FromResult(0)); Assert.That(Directory.Exists(Path.GetDirectoryName(newPath)), Is.True);
        }
        finally { Directory.Delete(folder,true); }
    }
    [Test]
    public async Task Script_generation_preserves_generator_bytes_and_refuses_existing_files()
    {
        var folder = Path.Combine(Path.GetTempPath(),"sql-menu-" + Guid.NewGuid());
        var c = new Fake("1","2","TEST","100","reason","","2",folder);
        try
        {
            Assert.That(await OperatorCli.Run([],c), Is.Zero);
            var requestId = Guid.Parse(c.Output.Single(x => x.StartsWith("RequestId: ")).Split(' ')[1]);
            var request = OperatorArguments.Parse(["pages","topup","--consumer","TEST","--pages","100","--note","reason","--request-id",requestId.ToString()]).Page(System.Security.Principal.WindowsIdentity.GetCurrent().Name + "@" + Environment.MachineName);
            var files = PageSql.Files(request);
            Assert.That(Directory.GetFiles(folder).Select(Path.GetFileName), Is.EquivalentTo(files.Keys));
            foreach (var (name,sql) in files) { var path = Path.Combine(folder,name); Assert.That(File.ReadAllBytes(path), Is.EqualTo(System.Text.Encoding.UTF8.GetBytes(sql))); Assert.That(c.Output, Does.Contain(path)); }
            Assert.That(c.Output, Does.Contain("Send this folder to IT/DBA: " + folder));
            var retry = new Fake { IsInputRedirected = true };
            Assert.That(await OperatorCli.Run(["pages","topup","--consumer","TEST","--pages","100","--note","reason","--request-id",requestId.ToString(),"--generate-sql",folder],retry), Is.EqualTo(1));
            foreach (var (name,sql) in files) Assert.That(File.ReadAllBytes(Path.Combine(folder,name)), Is.EqualTo(System.Text.Encoding.UTF8.GetBytes(sql)));
            Assert.That(retry.Output.Any(x => x.StartsWith("Send this folder")), Is.False);
            Assert.That(c.Output.Any(x => x.StartsWith("SQL Server")), Is.False);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    [Test]
    public async Task Scripts_omit_saved_connection_and_run_now_reuses_it()
    {
        var c = new Fake(Connection.Concat(["1","2","TEST","100","reason","","2","sql","","5","","","reason","","1"]).ToArray());
        var calls = new List<string[]>(); await Menu(c).Run(a => { calls.Add(a); return Task.FromResult(0); });
        Assert.That(calls.Count, Is.EqualTo(2)); Assert.That(calls[0], Does.Not.Contain("--sql-server")); Assert.That(calls[1], Does.Contain("--sql-server").And.Contain("SQL01").And.Contain("--trust-server-certificate"));
        Assert.That(c.Output.Count(x => x.StartsWith("SQL Server")), Is.EqualTo(1));
    }
    private static IEnumerable<TestCaseData> EofCases => new[] { Array.Empty<string>(), new[] { "1" }, new[] { "1", "2", "TEST" }, new[] { "4", "3" } }.Select(x => new TestCaseData(new object[] { x }));

    [Test]
    public async Task Date_routes_reprompt_zone_and_dates_and_ask_zone_every_time()
    {
        var folder = ModelVerdictTests.Folder();
        try
        {
            // A bad To date or a From/To order error re-asks BOTH dates (the From date may be the wrong one).
            var c = new Fake(Connection.Concat(["2", "1", "TEST", "bad", "1", "bad", "2026-11-01", "bad", "2026-11-01", "2026-10-31", "2026-11-01", "2026-11-15", Path.Combine(folder, "s.pdf"), "", "2", "", "2", "2026-11-01", "2026-11-15", Path.Combine(folder, "d.csv")]).ToArray());
            var calls = new List<string[]>(); await Menu(c).Run(a => { calls.Add(a); return Task.FromResult(0); });
            Assert.That(calls.Count, Is.EqualTo(2));
            Assert.That(OperatorArguments.Parse(calls[0]).Get("time-zone"), Is.EqualTo("egypt"));
            Assert.That(OperatorArguments.Parse(calls[1]).Get("time-zone"), Is.EqualTo("saudi"));
            Assert.That(OperatorArguments.Parse(calls[1]).Get("consumer"), Is.EqualTo("TEST"));
            Assert.That(c.Output.Count(x => x == "Time zone:"), Is.EqualTo(2));
            Assert.That(c.Output, Does.Contain("Dates must use yyyy-MM-dd.").And.Contain("From date must not be later than To date."));
            Assert.That(c.Output, Does.Not.Contain("How do you want to apply this?"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestCase("1", "egypt")][TestCase("2", "saudi")]
    public async Task Date_route_defaults_to_today_in_selected_zone_and_period_filename(string choice, string zone)
    {
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ReportPeriod.Resolve(zone)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var c = new Fake(Connection.Concat(["2", "1", "PHASE2_FIXTURE", choice, today, "", ""]).ToArray());
        string[]? call = null;
        await Menu(c).Run(a => { call = a; return Task.FromResult(0); });
        var args = OperatorArguments.Parse(call!);
        Assert.That(args.Get("to"), Is.EqualTo(today));
        Assert.That(Path.GetFileName(args.Get("pdf")), Is.EqualTo($"PHASE2_FIXTURE-statement-{today}-to-{today}.pdf"));
    }
    [TestCaseSource(nameof(EofCases))]
    public async Task Eof_exits_whole_menu_cleanly(string[] lines)
    {
        Assert.That(await Menu(new Fake(lines)).Run(_ => throw new AssertionException("Unexpected execution")), Is.Zero);
    }
    [Test]
    public async Task Menu_exit_has_no_second_lifetime_pause()
    {
        var c = new Fake("9","","0"); Assert.That(await OperatorCli.Run([],c), Is.Zero); OperatorLifetime.Pause([],true,c);
        Assert.That(c.Reads, Is.EqualTo(3)); Assert.That(c.Output.Count(x => x == "Press Enter to return to the menu"), Is.EqualTo(1));
        Assert.That(c.Output.Any(x => x.Contains("Press Enter to close")), Is.False);
        var other = new Fake(""); OperatorLifetime.Pause(["--help"],true,other); Assert.That(other.Reads, Is.EqualTo(1)); Assert.That(other.Output, Is.EqualTo(new[] {"Press Enter to close"}));
    }
}

internal static class MenuCycleFixture
{
    public static JsonNode WithMore(this JsonNode node, bool more) { node["hasMoreCycles"] = more; return node; }
}

