namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record CatalogModel(string DocumentTypeCode, string? ShippingLineCode, string Kind, string ModelId, decimal MinFieldConfidence);
