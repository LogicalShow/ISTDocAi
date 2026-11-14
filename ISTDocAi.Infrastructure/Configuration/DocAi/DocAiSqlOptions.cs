namespace ISTDocAi.Infrastructure.Configuration.DocAi;

public sealed class DocAiSqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int RetentionTimeoutSeconds { get; set; } = 1800;
}
