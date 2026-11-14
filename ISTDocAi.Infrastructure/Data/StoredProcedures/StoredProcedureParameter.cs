using System.Data;

namespace ISTDocAi.Infrastructure.Data.StoredProcedures;

public sealed record StoredProcedureParameter(
    string Name,
    object? Value,
    DbType? DbType = null,
    int? Size = null);
