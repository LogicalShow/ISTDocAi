using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ISTDocAi.Infrastructure.Configuration.DocAi;
namespace ISTDocAi.Infrastructure.Data.Sql.DocAi;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
    // Synchronous consumers must use a real synchronous connection, never wait on OpenAsync.
    SqlConnection Open() => throw new NotSupportedException("Synchronous SQL connections are not supported.");
}
