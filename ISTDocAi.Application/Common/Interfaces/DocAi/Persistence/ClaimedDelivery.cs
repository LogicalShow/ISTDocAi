namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record ClaimedDelivery(long DeliveryId, Guid LeaseToken, Guid EventId, Guid JobId, short RunNo,
    short ConsumerId, string EventType, string PayloadJson, byte Attempts);
