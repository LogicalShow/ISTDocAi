namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface ICatalogReader { Task<IReadOnlyList<CatalogModel>> ReadAsync(CancellationToken ct); }
