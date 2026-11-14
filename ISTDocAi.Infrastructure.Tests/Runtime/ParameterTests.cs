using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Data;
using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class ParameterTests
{
    [Test]
    public void Parameters_preserve_SQL_types_lengths_null_metadata_and_UTC_millisecond_precision()
    {
        var timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.FromHours(2));
        var request = SqlDatabase.Request() with { BlobPath = null };
        using var intake = new SqlCommand();
        ((SqlMapper.IDynamicParameters)new SqlParameters(request)).AddParameters(intake, null!);
        Check(intake, "@jobId", SqlDbType.UniqueIdentifier);
        Check(intake, "@consumerId", SqlDbType.SmallInt);
        Check(intake, "@pageCount", SqlDbType.SmallInt);
        Check(intake, "@fileSha256", SqlDbType.Binary, 32);
        Check(intake, "@clientRequestId", SqlDbType.VarChar, 100);
        Check(intake, "@fileName", SqlDbType.NVarChar, 260);
        Check(intake, "@blobPath", SqlDbType.VarChar, 400);
        Check(intake, "@activeMappingFingerprint", SqlDbType.Char, 64);
        Check(intake, "@activeClassifierModelId", SqlDbType.VarChar, 64);
        Check(intake, "@activeExtractModelId", SqlDbType.VarChar, 64);
        Check(intake, "@expectedRunNo", SqlDbType.SmallInt);
        Assert.Equal(DBNull.Value, intake.Parameters["@blobPath"].Value);
        using var completion = new SqlCommand();
        ((SqlMapper.IDynamicParameters)new SqlParameters(new ExtractionSuccess(request.JobId, Guid.NewGuid(), 123L,
            "bl-v1", 2, timestamp, "raw.json"))).AddParameters(completion, null!);
        Check(completion, "@operationId", SqlDbType.BigInt);
        Check(completion, "@modelId", SqlDbType.VarChar, 64);
        Check(completion, "@azureCompletedAtUtc", SqlDbType.DateTime2);
        Assert.Equal((byte)3, completion.Parameters["@azureCompletedAtUtc"].Scale);
        Assert.Equal(timestamp.UtcDateTime, completion.Parameters["@azureCompletedAtUtc"].Value);
    }
    [Test]
    public void Reporting_parameters_preserve_bigint_ids_and_typed_nulls()
    {
        using var command = new SqlCommand();
        ((SqlMapper.IDynamicParameters)new SqlParameters(new { consumerId=(short)1, grantId=(long?)null, limit=500, afterChargeId=long.MaxValue })).AddParameters(command,null!);
        Check(command,"@grantId",SqlDbType.BigInt); Check(command,"@afterChargeId",SqlDbType.BigInt);
        Check(command,"@limit",SqlDbType.Int); Check(command,"@consumerId",SqlDbType.SmallInt);
        Assert.Equal(DBNull.Value,command.Parameters["@grantId"].Value);
        Assert.Equal(long.MaxValue,command.Parameters["@afterChargeId"].Value);
    }
    [Test]
    public void Reporting_commands_clamp_timeout_and_forward_cancellation()
    {
        var options=Microsoft.Extensions.Options.Options.Create(new DocAiSqlOptions {CommandTimeoutSeconds=900});
        var store=new SqlDocAiStore(new SqlConnectionFactory(options),new SqlStatementCatalog(),options);
        using var stop=new CancellationTokenSource();
        foreach(var label in new[] {"S27","S28"}) {
            var command=(CommandDefinition)typeof(SqlDocAiStore).GetMethod("Command",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!
                .Invoke(store,[label,null,stop.Token])!;
            Assert.Equal(120,command.CommandTimeout); Assert.Equal(stop.Token,command.CancellationToken);
        }
    }
    private static void Check(SqlCommand command, string name, SqlDbType type, int? size = null)
    {
        Assert.Equal(type, command.Parameters[name].SqlDbType);
        if (size.HasValue) Assert.Equal(size.Value, command.Parameters[name].Size);
    }
}
