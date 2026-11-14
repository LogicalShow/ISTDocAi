namespace ISTDocAi.Infrastructure.Data.Sql;

internal static class SqlOrderByBuilder
{
    public static string Build(
        string? sortField,
        string? sortOrder,
        IReadOnlyDictionary<string, string> supportedColumns,
        string defaultOrderBy,
        string tieBreaker,
        bool sortDescendingByDefault = false)
    {
        if (string.IsNullOrWhiteSpace(sortField)
            || !supportedColumns.TryGetValue(sortField.Trim(), out var sortColumn))
        {
            return defaultOrderBy;
        }

        var direction = sortOrder?.Trim().ToLowerInvariant() switch
        {
            "asc" => "ASC",
            "desc" => "DESC",
            _ => sortDescendingByDefault ? "DESC" : "ASC"
        };

        return $"{sortColumn} {direction}, {tieBreaker}";
    }
}
