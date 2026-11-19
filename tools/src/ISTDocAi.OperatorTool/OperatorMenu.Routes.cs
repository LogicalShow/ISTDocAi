using System.Globalization;
using System.Text.Json.Nodes;
using ISTDocAi.Provisioning;

namespace ISTDocAi.OperatorTool;

public sealed partial class OperatorMenu
{
    private static readonly string[] Submenus = [
        "Pages & balance\n1) Balance\n2) Top up pages\n3) Adjust pages (+/-)\n4) Reverse a top-up\n5) Change warning percent\n6) Change mode (Prepaid/Unlimited)\n0) Back",
        "Customer reports\n1) Customer statement by dates (PDF)\n2) Customer documents by dates (CSV)\n3) Customer priced statement by dates (PDF or CSV)\n4) Customer statement by top-up (PDF)\n5) Customer documents by top-up (CSV)\n6) Customer report from the service API (needs kit secrets file)\n0) Back",
        "IST reports\n1) Azure cost per page by dates (PDF or CSV)\n2) IST internal report by top-up (PDF)\n3) IST documents with Azure cost by top-up (CSV)\n4) Azure prices (read only)\n0) Back",
        "Models\n1) Show active models\n2) Upgrade a model (guided)\n3) Inspect an Azure model (read only)\n4) Draft a mapping only (no activation)\n5) Re-check an edited draft\n6) Apply a reviewed upgrade (advanced)\n7) Replace a model mapping (advanced)\n8) Register first BL model (new server only)\n9) Register first model set (new server only)\n0) Back",
        "Setup & administration\n1) Create Azure identity\n2) Renew Azure identity\n3) Remove old Azure certificate\n4) Azure infrastructure\n5) Export operator SQL templates\n0) Back"
    ];

    public Func<string, Task<JsonNode>>? CycleReader { get; init; }

    private async Task<JsonNode> ReadCycles()
    {
        if (CycleReader is not null) return await CycleReader(consumer);
        await using var sql = new SqlRunner(server!, database, TrustServerCertificate == true);
        return await sql.Report(new PageRequest("Cycles", consumer) { Customer = true, GrantId = 0 });
    }

    private async Task<string?> PickGrant(bool documents)
    {
        JsonNode report;
        try
        {
            if (server is null) Connection();
            report = await ReadCycles();
        }
        catch (CliCancelled) { throw; }
        // Business refusals from the cycles statement (e.g. 50018 consumer not found) are safe and final; do not offer manual entry.
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number is >= 50000 and <= 51999) { throw new ArgumentException(e.Message); }
        catch (Exception)
        {
            console.WriteLine("Could not read top-ups. Enter the grant id manually (or prelude for documents).");
            return Required(documents ? "Grant id (or prelude)" : "Grant id");
        }
        var cycles = (report["cycles"] as JsonArray ?? []).OrderByDescending(x => DateTimeOffset.Parse(x!["startAtUtc"]!.ToString(), CultureInfo.InvariantCulture)).ToArray();
        if (cycles.Length == 0 && !documents) { console.WriteLine("No top-ups to reverse."); return null; }
        var prelude = documents && (report["prelude"] is not null || cycles.Length == 0);
        if (prelude) console.WriteLine("0) Prelude (before the first top-up)");
        for (var i = 0; i < cycles.Length; i++)
        {
            var row = cycles[i]!;
            var date = DateTimeOffset.Parse(row["startAtUtc"]!.ToString(), CultureInfo.InvariantCulture).UtcDateTime;
            console.WriteLine($"{i + 1}) {date:yyyy-MM-dd HH:mm} UTC  +{row["topUpPages"]} pages  (grant {row["topUpGrantId"]})" + (row["isReversed"]?.GetValue<bool>() == true ? "  [REVERSED]" : ""));
        }
        var more = report["hasMoreCycles"]?.GetValue<bool>() == true;
        if (more) console.WriteLine("Showing the latest 200 top-ups.");
        while (true)
        {
            // Empty input backs out of the picker without running anything.
            var choice = CliPrompts.Ask(console, "Top-up number (Enter = cancel)").Trim();
            if (choice.Length == 0) throw new CliCancelled();
            if (more && choice.StartsWith("id ", StringComparison.Ordinal)) return choice[3..].Trim();
            if (prelude && choice == "0") return "prelude";
            if (int.TryParse(choice, out var index) && index > 0 && index <= cycles.Length)
            {
                var row = cycles[index - 1]!;
                if (!documents && row["isReversed"]?.GetValue<bool>() == true) { console.WriteLine("Already reversed."); continue; }
                return row["topUpGrantId"]!.ToString();
            }
            console.WriteLine("Invalid choice. Try again.");
        }
    }

    private static JsonNode ReadReview(string folder)
    {
        var path = Path.Combine(Path.GetFullPath(folder), "review.json");
        if (!File.Exists(path)) throw new ArgumentException("No review.json in this folder. Run Draft first and use its review folder.");
        return JsonNode.Parse(File.ReadAllText(path)) ?? throw new ArgumentException("review.json is empty.");
    }

    private void TypedYes(string warning)
    {
        console.WriteLine(warning);
        console.Write("Type yes to continue: ");
        // Deliberately case-sensitive; surrounding whitespace is accepted.
        if (console.ReadLine()?.Trim() != "yes") throw new CliCancelled();
    }

    private string ApplyChoice(bool retryInvalid = true)
    {
        if (retryInvalid) console.WriteLine("How do you want to apply this?");
        console.WriteLine("1) Run it now on the SQL Server");
        console.WriteLine("2) Create a SQL script for IT to run (no database connection)");
        while (true)
        {
            var choice = Required("Choice");
            if (retryInvalid) choice = choice.Trim();
            if (choice is "1" or "2") return choice;
            // Preserve the guided upgrade's original refusal rather than changing its prompt contract.
            if (!retryInvalid) throw new ArgumentException("Choose 1 or 2.");
            console.WriteLine("Invalid choice. Try again.");
        }
    }

    private void SqlArguments(List<string> args, bool offerScript)
    {
        if (offerScript && ApplyChoice() == "2")
        {
            args.AddRange(["--generate-sql", Path.GetFullPath(Required("Output folder", SqlFolder(args[1])))]);
            return;
        }
        if (server is null) Connection();
        args.AddRange(["--sql-server", server!, "--database", database]);
        if (TrustServerCertificate == true) args.Add("--trust-server-certificate");
    }

    private static string SqlFolder(string suffix) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ISTDocAi", "sql", $"{DateTime.Now:yyyyMMdd-HHmmss}-{suffix}");

    private void OutputFile(List<string> args, string extension, string name, bool dated = true)
    {
        var safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        var path = Required("Output file", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ISTDocAi", safe + (dated ? $"-{DateTime.Today:yyyy-MM-dd}" : "") + "." + extension));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path)) { console.WriteLine("File exists. Overwrite?"); CliPrompts.Confirm(console, false); args.Add("--force"); }
        args.AddRange(["--" + extension, path]);
    }

    private ReportPeriod AskPeriod()
    {
        console.WriteLine("Time zone:"); console.WriteLine("1) Egypt"); console.WriteLine("2) Saudi Arabia");
        string zone;
        while (true)
        {
            var selected = Required("Choice").Trim();
            if (selected is "1" or "2") { zone = selected == "1" ? "egypt" : "saudi"; break; }
            console.WriteLine("Invalid choice. Try again.");
        }
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ReportPeriod.Resolve(zone)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ReportPeriod period;
        while (true)
        {
            var from = Required("From date (yyyy-MM-dd)");
            try { ReportPeriod.Parse(from, from, zone); }
            catch (ArgumentException e) { console.WriteLine(e.Message); continue; }
            var to = Required("To date (yyyy-MM-dd)", today);
            // A From/To pair error (order or span) may be the From date's fault, so both are asked again.
            try { period = ReportPeriod.Parse(from, to, zone); break; }
            catch (ArgumentException e) { console.WriteLine(e.Message); }
        }
        return period;
    }

    private async Task<string[]?> Build(string section, string choice)
    {
        var args = new List<string>();
        void Add(string key, string value) => args.AddRange(["--" + key, value]);
        void Need(string key, string label) => Add(key, Required(label));
        void Optional(string key, string? label = null) { var value = CliPrompts.Ask(console, label ?? key + " (optional)"); if (value.Length > 0) Add(key, value); }
        bool Yes(string label) => CliPrompts.Ask(console, label, "no").Equals("yes", StringComparison.OrdinalIgnoreCase);
        if (section == "3" && choice == "4")
        {
            args.AddRange(["prices", "show"]); SqlArguments(args, false); return args.ToArray();
        }
        if (section == "3" && choice == "1")
        {
            args.AddRange(["report", "azure-cost"]);
            var selectedConsumer = CliPrompts.Ask(console, "Consumer code (empty = all customers)").Trim();
            if (selectedConsumer.Length > 0) { PageSql.Validate(new PageRequest("Status", selectedConsumer)); consumer = selectedConsumer; Add("consumer", selectedConsumer); }
            var period = AskPeriod();
            Add("time-zone", period.Zone); Add("from", period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); Add("to", period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            console.WriteLine("Output: 1) PDF 2) CSV");
            string format;
            while (true)
            {
                var selected = Required("Choice").Trim();
                if (selected is "1" or "2") { format = selected == "1" ? "pdf" : "csv"; break; }
                console.WriteLine("Invalid choice. Try again.");
            }
            OutputFile(args, format, $"azure-cost-{(selectedConsumer.Length == 0 ? "all" : selectedConsumer)}-{period.From:yyyy-MM-dd}-to-{period.To:yyyy-MM-dd}", false);
            console.WriteLine(Reports.InternalWarning); SqlArguments(args, false);
        }
        else if (section == "2" && choice == "3")
        {
            consumer = Required("Consumer code", consumer);
            var period = AskPeriod();
            args.AddRange(["report", "customer-price", "--consumer", consumer]);
            Add("time-zone", period.Zone); Add("from", period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); Add("to", period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            string currency;
            console.WriteLine("Currency:"); console.WriteLine("1) EGP"); console.WriteLine("2) USD"); console.WriteLine("3) SAR");
            while (true)
            {
                var selected = Required("Choice").Trim();
                if (selected is "1" or "2" or "3") { currency = new[] { "EGP", "USD", "SAR" }[int.Parse(selected) - 1]; break; }
                console.WriteLine("Invalid choice. Try again.");
            }
            decimal price;
            while (true)
            {
                try { price = CustomerPriceReports.Price(Required("Price per page (" + currency + ")")); break; }
                catch (ArgumentException e) { console.WriteLine(e.Message); }
            }
            Add("currency", currency); Add("price-per-page", CustomerPriceReports.Rate(price));
            console.WriteLine("Output: 1) PDF 2) CSV");
            string format;
            while (true)
            {
                var selected = Required("Choice").Trim();
                if (selected is "1" or "2") { format = selected == "1" ? "pdf" : "csv"; break; }
                console.WriteLine("Invalid choice. Try again.");
            }
            OutputFile(args, format, $"{consumer}-priced-{period.From:yyyy-MM-dd}-to-{period.To:yyyy-MM-dd}", false);
            console.WriteLine($"Rate {CustomerPriceReports.Rate(price)} {currency} per page, {period.From:yyyy-MM-dd}..{period.To:yyyy-MM-dd} ({(period.Zone == "egypt" ? "Egypt" : "Saudi Arabia")} time)");
            SqlArguments(args, false);
        }
        else if (section == "2" && choice is "1" or "2")
        {
            consumer = Required("Consumer code", consumer);
            var period = AskPeriod();
            var zone = period.Zone;
            var action = choice == "1" ? "statement" : "documents-by-date";
            args.AddRange(["pages", action, "--consumer", consumer]);
            Add("time-zone", zone); Add("from", period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); Add("to", period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            OutputFile(args, choice == "1" ? "pdf" : "csv", $"{consumer}-{(choice == "1" ? "statement" : "documents")}-{period.From:yyyy-MM-dd}-to-{period.To:yyyy-MM-dd}", false);
            SqlArguments(args, false);
        }
        else if (section == "1" || section == "2" && choice is "4" or "5" || section == "3")
        {
            consumer = Required("Consumer code", consumer);
            var action = section == "1" ? new[] { "status", "topup", "adjust", "reverse", "set-warning", "set-mode" }[int.Parse(choice) - 1] : section == "2" ? choice == "4" ? "cycles" : "documents" : choice == "2" ? "cycles" : "documents";
            args.AddRange(["pages", action, "--consumer", consumer]);
            if (action is "topup" or "adjust") Need("pages", action == "adjust" ? "Signed pages (+/-)" : "Pages");
            if (action is "reverse" or "documents") { var grant = await PickGrant(action == "documents"); if (grant is null) return null; Add("grant-id", grant); }
            if (action == "set-warning") Add("percent", Required("Warning percent", "10"));
            if (action == "set-mode")
            {
                var mode = Required("Mode (Prepaid/Unlimited)"); Add("mode", mode);
                if (mode.Equals("Unlimited", StringComparison.OrdinalIgnoreCase)) TypedYes("Unlimited = no page limit for this customer");
                else if (mode.Equals("Prepaid", StringComparison.OrdinalIgnoreCase))
                {
                    var pages = CliPrompts.Ask(console, "Top-up pages (optional)");
                    if (pages.Length > 0) Add("top-up-pages", pages);
                    // Balance is unknown here (including script-only use). Consent permits the SQL preview's conditional check.
                    if ((!long.TryParse(pages, out var topup) || topup <= 0) && Yes("Customer may be blocked immediately if no pages are available. Continue? yes/no")) args.Add("--confirm-blocked");
                }
            }
            if (section == "1" && action != "status") { Need("note", "Note"); Optional("reference", "Reference (optional)"); }
            if (action is "adjust" or "reverse" && Yes("Allow negative balance (debt)? yes/no")) args.Add("--allow-debt");
            if (section != "1")
            {
                Add("audience", section == "3" ? "ist" : "customer");
                OutputFile(args, action == "documents" ? "csv" : "pdf", consumer + "-" + action + "-" + (section == "3" ? "ist" : "customer"));
                if (section == "3") console.WriteLine(Reports.InternalWarning);
            }
            SqlArguments(args, section == "1" && action != "status");
        }
        else if (section == "2")
        {
            args.AddRange(["report", "customer"]); Need("base-url", "Base URL"); Need("kit-secrets", "Kit secrets file"); Optional("grant-id", "Grant id (prelude/id/empty)");
            if (Yes("Documents? yes/no")) args.Add("--documents");
            var format = Required("Output format (pdf/csv/json)");
            if (format == "json") args.Add("--json"); else if (format is "pdf" or "csv") OutputFile(args, format, "customer-report"); else throw new ArgumentException("Choose pdf, csv or json.");
            Optional("company-name", "Company name (optional)");
        }
        else if (section == "4")
        {
            var action = choice switch { "1" => "active", "3" => "inspect", "4" => "draft", "5" => "check", "7" => "mapping", "8" => "register-bl", _ => "register-set" };
            args.AddRange(["models", action]);
            if (action is "active" or "inspect" or "draft" or "mapping") Need("document-type-code", "Document type (BL/MANIFEST/AUTO)");
            if (action is "inspect" or "draft" or "mapping" or "register-bl") Need("model-id", "Model id");
            if (action is "inspect" or "draft")
            {
                if (endpoint is null)
                {
                    var nextEndpoint = Required("Document Intelligence endpoint");
                    DocumentModelClient.ValidateEndpoint(nextEndpoint);
                    var nextTenant = CliPrompts.Ask(console, "Tenant id (optional)");
                    if (nextTenant.Length > 0) AzureIdentity.GuidValue(nextTenant);
                    endpoint = nextEndpoint; tenant = nextTenant;
                }
                Add("endpoint", endpoint); if (tenant.Length > 0) Add("tenant", tenant);
                Optional("doc-type");
            }
            if (action is "active" or "draft") Optional("shipping-line", "Shipping line (optional; empty = default group)");
            if (action == "draft") Add("output", Required("Output folder", ReviewFolder()));
            if (action == "check")
            {
                var folder = Required("Review folder"); Add("review", folder);
                var review = ReadReview(folder);
                Add("document-type-code", ModelVerdict.S(review["ActiveSnapshot"], "DocumentTypeCode")); Optional("mapping-file");
            }
            if (action == "mapping") Need("mapping-file", "Mapping file");
            if (action == "register-bl") { Need("test-field-accuracy", "Test accuracy"); foreach (var key in new[] { "minimum-accuracy", "min-field-confidence", "mapping-file" }) Optional(key); }
            if (action == "register-set")
            {
                Need("bl-model-id", "BL model id"); Need("bl-test-field-accuracy", "BL test accuracy");
                foreach (var key in new[] { "bl-minimum-accuracy", "bl-min-field-confidence", "bl-mapping-file", "manifest-model-id", "manifest-test-field-accuracy", "manifest-mapping-file", "manifest-min-field-confidence", "classifier-model-id", "classifier-test-field-accuracy", "classifier-min-confidence", "router-minimum-accuracy", "bill-class-name", "manifest-class-name", "other-class-name", "bill-shipping-line" }) Optional(key);
            }
            if (action is "mapping" or "register-bl" or "register-set") TypedYes(action == "mapping" ? "WARNING: This replaces a live mapping." : "WARNING: Register first models on a new server only.");
            if (action != "inspect" && action != "check") SqlArguments(args, action != "draft");
        }
        else
        {
            if (choice == "5") return ["--export-sql", Path.GetFullPath(Required("Output folder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ISTDocAi", "sql", $"templates-{DateTime.Now:yyyyMMdd-HHmmss}")))];
            args.Add("azure"); if (choice != "4") args.Add("identity");
            args.Add(choice switch { "1" => "create", "2" => "renew", "3" => "remove-old", _ => "infra" });
            if (choice is "2" or "3") Need("client-id", "Client id");
            if (choice == "3") Need("thumbprint", "Thumbprint");
            Optional("tenant");
            if (choice == "1") Add("env", Required("Environment (dev/prod)", "dev"));
            if (choice is "1" or "2") { Optional("name"); if (choice == "1") Optional("client-id"); Optional("output"); }
            if (choice == "3") TypedYes("Verify the replacement on the server first");
        }
        return args.ToArray();
    }
}
