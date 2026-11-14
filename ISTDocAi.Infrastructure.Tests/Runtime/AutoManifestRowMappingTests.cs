using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Data;
using Dapper;


using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class AutoManifestRowMappingTests
{
    [Test]
    public void Expanded_SQL_rows_map_requested_resolved_types_nullable_lines_and_result_type()
    {
        var options = Options.Create(new DocAiSqlOptions());
        _ = new SqlDocAiStore(new SqlConnectionFactory(options), new SqlStatementCatalog(), options);
        var id = Guid.NewGuid(); var token = Guid.NewGuid();
        var claim = Map<ClaimedJob>(new()
        {
            ["JobId"] = id, ["RunNo"] = (short)1, ["LeaseToken"] = token, ["TakeoverCount"] = (byte)0,
            ["ConsumerId"] = (short)1, ["DocumentTypeCode"] = "AUTO", ["ShippingLineCode"] = null,
            ["Stage"] = "Extract", ["BlobPath"] = "file", ["PageCount"] = (short)2, ["Attempts"] = (byte)0,
            ["CorrelationId"] = "test", ["ResolvedDocumentTypeCode"] = "MANIFEST"
        });
        Assert.Equal("AUTO", claim.DocumentTypeCode); Assert.Equal("MANIFEST", claim.ResolvedDocumentTypeCode);
        Assert.Null(claim.ShippingLineCode);
        var position = Map<RunPosition>(new()
        {
            ["RunNo"] = (short)1, ["Stage"] = "Extract", ["OperationId"] = null, ["Kind"] = null,
            ["ModelId"] = null, ["State"] = null, ["OperationLocation"] = null, ["AcceptedAtUtc"] = null,
            ["UnknownInRun"] = 0, ["MapOperationId"] = null, ["MapRawResultPath"] = null, ["MapModelId"] = null,
            ["RequestedDocumentTypeCode"] = "AUTO", ["ResolvedDocumentTypeCode"] = "MANIFEST", ["ShippingLineCode"] = null
        });
        Assert.Equal("AUTO", position.RequestedDocumentTypeCode); Assert.Equal("MANIFEST", position.ResolvedDocumentTypeCode);
        var run = Map<RunOutcome>(new()
        {
            ["JobId"] = id, ["RunNo"] = (short)1, ["ClientRequestId"] = null, ["Status"] = "Completed",
            ["ErrorCode"] = null, ["ErrorMessage"] = null, ["CreatedAtUtc"] = DateTime.UtcNow,
            ["CompletedAtUtc"] = null, ["PageCount"] = (short)2, ["ContractVersion"] = "1.0",
            ["ResultJson"] = "{}", ["FlaggedFields"] = (short)0, ["RequestedDocumentTypeCode"] = "AUTO",
            ["ResolvedDocumentTypeCode"] = "MANIFEST", ["ResultType"] = "cargo-manifest.v1"
        });
        Assert.Equal("cargo-manifest.v1", run.ResultType); Assert.Equal("MANIFEST", run.ResolvedDocumentTypeCode);
        Assert.Equal("AUTO", run.RequestedDocumentTypeCode);
        var mapping = Map<ClassMapping>(new() { ["ShippingLineCode"] = null, ["MinClassConfidence"] = .8m, ["TargetDocumentTypeCode"] = "MANIFEST" });
        Assert.Null(mapping.ShippingLineCode); Assert.Equal("MANIFEST", mapping.TargetDocumentTypeCode); Assert.Equal(.8m, mapping.MinClassConfidence);
    }

    
    [TestCase(null, null)]
    [TestCase("MANIFEST", "cargo-manifest.v1")]
    public void Route_and_result_parameters_preserve_SQL_metadata_even_when_null(string? type, string? resultType)
    {
        var decision = new ClassifierSuccess(Guid.NewGuid(), Guid.NewGuid(), 1, "router", 2, DateTimeOffset.UtcNow,
            "class", .9m, null, null, Guid.NewGuid(), "{}", type);
        using var classifier = new SqlCommand();
        ((SqlMapper.IDynamicParameters)new SqlParameters(decision)).AddParameters(classifier, null!);
        var target = classifier.Parameters["@resolvedDocumentTypeCode"];
        Assert.Equal(SqlDbType.VarChar, target.SqlDbType); Assert.Equal(20, target.Size);
        Assert.Equal((object?)type ?? DBNull.Value, target.Value);
        Assert.Equal(DBNull.Value, classifier.Parameters["@resolvedLine"].Value);
        using var completion = new SqlCommand();
        ((SqlMapper.IDynamicParameters)new SqlParameters(new Completion(decision.JobId, decision.LeaseToken, "{}", 0,
            Guid.NewGuid(), "{}", resultType))).AddParameters(completion, null!);
        var discriminator = completion.Parameters["@resultType"];
        Assert.Equal(SqlDbType.VarChar, discriminator.SqlDbType); Assert.Equal(32, discriminator.Size);
        Assert.Equal((object?)resultType ?? DBNull.Value, discriminator.Value);
    }

    private static T Map<T>(Dictionary<string, object?> values)
    {
        var table = new DataTable();
        foreach (var name in values.Keys)
        {
            var type = typeof(T).GetProperty(name)!.PropertyType;
            type = Nullable.GetUnderlyingType(type) ?? type;
            table.Columns.Add(name, type == typeof(DateTimeOffset) ? typeof(DateTime) : type);
        }
        table.Rows.Add(values.Values.Select(v => v ?? DBNull.Value).ToArray());
        using var reader = table.CreateDataReader();
        reader.Read();
        return reader.GetRowParser<T>()(reader);
    }
}
