using System.Globalization;
using System.Text.RegularExpressions;

namespace ISTDocAi.OperatorTool;

public static class ModelUpgradeSql
{
    public const string ChargingNote = "Activation does not re-read documents by itself. A later re-upload of a document read by the old model is re-read with the new model and charged, unless page quota suppresses the re-read.";
    private static string Group(string doc, string line)
    {
        if (doc is not ("BL" or "MANIFEST" or "AUTO")) throw new ArgumentException("Document type must be BL, MANIFEST or AUTO.");
        if (line.Length > 0) ModelSql.Identifier(line, 20);
        if (doc == "AUTO" && line.Length > 0) throw new ArgumentException("AUTO has no shipping line.");
        return $"DocumentTypeCode='{doc}' AND Kind='{(doc == "AUTO" ? "classify" : "extract")}' AND ShippingLineCode {(line.Length == 0 ? "IS NULL" : "='" + line + "'")}";
    }
    private static string Transaction(string body, bool preview = false) => "SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRY BEGIN TRANSACTION;\n" + body + (preview ? "\nROLLBACK;" : "\nCOMMIT;") + "\nEND TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;";
    private static string Exec(string body) => "EXEC sys.sp_executesql N'" + SqlResources.Escape(body) + "';\n";
    private static string Number(decimal value) => value is >= 0 and <= 1 ? value.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("Accuracy/confidence must be between 0 and 1.");
    private static void Token(string token)
    {
        if (!Regex.IsMatch(token, "\\A[0-9A-Fa-f]{64}\\z")) throw new ArgumentException("--expected-snapshot requires a SHA-256 token from models active.");
    }
    // SQL JSON property order, BIN2 row order, explicit nulls, UTF-16LE bytes; no observation timestamp.
    public static string SnapshotBody(string doc, string line = "")
    {
        var group = Group(doc, line);
        return SqlResources.Read("ModelSnapshot.sql").Replace("$group", group, StringComparison.Ordinal)
            .Replace("$documentType", doc, StringComparison.Ordinal).Replace("$shippingLine", line, StringComparison.Ordinal);
    }
    public static string Active(string doc, string line = "") => Transaction(SnapshotBody(doc, line) + "\nSELECT @snapshot SnapshotJson,@snapshotToken SNAPSHOT_TOKEN;");

    public static string Generate(OperatorArguments a, bool preview = false, string reviewedPreview = "", bool script = false)
    {
        if (a.Action == "active") return Active(a.Required("document-type-code"), a.Get("shipping-line"));
        var classifier = a.Action == "upgrade-classifier";
        if (!classifier && a.Action != "upgrade-extract") throw new ArgumentException("Unknown upgrade action.");
        var doc = classifier ? "AUTO" : a.Required("document-type-code");
        if (!classifier && doc is not ("BL" or "MANIFEST")) throw new ArgumentException("Extract type must be BL or MANIFEST.");
        var line = a.Get("shipping-line"); var group = Group(doc, line);
        var id = a.Required("model-id"); ModelSql.Identifier(id);
        var expected = a.Required("expected-snapshot"); Token(expected); expected = expected.ToUpperInvariant();
        var accuracyName = classifier ? "test-accuracy" : "test-field-accuracy"; a.Required(accuracyName);
        if (classifier) a.Required("min-confidence");
        var accuracy = Number(a.Decimal(accuracyName, 0));
        var confidence = Number(a.Decimal(classifier ? "min-confidence" : "min-field-confidence", .8m));
        // Same activation gates as register-set: BL 0.95; MANIFEST extract and AUTO classifier use the router gate 0.6.
        var gate = Number(a.Decimal(classifier ? "router-minimum-accuracy" : "minimum-accuracy", doc == "BL" ? .95m : .6m));
        var evidence = a.Get("evidence");
        if (evidence.Length > 400 || evidence.Any(char.IsControl) || evidence.Contains("$(", StringComparison.Ordinal)) throw new ArgumentException("Evidence requires at most 400 printable characters without SQLCMD substitutions.");
        string prepare;
        if (!classifier) prepare = Exec(ModelSql.Binding(id, doc, File.ReadAllText(a.Required("mapping-file"))));
        else
        {
            var classes = new[] { a.Required("bill-class-name"), a.Required("manifest-class-name"), a.Required("other-class-name") };
            foreach (var name in classes) ModelSql.Identifier(name);
            if (classes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3) throw new ArgumentException("Class names must be distinct.");
            var billLine = a.Get("bill-shipping-line"); if (billLine.Length > 0) ModelSql.Identifier(billLine, 20);
            prepare = $"INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode) VALUES('{id}','{classes[0]}','BL',{(billLine.Length == 0 ? "NULL" : "'" + billLine + "'")}),('{id}','{classes[1]}','MANIFEST',NULL),('{id}','{classes[2]}',NULL,NULL);";
        }
        var mutation = $"""
            IF EXISTS(SELECT 1 FROM dbo.ModelRegistry WITH(UPDLOCK,HOLDLOCK) WHERE ModelId='{id}')
                THROW 50018,'New model ID already exists; use a new ID.',1;
            DECLARE @version int;
            SELECT @version=ISNULL(MAX(CONVERT(int,Version)),0)+1 FROM dbo.ModelRegistry WITH(UPDLOCK,HOLDLOCK) WHERE {group};
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status,TestFieldAccuracy,MinFieldConfidence)
            VALUES('{id}','{doc}',{(line.Length == 0 ? "NULL" : "'" + line + "'")},'{(classifier ? "classify" : "extract")}',@version,'Testing',{accuracy},{confidence});
            {prepare}
            EXEC sys.sp_executesql N'{SqlResources.Escape(SqlResources.Activation)}',N'@newModelId varchar(64),@minimumAccuracy decimal(5,4)',@newModelId='{id}',@minimumAccuracy={gate};
            """;
        // Include the entire desired mutation in the preview token so a changed mapping/route cannot reuse approval.
        var payload = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(mutation + evidence)));
        var check = script ? "IF @OperatorConfirmed<>1 OR @token<>@ReviewedPreviewToken THROW 50018,'Review preview and confirm before execution.',1;" : reviewedPreview.Length > 0 ? $"IF @token<>'{CheckedPreview(reviewedPreview)}' THROW 50018,'Preview changed; review again.',1;" : "";
        var body = SnapshotBody(doc, line) + $"""

            IF @snapshotToken<>'{expected}' THROW 50018,'Active model changed since review. Run models active again.',1;
            DECLARE @token varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),@snapshot+N'{payload}')),2);
            {check}
            SELECT @snapshot BeforeSnapshot,'{id}' NewModelId,{gate} MinimumAccuracy,N'{SqlResources.Escape(evidence)}' Evidence,'{ChargingNote}' ChargingNote;
            {mutation}
            {Exec(SnapshotBody(doc, line) + "\nSELECT @snapshot AfterSnapshot;")}
            SELECT 'PREVIEW_TOKEN='+@token PreviewToken;
            """;
        return Transaction(body, preview);
    }
    private static string CheckedPreview(string token) { Token(token); return token; }
    public static Dictionary<string, string> Files(OperatorArguments a) => a.Action == "active"
        ? new() { ["models-active.sql"] = Generate(a) }
        : new()
        {
            ["model-upgrade-preview.sql"] = Generate(a, true),
            ["model-upgrade.sql"] = "-- Review preview, paste PREVIEW_TOKEN and explicitly confirm.\nDECLARE @ReviewedPreviewToken varchar(64)=''; DECLARE @OperatorConfirmed bit=0;\n" + Generate(a, script: true)
        };
}
