using Microsoft.Data.SqlClient;
using System.Data;

namespace ISTDocAi.Infrastructure.Data.Sql;

internal static class SqlParameterFactory
{
    public static SqlParameter Create(
        string name,
        SqlDbType type,
        object value) =>
        new(name, type)
        {
            Value = value
        };

    public static SqlParameter CreateNullable(
        string name,
        SqlDbType type,
        object? value) =>
        new(name, type)
        {
            Value = value ?? DBNull.Value
        };
}
