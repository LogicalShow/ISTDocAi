using Azure.Core;
using Azure.Identity;
using ISTDocAi.Provisioning;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ISTDocAi.OperatorTool;

public static class ModelReviewCli
{
    public static async Task<int> Run(OperatorArguments a, IProvisionConsole c)
    {
        Validate(a);
        try
        {
            var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions { TenantId = a.Has("tenant") ? AzureIdentity.GuidValue(a.Get("tenant")) : null });
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(40));
            var account = await credential.AuthenticateAsync(new TokenRequestContext([DocumentModelClient.Scope]), deadline.Token);
            if (a.Has("tenant") && !string.Equals(account.TenantId, a.Get("tenant"), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Signed-in tenant differs from requested tenant.");
            if (!a.Has("json")) c.WriteLine("Signed in: " + account.Username + "; tenant: " + account.TenantId);
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };
            return await Execute(a, c, new DocumentModelClient(http, credential, a.Required("endpoint")), async () =>
            {
                var interactive = !Redirected(c);
                var server = a.Has("sql-server") ? a.Get("sql-server") : interactive ? CliPrompts.Ask(c, "SQL Server") : throw new ArgumentException("--sql-server required with redirected input.");
                var database = a.Get("database", "ISTDocAi");
                await using var sql = new SqlRunner(server, database, a.Has("trust-server-certificate"));
                var row = (await sql.Execute(ModelUpgradeSql.Active(a.Get("document-type-code"), a.Get("shipping-line")))).Single().Single();
                return ((JsonObject)JsonNode.Parse((string)row["SnapshotJson"]!)!, (string)row["SNAPSHOT_TOKEN"]!);
            }, deadline.Token);
        }
        catch (AuthenticationFailedException) { throw new ActionNeededException("Browser sign-in failed; upgrade refused."); }
        catch (OperationCanceledException) { throw new ActionNeededException("Azure is unreachable; upgrade refused."); }
    }
    public static bool Redirected(IProvisionConsole c) => c is IOperatorConsole input ? input.IsInputRedirected : Console.IsInputRedirected;
    public static void Validate(OperatorArguments a)
    {
        if (a.Required("document-type-code") is not ("BL" or "MANIFEST" or "AUTO")) throw new ArgumentException("Document type must be BL, MANIFEST or AUTO.");
        ModelSql.Identifier(a.Required("model-id")); DocumentModelClient.ValidateEndpoint(a.Required("endpoint"));
        if (a.Has("tenant")) AzureIdentity.GuidValue(a.Get("tenant"));
        if (a.Has("shipping-line")) ModelSql.Identifier(a.Get("shipping-line"), 20);
        if (a.Get("document-type-code") == "AUTO" && a.Has("shipping-line")) throw new ArgumentException("AUTO has no shipping line.");
        if (a.Has("output")) ValidateFolder(a.Get("output"));
    }
    public static string ValidateFolder(string path)
    {
        var folder = Path.GetFullPath(path);
        if (File.Exists(folder) || Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()) throw new ArgumentException("Output folder must be new or empty.");
        // Refuse reparse points to keep output confined to the chosen physical folder.
        for (var parent = new DirectoryInfo(folder); parent is not null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Output folder cannot use reparse points.");
        return folder;
    }
    public static async Task<int> Execute(OperatorArguments a, IProvisionConsole c, DocumentModelClient client, Func<Task<(JsonObject Snapshot, string Token)>> snapshot, CancellationToken ct = default)
    {
        Validate(a); var doc = a.Get("document-type-code"); var id = a.Get("model-id"); var auto = doc == "AUTO";
        var model = await client.Fetch(id, auto, a.Get("doc-type"), ct);
        if (a.Action == "inspect")
        {
            if (a.Has("json")) c.WriteLine(model.ToJsonString());
            else
            {
                foreach (var key in new[] { auto ? "classifierId" : "modelId", "createdDateTime", "expirationDateTime", "apiVersion", "buildMode" }) c.WriteLine(key + ": " + (model[key]?.ToString() ?? "unavailable"));
                if (auto) c.WriteLine("Classes: " + string.Join(", ", model["docTypes"]!.AsObject().Select(p => p.Key)));
                else PrintTree(c, DocumentModelClient.Fields(model), "");
            }
            return 0;
        }
        var folder = ValidateFolder(a.Get("output", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ISTDocAi", "models", DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + id)));
        var (active, token) = await snapshot();
        if (active["Model"] is not JsonObject oldModel) throw new ArgumentException("No active model in this group.");
        if (oldModel["ModelId"]?.ToString() == id) throw new ArgumentException("New model ID must differ from the active model.");
        var redirected = Redirected(c);
        string? Choose(string name, string[] options)
        {
            if (redirected) return null;
            c.WriteLine(name + ": " + string.Join(", ", options) + (auto ? "" : "; new canonical identifier, or ignore"));
            c.Write("Choice: "); return c.ReadLine();
        }
        DraftResult result;
        if (auto)
        {
            var oldLine = active["ClassifierClasses"]!.AsArray().SingleOrDefault(r => r?["TargetDocumentTypeCode"]?.ToString() == "BL")?["ShippingLineCode"]?.ToString() ?? "";
            string? line = null;
            if (!redirected) { c.Write("Bill shipping line [" + oldLine + "] (enter to keep; - to clear): "); var answer = c.ReadLine(); line = string.IsNullOrEmpty(answer) ? oldLine : answer == "-" ? "" : answer; }
            result = ModelDraft.Classifier(active, token, model["docTypes"]!.AsObject(), id, Choose, line);
        }
        else
        {
            var old = await client.Fetch(oldModel["ModelId"]!.ToString(), false, a.Get("doc-type"), ct, allowExpired: true);
            var reference = "";
            if (!redirected) { c.Write("ChangeReference (required): "); reference = c.ReadLine() ?? ""; }
            result = ModelDraft.Extract(active, token, DocumentModelClient.Fields(old), DocumentModelClient.Fields(model), id, reference, Choose);
        }
        // Redirected input always produces review only, even if every row could be carried.
        var usable = !redirected && result.Draft is not null;
        result.Review["UsableDraft"] = usable;
        result.Review["AzureModel"] = model.DeepClone();
        result.Review["ActiveSnapshot"] = active.DeepClone();
        result.Review["DocumentTypeCode"] = doc;
        result.Review["ReviewFolder"] = folder;
        var text = result.Review.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        ValidateFolder(folder); Directory.CreateDirectory(folder);
        void Write(string name, string content) { using var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write); using var writer = new StreamWriter(stream); writer.Write(content); }
        Write("review.json", text); Write("review.txt", "DRAFT REVIEW\n" + text);
        c.WriteLine(text);
        if (!usable) { c.WriteLine("Review only: unresolved or blocked items; no usable draft written."); return 2; }
        var path = Path.Combine(folder, auto ? "draft-classifier.json" : "draft-mapping.json");
        Write(Path.GetFileName(path), result.Draft!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        string Q(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var next = auto ? "models upgrade-classifier --model-id " + id + " --test-accuracy <measured> --min-confidence <reviewed> " + string.Join(" ", result.Draft.Select(p => "--" + p.Key + " " + Q(p.Value!.ToString())).Where(s => !s.EndsWith("\"\"", StringComparison.Ordinal))) : "models upgrade-extract --document-type-code " + doc + " --model-id " + id + " --test-field-accuracy <measured> --mapping-file " + Q(path) + (a.Has("shipping-line") ? " --shipping-line " + a.Get("shipping-line") : "");
        c.WriteLine(next + " --expected-snapshot " + token);
        c.WriteLine("Test accuracy must be the model team's measured value."); return 0;
    }
    private static void PrintTree(IProvisionConsole c, JsonObject fields, string indent)
    {
        foreach (var field in fields) PrintNode(c, field.Key, field.Value!.AsObject(), indent);
    }
    private static void PrintNode(IProvisionConsole c, string name, JsonObject schema, string indent)
    {
        c.WriteLine(indent + name + ": " + schema["type"] + "; confidence: " + ModelDraft.Confidence(schema));
        if (schema["properties"] is JsonObject properties) PrintTree(c, properties, indent + "  ");
        if (schema["items"] is JsonObject item) PrintNode(c, "items", item, indent + "  ");
    }
}