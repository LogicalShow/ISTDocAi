using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ISTDocAi.Infrastructure.Configuration.DocAi;
namespace ISTDocAi.Infrastructure.Data.Sql.DocAi;

public sealed class SqlConnectionFactory(IOptions<DocAiSqlOptions> options) : ISqlConnectionFactory
{
    public SqlConnection Open()
    {
        var settings = new SqlConnectionStringBuilder(options.Value.ConnectionString)
        { ConnectTimeout = options.Value.CommandTimeoutSeconds };
        var connection = new SqlConnection(settings.ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(options.Value.ConnectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
}
