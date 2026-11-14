namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record UnpricedUsage(string MeterCode, DateTimeOffset FirstUsageAtUtc, int UnpricedRows, int UnpricedPages);
