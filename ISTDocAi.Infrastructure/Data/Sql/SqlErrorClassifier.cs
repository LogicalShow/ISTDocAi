using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Data.Sql;

public static class SqlErrorClassifier
{
    public static bool IsDuplicateKey(Exception error) => error is SqlException { Number: 2601 or 2627 };
}
