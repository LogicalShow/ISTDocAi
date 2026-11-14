using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

public sealed class DatabaseInitializationException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
