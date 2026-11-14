namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record Consumer(short ConsumerId, string Code, string TenantCode, byte[] ApiKeyHash1,
    byte[]? ApiKeyHash2, short MaxPagesPerJob, int DailyPageCap, bool HasCallback);
