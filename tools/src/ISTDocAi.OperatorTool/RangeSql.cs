using System.Globalization;

namespace ISTDocAi.OperatorTool;

public static class RangeSql
{
    public static string Generate(string consumer, ReportPeriod period, bool documents = false)
    {
        PageSql.Validate(new PageRequest("Status", consumer));
        return SqlResources.Expand(documents ? "DocumentsRange.sql" : "StatementRange.sql", new()
        {
            ["$consumer"] = consumer,
            ["$fromUtc"] = period.FromUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            ["$toExclusiveUtc"] = period.ToExclusiveUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)
        });
    }
}
