using System.Globalization;
using System.Text.Json.Nodes;
using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

public sealed partial class OperatorMenu
{
    // Tests inject the Azure/SQL draft boundary; production uses the phase B authenticated path.
    public Func<OperatorArguments, IProvisionConsole, Task<int>> DraftRunner { get; init; } = ModelReviewCli.Run;
    public Func<string> ReviewFolder { get; init; } = () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ISTDocAi", "models", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
    private string? endpoint;
    private string tenant = "";

    private async Task Upgrade(Func<string[], Task<int>> command)
    {
        var doc = Required("Document type (BL/MANIFEST/AUTO)").ToUpperInvariant();
        if (doc is not ("BL" or "MANIFEST" or "AUTO")) throw new ArgumentException("Document type must be BL, MANIFEST or AUTO.");
        var id = Required("New model id"); ModelSql.Identifier(id);
        if (endpoint is null)
        {
            var next = Required("Document Intelligence endpoint"); DocumentModelClient.ValidateEndpoint(next);
            var nextTenant = CliPrompts.Ask(console, "Tenant id (optional)");
            if (nextTenant.Length > 0) AzureIdentity.GuidValue(nextTenant);
            endpoint = next; tenant = nextTenant;
        }
        var line = doc == "AUTO" ? "" : CliPrompts.Ask(console, "Shipping line (optional; empty = default group)");
        if (line.Length > 0) ModelSql.Identifier(line, 20);
        if (server is null) Connection();
        var folder = ReviewFolder();
        var values = new Dictionary<string, string> { ["document-type-code"] = doc, ["model-id"] = id, ["endpoint"] = endpoint, ["output"] = folder, ["sql-server"] = server!, ["database"] = database };
        if (TrustServerCertificate == true) values["trust-server-certificate"] = "true";
        if (tenant.Length > 0) values["tenant"] = tenant;
        if (line.Length > 0) values["shipping-line"] = line;
        var exit = await DraftRunner(new("models", "draft", values), console);
        console.WriteLine("Review folder: " + folder);
        if (exit != 0) { console.WriteLine("REFUSED: Azure/draft review did not produce a usable draft. Run the review again."); return; }
        console.WriteLine("Review/edit the draft, then press Enter");
        if (console.ReadLine() is null) throw new CliCancelled();
        await FinishUpgrade(command, doc, id, line, folder);
    }

    private async Task ApplyReview(Func<string[], Task<int>> command)
    {
        var folder = Required("Review folder");
        var review = ReadReview(folder);
        var doc = ModelVerdict.S(review["ActiveSnapshot"], "DocumentTypeCode");
        var line = ModelVerdict.S(review["ActiveSnapshot"], "ShippingLineCode");
        await FinishUpgrade(command, doc, ModelVerdict.S(review, "NewModelId"), line, folder);
    }

    private async Task FinishUpgrade(Func<string[], Task<int>> command, string doc, string id, string line, string folder)
    {
        var final = ModelCheck.Read(doc, folder);
        ModelCheck.Write(final, folder, console);
        if (final.Verdict.Colour == "RED") { console.WriteLine("REFUSED: RED verdict; activation is refused."); return; }
        if (final.Verdict.Colour == "YELLOW")
        {
            console.Write("Acknowledge the missing kit keys above: type yes: ");
            if (console.ReadLine() != "yes") throw new CliCancelled();
        }
        else if (final.Verdict.Items.Any(i => i.Colour is "BLUE" or "WHITE")) CliPrompts.Confirm(console, false);
        var draft = final.Draft!;
        var measuredText = Required("Measured test accuracy (0..1)");
        var numberArgs = new OperatorArguments("models", "", new() { ["accuracy"] = measuredText });
        var measured = numberArgs.Decimal("accuracy", 0);
        var evidence = Required("Evidence reference"); UpgradeAccuracy.Evidence(evidence);
        var estimate = UpgradeAccuracy.Estimate(doc, draft, final.Review);
        console.WriteLine("Measured: " + measured.ToString(CultureInfo.InvariantCulture) + "; Azure estimate: " + (estimate?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"));
        if (UpgradeAccuracy.Gap(measured, estimate))
        {
            console.WriteLine("WARNING: measured accuracy and Azure estimate differ by more than 5 percentage points. Continue?");
            CliPrompts.Confirm(console, false);
        }
        var confidence = doc == "AUTO" ? Required("Minimum classifier confidence (0..1)", "0.5") : null;
        if (confidence is not null) new OperatorArguments("models", "", new() { ["confidence"] = confidence }).Decimal("confidence", 0);
        console.WriteLine("Old model: " + ModelVerdict.S(final.Review, "OldModelId") + "; new model: " + id + "; verdict: " + final.Verdict.Colour);
        console.WriteLine(ModelUpgradeSql.ChargingNote);
        var apply = ApplyChoice(retryInvalid: false);
        if (ModelVerdict.S(final.Review, "NewModelId") != id) throw new ArgumentException("Review model ID changed; run the review again.");
        var args = new List<string> { "models", doc == "AUTO" ? "upgrade-classifier" : "upgrade-extract", "--model-id", id, "--expected-snapshot", ModelVerdict.S(final.Review, "SNAPSHOT_TOKEN"), "--evidence", evidence, doc == "AUTO" ? "--test-accuracy" : "--test-field-accuracy", measured.ToString(CultureInfo.InvariantCulture) };
        void Add(string key, string value) { args.Add("--" + key); args.Add(value); }
        if (doc == "AUTO")
        {
            foreach (var name in ModelVerdict.RouteNames) Add(name, ModelVerdict.S(draft, name));
            if (ModelVerdict.S(draft, "bill-shipping-line").Length > 0) Add("bill-shipping-line", ModelVerdict.S(draft, "bill-shipping-line"));
            Add("min-confidence", confidence!);
        }
        else
        {
            // Freeze the reviewed bytes; edits while answering later prompts cannot change the activation payload.
            var path = Path.Combine(folder, "reviewed-mapping.json");
            File.WriteAllText(path, draft.ToJsonString());
            Add("mapping-file", path); Add("document-type-code", doc);
            if (line.Length > 0) Add("shipping-line", line);
        }
        if (apply == "2") Add("generate-sql", Required("Output folder", Path.Combine(folder, "sql")));
        else { if (server is null) Connection(); Add("sql-server", server!); Add("database", database); if (TrustServerCertificate == true) args.Add("--trust-server-certificate"); }
        // Validate the exact payload even when a test command runner replaces execution.
        ModelUpgradeSql.Generate(OperatorArguments.Parse(args.ToArray()));
        var result = await command(args.ToArray());
        if (result != 0) console.WriteLine("Action ended with exit code " + result + ".");
    }
}
