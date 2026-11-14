using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ISTDocAi.Infrastructure.Data;

public class Repository<T> : IRepository<T> where T : Entity
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    private IQueryable<T> ApplyIncludes(IQueryable<T> query, IEnumerable<Expression<Func<T, object>>>? includes)
    {
        if (includes != null)
        {
            query = includes.Aggregate(query, (current, include) => current.Include(include));
        }
        return query;
    }
    private static IQueryable<T> ApplyIncludes(
    IQueryable<T> query,
    IEnumerable<string>? includePaths)
    {
        if (includePaths is null) return query;
        foreach (var path in includePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
                query = query.Include(path.Trim());
        }
        return query;
    }
    public async Task<T?> GetAsyncWithThenIncludeOnly(
    string entityId,
    IEnumerable<string>? includePaths,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false,
    bool useSplitQuery = false)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        // optional: avoid cartesian explosion when including many paths
        if (useSplitQuery) query = query.AsSplitQuery();

        query = ApplyIncludes(query, includePaths);
        if (ignoreFilters) query = query.IgnoreQueryFilters();
        return await query.FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);
    }
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>The entity if found, or null if not found.</returns>
    public async Task<T?> GetAsync(
        string entityId, 
        IEnumerable<Expression<Func<T, object>>>? includes = null,
        CancellationToken cancellationToken = default, bool ignoreFilters = false)
    {
        var query = _dbSet.AsNoTracking();
        query = ApplyIncludes(query, includes);
        if (ignoreFilters)
        {
            query = query.IgnoreQueryFilters();
        }
        return await query.FirstOrDefaultAsync(e => e.Id.Equals(entityId), cancellationToken);
    }


    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>The entity if found, or null if not found.</returns>
    public async Task<T?> GetAsyncWithTracking(
        string entityId,
        IEnumerable<Expression<Func<T, object>>>? includes = null,
        CancellationToken cancellationToken = default, bool ignoreFilters = false)
    {
        var query = _dbSet.AsTracking();
        query = ApplyIncludes(query, includes);
        if (ignoreFilters)
        {
            query = query.IgnoreQueryFilters();
        }
        return await query.FirstOrDefaultAsync(e => e.Id.Equals(entityId), cancellationToken);
    }


    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="includePaths">Optional collection of navigation property paths to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>The entity if found, or null if not found.</returns>
    public async Task<T?> GetAsyncWithTracking(
        string entityId,
        IEnumerable<string>? includePaths = null,
        CancellationToken cancellationToken = default, bool ignoreFilters = false)
    {
        var query = _dbSet.AsTracking();

        // Apply string-based includes
        if (includePaths != null)
        {
            foreach (var includePath in includePaths)
            {
                query = query.Include(includePath);
            }
        }

        if (ignoreFilters)
        {
            query = query.IgnoreQueryFilters();
        }
        return await query.FirstOrDefaultAsync(e => e.Id.Equals(entityId), cancellationToken);
    }


    /// <summary>
    /// Retrieves a single entity by its identifier and projects it into a DTO using the provided selector.
    /// </summary>
    /// <typeparam name="TDto">The type of the projection result (DTO).</typeparam>
    /// <param name="entityId">The identifier of the entity to retrieve.</param>
    /// <param name="selector">An expression specifying how to project the entity into <typeparamref name="TDto"/>.</param>
    /// <param name="includePaths">Optional collection of navigation property paths to include.</param>
    /// <param name="cancellationToken">A token for cancelling the async operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters when retrieving the entity.</param>
    /// <returns>
    /// A projected <typeparamref name="TDto"/> instance if found; otherwise <c>null</c>.
    /// </returns>
    public async Task<TDto?> GetAsync<TDto>(
    string entityId,
    Expression<Func<T, TDto>> selector,
    IEnumerable<string>? includePaths = null,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false)
    where TDto : class
    {
        var query = _dbSet.AsNoTracking();

        // Apply string-based includes
        if (includePaths != null)
        {
            foreach (var includePath in includePaths)
            {
                query = query.Include(includePath);
            }
        }

        if (ignoreFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Where(e => e.Id.Equals(entityId))
            .Select(selector)   // use input projection
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a single entity by ID with optional eager loading using string paths and projects to DTO.
    /// Returns null if entity not found.
    /// </summary>
    /// <typeparam name="TDto">DTO type that implements IProjectable interface</typeparam>
    /// <param name="entityId">The entity identifier</param>
    /// <param name="includePaths">Optional list of navigation property paths (e.g., "Department.Company")</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <param name="ignoreFilters">If true, bypasses EF Core global query filters</param>
    /// <returns>Projected DTO or null if not found</returns>
    public async Task<TDto?> GetAsync<TDto>(
        string entityId,
        IEnumerable<string>? includePaths = null,
        CancellationToken cancellationToken = default,
        bool ignoreFilters = false)
    where TDto : class, IProjectable<T, TDto>
    {
        var query = _dbSet.AsNoTracking();

        // Apply string-based includes
        if (includePaths != null)
        {
            foreach (var includePath in includePaths)
            {
                query = query.Include(includePath);
            }
        }

        if (ignoreFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Where(e => e.Id.Equals(entityId))
            .Select(TDto.Selector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a single entity by ID and projects it directly into a DTO using the provided selector.
    /// 
    /// Notes:
    /// - This method is intended for read-only projections (DTOs), not for returning tracked entities.
    /// - Navigation properties referenced inside the projection are automatically translated into
    ///   the necessary JOINs by EF Core. No <see cref="Include"/> calls are required or applied.
    /// - If <paramref name="ignoreFilters"/> is true, global query filters will be bypassed.
    /// - Returns <c>null</c> if no matching entity is found.
    /// </summary>
    public async Task<TDto?> GetAsync<TDto>(
    string entityId,
    Expression<Func<T, TDto>> selector,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (ignoreFilters)
            query = query.IgnoreQueryFilters();

        return await query
            .Where(e => e.Id == entityId)
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }


    /// <summary>
    /// Retrieves all and optionally sorted entities.
    /// </summary>
    /// <param name="orderBy">An optional sorting expression.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of all entities.</returns>
    public async Task<List<T>> GetAllAsync(
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        IEnumerable<Expression<Func<T, object>>>? includes = null,
        CancellationToken cancellationToken = default, bool IgnoreFilters = false)
    {
        var query = _dbSet.AsNoTracking();
        query = ApplyIncludes(query, includes);
        
        if (orderBy != null)
        {
            query = orderBy(query);
        }
        if (IgnoreFilters)
        {
            query = query.IgnoreQueryFilters();
        }
        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new entity to the database.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Updates an existing entity in the database.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        _dbSet.Update(entity);
        ConfigureOriginalRowVersion(entity);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes an entity from the database.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public Task DeleteAsync(T entity, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(entity);

        if (entry.State == EntityState.Detached)
            _dbSet.Attach(entity);

        ConfigureOriginalRowVersion(entity);

        entry.State = EntityState.Deleted;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity to delete.</param>
    /// <param name="RowVersion">The RowVersion value to ensure the entity has not been modified by another process.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public async Task DeleteAsync(string entityId, byte[] RowVersion, CancellationToken cancellationToken)
    {
        var entity = Activator.CreateInstance<T>();
        entity.Id = entityId;

        _dbSet.Attach(entity);
        ConfigureOriginalRowVersion(entity, RowVersion);

        _dbSet.Remove(entity);

        await Task.CompletedTask;
    }

    public Task BulkDeleteAsync(IEnumerable<string> entityIds, CancellationToken cancellationToken)
    {
        var entities = entityIds.Select(entityId =>
        {
            var entity = Activator.CreateInstance<T>();
            entity.Id = entityId;
            return entity;
        }).ToList();

        foreach (var entity in entities)
        {
            _dbSet.Attach(entity);
            ConfigureOriginalRowVersion(entity);
            _dbSet.Remove(entity);
        }

        return Task.CompletedTask;
    }

    public Task BulkDeleteAsync(IEnumerable<(string Id, byte[] RowVersion)> items, CancellationToken cancellationToken)
    {
        foreach (var (id, rowVersion) in items)
        {
            var entity = Activator.CreateInstance<T>();
            entity.Id = id;

            _dbSet.Attach(entity);
            ConfigureOriginalRowVersion(entity, rowVersion);

            _dbSet.Remove(entity);
        }

        return Task.CompletedTask;
    }

    private void ConfigureOriginalRowVersion(T entity, byte[]? suppliedRowVersion = null)
    {
        if (entity is not ConcurrentEntity concurrentEntity)
            return;

        var rowVersion = suppliedRowVersion ?? concurrentEntity.RowVersion;

        if (rowVersion is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"A RowVersion is required to update or delete {typeof(T).Name}.");
        }

        concurrentEntity.RowVersion = rowVersion;

        var property = _context.Entry(entity)
            .Property(nameof(ConcurrentEntity.RowVersion));

        property.OriginalValue = rowVersion;
        property.IsModified = false;
    }

    public IQueryable<T> Query(bool ignoreFilters = false, bool asNoTracking = true)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
            query = query.AsNoTracking();

        if (ignoreFilters)
            query = query.IgnoreQueryFilters();

        return query;
    }

    
  /// <summary>
    /// Executes a LINQ query asynchronously and returns the results as a list.
    /// This method allows you to build a query using LINQ and execute it asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of the query result elements.</typeparam>
    /// <param name="queryBuilder">A function that builds the query from the base IQueryable.</param>
    /// <param name="ignoreFilters">If true, bypasses global query filters.</param>
    /// <param name="asNoTracking">If true (default), returns AsNoTracking().</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of results from the query execution.</returns>
    public async Task<List<TResult>> QueryAsync<TResult>(
        Func<IQueryable<T>, IQueryable<TResult>> queryBuilder,
        bool ignoreFilters = false,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
            query = query.AsNoTracking();

        if (ignoreFilters)
            query = query.IgnoreQueryFilters();

        var finalQuery = queryBuilder(query);
        return await finalQuery.ToListAsync(cancellationToken);
    }
}
