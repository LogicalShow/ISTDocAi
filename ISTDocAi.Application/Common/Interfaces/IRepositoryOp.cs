using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;
using System.Linq.Expressions;
using ISTDocAi.Application.Common.SharedModels;

namespace ISTDocAi.Application.Common.Interfaces;

public interface IRepositoryOp<T> where T : Entity
{
    /// <summary>
    /// Gets the row count of entities based on the specified condition.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The count of entities matching the condition.</returns>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken,bool IgnoreFilters = false);

    /// <summary>
    /// Checks if any entity exists matching the specified condition.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>True if any entity matches the condition, otherwise false.</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken, bool ignoreFilters = false);

    /// <summary>
    /// Gets the first entity in the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The first entity, or null if none found.</returns>
    Task<T?> GetFirstAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the first entity matching the specified condition.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The first entity matching the condition, or null if none found.</returns>
    Task<T?> GetFirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the first entity matching the specified condition,
    /// including the specified navigation properties.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="includes">Navigation properties to include.</param>
    /// <returns>The first entity matching the condition, or null if none found.</returns>
    Task<T?> GetFirstAsync(
     Expression<Func<T, bool>> predicate,
     CancellationToken cancellationToken = default,
     params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Retrieves a single value from an entity that matches the specified predicate.
    /// This method efficiently projects a single property or computed value without retrieving the entire entity.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to retrieve.</typeparam>
    /// <param name="predicate">An expression that defines the condition to match the entity.</param>
    /// <param name="selector">An expression that specifies which value to extract from the matched entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <param name="ignoreFilters">If true, bypasses global query filters applied to the entity type.</param>
    /// <returns>
    /// The selected value from the first matching entity, or <c>null</c> if no entity matches the predicate
    /// or if the selected value is null.
    /// </returns>
    Task<TValue?> GetFirstAsync<TValue>(
    Expression<Func<T, bool>> predicate,
    Expression<Func<T, TValue>> selector,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false);

    /// <summary>
    /// Gets the last entity matching the specified condition.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="orderBy">The key selector to determine the order.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The last entity matching the condition, or null if none found.</returns>
    Task<T?> GetLastAsync<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderBy, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the last entity matching the specified condition,
    /// including the specified navigation properties.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="orderBy">The key selector to determine the order.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="includes">Navigation properties to include.</param>
    /// <returns>The last entity matching the condition, or null if none found.</returns>
    Task<T?> GetLastAsync<TKey>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TKey>> orderBy,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Gets the first entity matching the specified condition
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="orderBy">The key selector to determine the order.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <param name="selector">An expression that specifies which value to extract from the matched entity.</param>
    /// <param name="ignoreFilters">If true, bypasses global query filters applied to the entity type.</param>
    /// <param name="includes">Navigation properties to include.</param>
    /// <returns>The first entity matching the condition, or null if none found.</returns>
    Task<TValue?> GetFirstAsync<TValue>(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy, Expression<Func<T, TValue>> selector, CancellationToken cancellationToken = default, bool ignoreFilters = false,params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Retrieves the last entity matching the specified condition and projects it to the specified type.
    /// This method efficiently projects a single property or computed value without retrieving the entire entity.
    /// </summary>
    /// <typeparam name="TKey">The type of the property used for ordering.</typeparam>
    /// <typeparam name="TValue">The type of the value to retrieve.</typeparam>
    /// <param name="predicate">An expression that defines the condition to match the entity.</param>
    /// <param name="orderBy">The key selector to determine the order (descending).</param>
    /// <param name="selector">An expression that specifies which value to extract from the matched entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <param name="ignoreFilters">If true, bypasses global query filters applied to the entity type.</param>
    /// <returns>
    /// The selected value from the last matching entity, or <c>null</c> if no entity matches the predicate.
    /// </returns>
    Task<TValue?> GetLastAsync<TKey, TValue>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TKey>> orderBy,
        Expression<Func<T, TValue>> selector,
        CancellationToken cancellationToken = default,
        bool ignoreFilters = false);

    /// <summary>
    /// Retrieves a filtered and optionally sorted list of entities.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="orderBy">An optional sorting expression.</param>
    /// <param name="includes">Optional navigation properties to include.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An enumerable of filtered and optionally sorted entities.</returns>
    Task<IEnumerable<T>> GetListAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated, filtered, and optionally sorted list of entities along with the total count of matching entities.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="orderBy">An optional sorting expression.</param>
    /// <param name="pageNo">The page number (1-based).</param>
    /// <param name="pageRowCount">The number of rows per page.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="GetListResult{T}"/> containing the paged items and the total count of matching entities.</returns>
    Task<GetListResult<T>> GetListAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageNo = 1,
        int pageRowCount = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a filtered, optionally included, and optionally ordered paged list of entities
    /// from the database without projection, and returns both the items and the total count.
    /// </summary>
    /// <param name="predicate">An expression to filter the entities.</param>
    /// <param name="includes">A collection of related navigation properties to include in the query.</param>
    /// <param name="orderBy">An optional function to order the query results.</param>
    /// <param name="pageNo">The current page number (1-based).</param>
    /// <param name="pageRowCount">The number of rows per page.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <param name="ignoreFilters">Whether to ignore global query filters applied to the entity type.</param>
    /// <returns>
    /// A task representing the asynchronous operation, containing a <see cref="GetListResult{T}"/> 
    /// with the paged entities and the total count.
    /// </returns>
    Task<GetListResult<T>> GetListAsyncIncludes(
        Expression<Func<T, bool>> predicate,
        IEnumerable<Expression<Func<T, object>>>? includes = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageNo = 1,
        int pageRowCount = 10,
        CancellationToken cancellationToken = default,
        bool ignoreFilters = false);


    /// <summary>
    /// Retrieves a paginated list of entities with optional filtering, eager loading, and custom projection to DTOs.
    /// Executes as a single optimized read-only database query with automatic total count calculation.
    /// </summary>
    /// <typeparam name="TDto">The target DTO type for projection</typeparam>
    /// <param name="predicate">Optional filter condition for entities (WHERE clause)</param>
    /// <param name="selector">Required projection expression to map entity to DTO</param>
    /// <param name="includePaths">Optional navigation property paths for eager loading (e.g., "Department.Company")</param>
    /// <param name="orderBy">Optional sorting logic to apply before pagination</param>
    /// <param name="pageNo">Page number (1-based indexing)</param>
    /// <param name="pageRowCount">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <param name="ignoreFilters">If true, bypasses EF Core global query filters</param>
    /// <returns>Paginated result containing projected DTOs and total count</returns>
    Task<GetListResult<TDto>> GetListAsyncIncludes<TDto>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TDto>> selector,
        IEnumerable<string>? includePaths = null,              
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageNo = 1,
        int pageRowCount = 10,
        CancellationToken cancellationToken = default,
        bool ignoreFilters = false) where TDto : class;


    /// <summary>
    /// Retrieves a paginated list of <typeparamref name="TDto"/> results from the database 
    /// with support for filtering, ordering, and eager-loading related entities.
    /// </summary>
    /// <typeparam name="TDto">
    /// The DTO type that implements <see cref="IProjectable{T, TDto}"/> and 
    /// provides a projection expression (<c>Selector</c>) for mapping from <typeparamref name="T"/>.
    /// </typeparam>
    /// <param name="predicate">
    /// Optional filter expression to restrict the query results (WHERE clause).
    /// </param>
    /// <param name="includePaths">
    /// Optional list of navigation property paths (e.g., "Orders.Customer") to include 
    /// for eager-loading related entities.
    /// </param>
    /// <param name="orderBy">
    /// Optional function to apply ordering to the query.
    /// </param>
    /// <param name="pageNo">
    /// The current page number (1-based).
    /// </param>
    /// <param name="pageRowCount">
    /// The number of records per page.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe while waiting for the task to complete.
    /// </param>
    /// <param name="ignoreFilters">
    /// If true, disables global query filters defined on the entity.
    /// </param>
    Task<GetListResult<TDto>> GetListAsyncIncludes<TDto>(
        Expression<Func<T, bool>>? predicate,
        IEnumerable<string>? includePaths = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageNo = 1,
        int pageRowCount = 10,
        CancellationToken cancellationToken = default,
        bool ignoreFilters = false) where TDto : class, IProjectable<T, TDto>;


    Task<GetListResult<T>> GetListAsyncThenIncludes(
    Expression<Func<T, bool>> predicate,
    IEnumerable<Expression<Func<T, object>>>? includes = null,
    IEnumerable<string>? includePaths = null,             // <-- add
    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
    int pageNo = 1,
    int pageRowCount = 10,
    CancellationToken cancellationToken = default,
    bool ignoreFilters = false);
}
