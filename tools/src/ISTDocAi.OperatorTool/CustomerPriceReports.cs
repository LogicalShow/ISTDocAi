using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISTDocAi.Provisioning;
using ISTDocAi.ReportPdf;

namespace ISTDocAi.OperatorTool;

public sealed record CustomerPriceLine(string Time, string Utc, string DocumentType, long Pages, decimal Amount, string JobId, long Run);
public sealed record CustomerPriceDay(string Date, long Documents, long Pages, decimal Amount);
public sealed record CustomerPriceReport(string Period, string UtcPeriod, string ZoneName, string Rate, string Currency,
    long Documents, long Pages, decimal Amount, IReadOnlyList<CustomerPriceDay> Daily, IReadOnlyList<CustomerPriceLine> Lines);

public static class CustomerPriceReports
{
    public const string Subtitle = "Amounts at the rate entered for this statement; not a tax invoice.";
    public const string RoundingNote = "Each document amount = pages multiplied by the rate, rounded to 2 decimals away from zero. Total and daily amounts sum the rounded document amounts.";
    public static decimal Price(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) || price <= 0 || price > 100000 ||
            (value.Contains('.') && value.Length - value.IndexOf('.') - 1 > 4))
            throw new ArgumentException("Price per page must be greater than 0 and at most 100000, with at most 4 decimal places (invariant decimal only).");
        return price;
    }
    public static string Currency(string value) => value.ToUpperInvariant() is var code && code is "EGP" or "USD" or "SAR" ? code : throw new ArgumentException("Currency must be EGP, USD or SAR.");
    public static string Rate(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);
    public static string Money(decimal value, string currency) => value.ToString("F2", CultureInfo.InvariantCulture) + " " + currency;
    // Free text keeps the existing customer restrictions; the currency is supplied separately from the validated enum.
    public static string Clean(string value) => Regex.Replace(new string(value.Select(c => c is >= ' ' and <= '\u00ff' ? c : ' ').ToArray()), @"\$|USD|Azure|cost|price|meter|ledger|PRIVATE_NOTE|PRIVATE_OPERATOR", "[omitted]", RegexOptions.IgnoreCase);
    public static string Sql(string consumer, ReportPeriod period) => RangeSql.Generate(consumer, period, documents: true);
    public static void Reconcile(CustomerPriceReport report)
    {
        if (report.Daily.Sum(d => d.Amount) != report.Amount || report.Daily.Sum(d => d.Pages) != report.Pages || report.Daily.Sum(d => d.Documents) != report.Documents)
            throw new ArgumentException("Priced statement does not reconcile; no file written.");
    }
    public static async Task<CustomerPriceReport> Collect(ReportPeriod period, decimal price, string currency,
        Func<Func<Dictionary<string, object?>, Task>, Task> read, int limit = 10000, bool json = true, Func<CustomerPriceLine, Task>? write = null)
    {
        price = Price(price.ToString(CultureInfo.InvariantCulture)); currency = Currency(currency);
        var lines = new List<CustomerPriceLine>(); var daily = new SortedDictionary<string, CustomerPriceDay>(StringComparer.Ordinal);
        long count = 0, pages = 0; decimal amount = 0;
        await read(async raw =>
        {
            var row = RangeReports.DocumentRow(raw);
            var utc = RangeReports.Utc(row["recordedAtUtc"]);
            var n = long.Parse(row["pages"]!.ToString(), CultureInfo.InvariantCulture);
            if (n < 0) throw new ArgumentException("Invalid charged pages.");
            // Round each charged run before summing: rounding the grand total instead does not reconcile to the lines.
            var lineAmount = decimal.Round(checked(n * price), 2, MidpointRounding.AwayFromZero);
            var line = new CustomerPriceLine(period.LocalTime(utc), utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Clean(row["resolvedDocumentTypeCode"]?.ToString() ?? ""), n, lineAmount,
                Clean(row["jobId"]?.ToString() ?? ""), long.Parse(row["runNo"]!.ToString(), CultureInfo.InvariantCulture));
            count = checked(count + 1); pages = checked(pages + n); amount = checked(amount + lineAmount);
            if (json && count > limit) throw new ArgumentException("More than 10000 documents; use --csv.");
            if (lines.Count < limit) lines.Add(line);
            var date = line.Time[..10]; daily.TryGetValue(date, out var d);
            daily[date] = new(date, checked((d?.Documents ?? 0) + 1), checked((d?.Pages ?? 0) + n), checked((d?.Amount ?? 0) + lineAmount));
            if (write is not null) await write(line);
        });
        var result = new CustomerPriceReport(period.LocalLine, period.UtcLine, period.ZoneName, Rate(price), currency, count, pages, amount, daily.Values.ToArray(), lines);
        Reconcile(result); return result;
    }
    private static string TextCell(string text) => Reports.CsvCell(text.Length > 0 && text[0] is '-' or '\n' ? "'" + text : text);
    public static async Task<CustomerPriceReport> CsvFile(string path, bool force, ReportPeriod period, decimal price, string currency, Func<Func<Dictionary<string, object?>, Task>, Task> read)
    {
        var full = Path.GetFullPath(path);
        if (File.Exists(full) && !force) throw new ArgumentException("Existing files require --force.");
        var temporary = Path.Combine(Path.GetDirectoryName(full)!, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            CustomerPriceReport result;
            await using (var writer = new StreamWriter(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(false)))
            {
                writer.NewLine = "\r\n";
                await writer.WriteLineAsync(string.Join(',', new[] { "Time (" + period.ZoneName + ")", "Time (UTC)", "Document type", "Pages", "Price per page", "Currency", "Amount", "Job id", "Run" }.Select(TextCell)));
                result = await Collect(period, price, currency, read, 0, false, line => writer.WriteLineAsync(string.Join(',', new[] { TextCell(line.Time), TextCell(line.Utc), TextCell(line.DocumentType), line.Pages.ToString(CultureInfo.InvariantCulture), Rate(price), Currency(currency), line.Amount.ToString("F2", CultureInfo.InvariantCulture), TextCell(line.JobId), line.Run.ToString(CultureInfo.InvariantCulture) })));
                await writer.WriteLineAsync($"TOTAL,,,{result.Pages.ToString(CultureInfo.InvariantCulture)},,{result.Currency},{result.Amount.ToString("F2", CultureInfo.InvariantCulture)},,");
            }
            File.Move(temporary, full, force); return result;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static byte[] Pdf(CustomerPriceReport report, string company, string consumer)
    {
        Reconcile(report);
        using var buffer = new MemoryStream();
        ReportRenderer.Render(JsonSerializer.Serialize(new { report, companyName = Clean(company), consumerCode = Clean(consumer) }), "Customer", "PriceRange", buffer);
        return buffer.ToArray();
    }
    public static async Task<int> Run(OperatorArguments a, IProvisionConsole console, bool? trust)
    {
        var period = ReportPeriod.Parse(a.Required("from"), a.Required("to"), a.Required("time-zone"));
        var price = Price(a.Required("price-per-page")); var currency = Currency(a.Required("currency"));
        var query = Sql(a.Required("consumer"), period);
        var server = a.Has("sql-server") ? a.Get("sql-server") : CliPrompts.Ask(console, "SQL Server");
        var database = a.Has("database") ? a.Get("database") : CliPrompts.Ask(console, "Database", "ISTDocAi");
        SqlRunner.ValidateNames(server, database);
        var trusted = trust ?? (a.Has("trust-server-certificate") || CliPrompts.Ask(console, "TrustServerCertificate? yes/no", "no").Equals("yes", StringComparison.OrdinalIgnoreCase));
        await using var sql = new SqlRunner(server, database, trusted);
        var report = a.Has("csv") ? await CsvFile(a.Get("csv"), a.Has("force"), period, price, currency, callback => sql.Stream(query, callback)) :
            await Collect(period, price, currency, callback => sql.Stream(query, callback), a.Has("pdf") ? 1000 : 10000, !a.Has("pdf"));
        if (a.Has("pdf"))
        {
            // Same default as the other customer PDFs: the consumer's registered name (code already regex-validated by Sql()).
            var company = a.Get("company-name");
            if (company.Length == 0)
            {
                var rows = await sql.Execute($"SELECT Name FROM dbo.Consumers WHERE Code='{a.Required("consumer")}';");
                company = rows.SelectMany(x => x).SelectMany(x => x.Values).FirstOrDefault()?.ToString() ?? a.Required("consumer");
            }
            var bytes = Pdf(report, company, a.Required("consumer"));
            using var file = new FileStream(a.Get("pdf"), a.Has("force") ? FileMode.Create : FileMode.CreateNew, FileAccess.Write); file.Write(bytes);
        }
        else if (!a.Has("csv"))
        {
            // JSON mode prints only the JSON document so it can be piped or parsed.
            console.WriteLine(JsonSerializer.Serialize(report));
            return 0;
        }
        console.WriteLine(report.Period); console.WriteLine(report.UtcPeriod);
        console.WriteLine($"Rate: {report.Rate} {report.Currency} per page");
        console.WriteLine($"Documents: {report.Documents}; Pages: {report.Pages}; Total amount: {Money(report.Amount, report.Currency)}");
        return 0;
    }
}
