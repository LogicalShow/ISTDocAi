using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace ISTDocAi.Infrastructure.Data.Sql.DocAi;

/// <summary>Unmodified slices of the tested reference file, including each statement's contract comment.</summary>
public sealed class SqlStatementCatalog
{
    public string Source { get; }
    public IReadOnlyDictionary<string, string> Statements { get; }
    public SqlStatementCatalog()
    {
        using var stream = typeof(SqlStatementCatalog).Assembly.GetManifestResourceStream("DocAi.Statements.sql")
            ?? throw new InvalidOperationException("Embedded SQL reference is missing.");
        using var reader = new StreamReader(stream);
        Source = reader.ReadToEnd();
        var labels = Regex.Matches(Source, @"(?m)^-- \[(S\d+[a-z]?)\]");
        var statements = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < labels.Count; i++)
        {
            var end = i + 1 < labels.Count ? labels[i + 1].Index : Source.Length;
            statements.Add(labels[i].Groups[1].Value, Source[labels[i].Index..end]);
        }
        using var billingStream = typeof(SqlStatementCatalog).Assembly.GetManifestResourceStream("DocAi.Billing.sql")
            ?? throw new InvalidOperationException("Embedded billing reference is missing.");
        using var billingReader = new StreamReader(billingStream);
        var billing = billingReader.ReadToEnd();
        var billingLabels = Regex.Matches(billing, @"(?m)^-- \[(B\d+[a-z]?)\]");
        for (var i = 0; i < billingLabels.Count; i++)
        {
            if (billingLabels[i].Groups[1].Value != "B9") continue;
            var end = i + 1 < billingLabels.Count ? billingLabels[i + 1].Index : billing.Length;
            statements.Add("B9", billing[billingLabels[i].Index..end]);
        }
        if (!statements.ContainsKey("B9")) throw new InvalidOperationException("B9 is missing.");
        Statements = new ReadOnlyDictionary<string, string>(statements);
    }
    public string this[string label] => Statements[label];
}
