using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Data;

public static class DatabaseConnection
{
    public static void Validate(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(database) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("DefaultConnection must name an application database; system databases are not allowed.");
    }
}
