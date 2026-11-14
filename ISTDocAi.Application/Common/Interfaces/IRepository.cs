using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Application.Common.Interfaces;

public interface IRepository<T> where T : Entity
{
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>The entity if found, or null if not found.</returns>
    Task<T?> GetAsync(
        string entityId,
        IEnumerable<Expression<Func<T, object>>>? includes = null,
        CancellationToken cancellationToken = default,bool ignoreFilters = false);

    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>The entity if found, or null if not found.</returns>
    Task<T?> GetAsyncWithTracking(
    string entityId,
    IEnumerable<Expression<Func<T, object>>>? includes = null,
    CancellationToken cancellationToken = default, bool ignoreFilters = false);


    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="includePaths">Optional collection of navigation property paths to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>The entity if found, or null if not found.</returns>
Task<T?> GetAsyncWithTracking(
        string entityId,
        IEnumerable<string>? includePaths = null,
        CancellationToken cancellationToken = default, bool ignoreFilters = false);

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
    Task<TDto?> GetAsync<TDto>(
    string entityId,
    Expression<Func<T, TDto>> selector,
    IEnumerable<string>? includePaths = null,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false)  where TDto : class;

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
    Task<TDto?> GetAsync<TDto>(
        string entityId,
        IEnumerable<string>? includePaths = null,
        CancellationToken cancellationToken = default,
        bool ignoreFilters = false) where TDto : class, IProjectable<T, TDto>;


    /// <summary>
    /// Retrieves all and optionally sorted entities.
    /// </summary>
    /// <param name="orderBy">An optional sorting expression.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of all entities.</returns>
    Task<List<T>> GetAllAsync(
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        IEnumerable<Expression<Func<T, object>>>? includes = null,
        CancellationToken cancellationToken = default, bool IgnoreFilters = false);


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
    Task<TDto?> GetAsync<TDto>(
    string entityId,
    Expression<Func<T, TDto>> selector,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false);
    

    /// <summary>
    /// Adds a new entity to the database.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    Task AddAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing entity in the database.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    Task UpdateAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an entity from the database.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    Task DeleteAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity to delete.</param>
    /// <param name="RowVersion">The RowVersion value to ensure the entity has not been modified by another process.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    Task DeleteAsync(string entityId, byte[] RowVersion, CancellationToken cancellationToken);


    Task BulkDeleteAsync(IEnumerable<string> entityIds, CancellationToken cancellationToken = default);
    Task BulkDeleteAsync(IEnumerable<(string Id, byte[] RowVersion)> items, CancellationToken cancellationToken = default);
    Task<T?> GetAsyncWithThenIncludeOnly(string entityId, IEnumerable<string>? includePaths, CancellationToken cancellationToken = default, bool ignoreFilters = false, bool useSplitQuery = false);
    /// <summary>
    /// Returns an IQueryable over T for advanced LINQ scenarios.
    /// </summary>
    /// <param name="ignoreFilters">If true, bypasses global query filters.</param>
    /// <param name="asNoTracking">If true (default), returns AsNoTracking().</param>
    IQueryable<T> Query(bool ignoreFilters = false, bool asNoTracking = true);

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
    Task<List<TResult>> QueryAsync<TResult>(
        Func<IQueryable<T>, IQueryable<TResult>> queryBuilder,
        bool ignoreFilters = false,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

}
