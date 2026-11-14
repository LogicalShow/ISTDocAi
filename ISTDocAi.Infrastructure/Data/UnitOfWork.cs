using Microsoft.EntityFrameworkCore.Storage;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Infrastructure.Data;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction != null) await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction != null) await _transaction.RollbackAsync(cancellationToken);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
    }

    public  void ClearTrackedEntities(CancellationToken cancellationToken)
    {
        if (_transaction != null)
        {
            try { _transaction.Rollback(); } catch { }
            _transaction.Dispose();
            _transaction = null;
        }

        context.ChangeTracker.Clear();
    }
}
