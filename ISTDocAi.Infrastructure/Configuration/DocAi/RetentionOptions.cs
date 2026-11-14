namespace ISTDocAi.Infrastructure.Configuration.DocAi;

public sealed class RetentionOptions
{
    public bool Enabled { get; set; } = true;
    public TimeSpan RunAtUtc { get; set; } = TimeSpan.FromHours(2);
}
