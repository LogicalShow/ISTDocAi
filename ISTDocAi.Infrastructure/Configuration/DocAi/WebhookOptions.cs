namespace ISTDocAi.Infrastructure.Configuration.DocAi;

public sealed class WebhookOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxParallelSends { get; set; } = 4;
}
