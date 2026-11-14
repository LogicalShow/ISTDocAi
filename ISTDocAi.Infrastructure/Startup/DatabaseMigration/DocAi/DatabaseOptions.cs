using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

/// <summary>DocAi:Database settings. Startup uses a separate migration connection when configured.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "DocAi:Database";
    public bool MigrateOnStartup { get; set; } = true;
    public bool AllowDatabaseCreation { get; set; } = true;
    /// <summary>Null/empty means use ConnectionStrings:DefaultConnection during startup.</summary>
    public string? MigrationConnectionString { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 300;
    public int InitializationTimeoutSeconds { get; set; } = 900;
}
