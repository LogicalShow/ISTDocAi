using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Data.Sql.DocAi;

// Explicit SQL metadata prevents provider inference (including for nulls and short strings).
internal sealed class SqlParameters : SqlMapper.IDynamicParameters
{
    private readonly List<SqlParameter> parameters = [];
    public SqlParameters(object? values)
    {
        if (values is null) return;
        foreach (var property in values.GetType().GetProperties())
        {
            var name = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            var value = property.GetValue(values);
            var (type, size, precision, scale) = Metadata(name);
            if (value is DateTimeOffset timestamp) value = timestamp.UtcDateTime;
            parameters.Add(new SqlParameter("@" + name, type, size)
            {
                Value = value ?? DBNull.Value, Precision = precision, Scale = scale
            });
        }
    }
    public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
    }
    private static (SqlDbType Type, int Size, byte Precision, byte Scale) Metadata(string name) => name switch
    {
        "checkId" or "jobId" or "leaseToken" or "eventId" => (SqlDbType.UniqueIdentifier, 0, 0, 0),
        "expectedRunNo" or "consumerId" or "runNo" or "pageCount" or "flaggedFields" or "statusCode" => (SqlDbType.SmallInt, 0, 0, 0),
        "grantId" or "afterChargeId" or "operationId" or "deliveryId" => (SqlDbType.BigInt, 0, 0, 0),
        "limit" or "months" or "fileSizeBytes" or "pages" or "retryAfterSeconds" or "delaySeconds" => (SqlDbType.Int, 0, 0, 0),
        "reprocess" or "countsAsAttempt" => (SqlDbType.Bit, 0, 0, 0),
        "fileSha256" => (SqlDbType.Binary, 32, 0, 0),
        "startedAtUtc" or "checkedAtUtc" or "asOfUtc" or "azureCompletedAtUtc" => (SqlDbType.DateTime2, 0, 0, 3),
        "minimumAccuracy" => (SqlDbType.Decimal, 0, 5, 4),
        "classConfidence" => (SqlDbType.Decimal, 0, 4, 3),
        "expectedDocumentType" or "expectedLine" or "resolvedDocumentTypeCode" or "documentTypeCode" or "requestedLine" or "idemLine" or "resolvedLine" => (SqlDbType.VarChar, 20, 0, 0),
        "mappingFingerprint" or "activeMappingFingerprint" => (SqlDbType.Char, 64, 0, 0),
        "outcome" => (SqlDbType.VarChar, 16, 0, 0),
        "reasonCode" => (SqlDbType.VarChar, 64, 0, 0),
        "resultType" => (SqlDbType.VarChar, 32, 0, 0),
        "kind" or "state" => (SqlDbType.VarChar, 10, 0, 0),
        "activeClassifierModelId" or "activeExtractModelId" or "modelId" or "classifierModelId" or "newModelId" or "className" or "correlationId" => (SqlDbType.VarChar, 64, 0, 0),
        "requestId" or "clientRequestId" or "contentType" => (SqlDbType.VarChar, 100, 0, 0),
        "blobPath" or "rawResultPath" => (SqlDbType.VarChar, 400, 0, 0),
        "fileName" => (SqlDbType.NVarChar, 260, 0, 0),
        "operationLocation" or "error" => (SqlDbType.NVarChar, 500, 0, 0),
        "errorCode" or "azureErrorCode" => (SqlDbType.VarChar, 50, 0, 0),
        // S9/S18 intentionally truncate messages in SQL, so bind the full input as nvarchar(max).
        "errorMessage" or "resultJson" or "payloadJson" => (SqlDbType.NVarChar, -1, 0, 0),
        _ => throw new ArgumentException($"No SQL metadata defined for {name}.")
    };
}

internal sealed class UtcTimestampHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTime date => new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)),
        DateTimeOffset offset => offset.ToUniversalTime(),
        _ => throw new DataException("Expected a SQL UTC timestamp.")
    };
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.DbType = DbType.DateTime2;
        parameter.Value = value.UtcDateTime;
    }
}
