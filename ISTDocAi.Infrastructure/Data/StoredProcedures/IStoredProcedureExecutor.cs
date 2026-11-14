namespace ISTDocAi.Infrastructure.Data.StoredProcedures;

/// <summary>
/// Low-level stored procedure access for custom repositories; application code should use repository abstractions.
/// </summary>
public interface IStoredProcedureExecutor
{
    /// <summary>
    /// Executes a stored procedure and maps its first result set to <typeparamref name="TResult"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var rows = await executor.QueryAsync&lt;MaintenanceRequestRow&gt;(
    ///     "dbo.usp_MaintenanceRequestReport",
    ///     [
    ///         new("@EquipmentId", equipmentId, DbType.String),
    ///         new("@RequestStatus", requestStatus, DbType.Int32),
    ///         new("@RequestIssueDateFrom", issueDateFrom, DbType.DateTime2)
    ///     ],
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<IReadOnlyList<TResult>> QueryAsync<TResult>(
        string procedureName,
        IReadOnlyCollection<StoredProcedureParameter> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a stored procedure that does not return a result set.
    /// </summary>
    Task<int> ExecuteAsync(
        string procedureName,
        IReadOnlyCollection<StoredProcedureParameter> parameters,
        CancellationToken cancellationToken = default);
}
