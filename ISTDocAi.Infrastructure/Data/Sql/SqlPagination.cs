using Microsoft.Data.SqlClient;
using System.Data;

namespace ISTDocAi.Infrastructure.Data.Sql;

internal static class SqlPagination
{
    public static SqlParameter[] CreateParameters(
        int pageNumber,
        int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var offset = checked((long)(pageNumber - 1) * pageSize);

        return
        [
            SqlParameterFactory.Create("@Offset", SqlDbType.BigInt, offset),
            SqlParameterFactory.Create("@PageSize", SqlDbType.Int, pageSize)
        ];
    }
}
