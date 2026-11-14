namespace ISTDocAi.Application.Common.Interfaces;

public interface IUnitOfWork : IDisposable
{
    void ClearTrackedEntities(CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
