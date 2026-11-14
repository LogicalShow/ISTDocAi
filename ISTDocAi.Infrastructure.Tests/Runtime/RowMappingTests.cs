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

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class RowMappingTests
{
    [Test]
    public void Dapper_maps_expanded_intake_metadata_and_keeps_bound_run_number()
    {
        InitializeHandlers();
        var table = new DataTable();
        table.Columns.Add("JobId", typeof(Guid));
        table.Columns.Add("RunNo", typeof(short));
        table.Columns.Add("Outcome", typeof(string));
        table.Columns.Add("RunStatus", typeof(string));
        table.Columns.Add("RunReason", typeof(string));
        table.Columns.Add("ChangesJson", typeof(string));
        table.Rows.Add(Guid.NewGuid(), (short)2, "Existing", "Completed", "ModelChanged", "{\"mapping\":true}");
        using var reader = table.CreateDataReader();
        reader.Read();
        var result = reader.GetRowParser<IntakeResult>()(reader);
        Assert.Equal(IntakeOutcome.Existing, result.Outcome);
        Assert.Equal((short)2, result.RunNo);
        Assert.Equal("ModelChanged", result.RunReason);
        Assert.Equal("{\"mapping\":true}", result.ChangesJson);
    }

    [Test]
    public void Dapper_maps_intake_string_outcome_and_nullable_run()
    {
        InitializeHandlers();
        var table = new DataTable();
        table.Columns.Add("JobId", typeof(Guid));
        table.Columns.Add("RunNo", typeof(short));
        table.Columns.Add("Outcome", typeof(string));
        table.Columns.Add("RunStatus", typeof(string));
        table.Rows.Add(DBNull.Value, DBNull.Value, "NeedsUpload", DBNull.Value);
        using var reader = table.CreateDataReader();
        reader.Read();
        var result = reader.GetRowParser<IntakeResult>()(reader);
        Assert.Equal(IntakeOutcome.NeedsUpload, result.Outcome);
        Assert.Null(result.JobId);
        Assert.Null(result.RunNo);
    }
    
    [TestCase(true)]
    [TestCase(false)]
    public void Dapper_maps_datetime2_to_UTC_in_run_and_position_records(bool hasTimestamp)
    {
        InitializeHandlers();
        var table = new DataTable();
        Add<short>("RunNo"); Add<string>("Stage"); Add<long>("OperationId"); Add<string>("Kind");
        Add<string>("ModelId"); Add<string>("State"); Add<string>("OperationLocation"); Add<DateTime>("AcceptedAtUtc");
        Add<int>("UnknownInRun"); Add<long>("MapOperationId"); Add<string>("MapRawResultPath"); Add<string>("MapModelId");
        var date = new DateTime(2026, 10, 15, 12, 30, 0, DateTimeKind.Unspecified);
        table.Rows.Add((short)1, "Extract", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
            hasTimestamp ? date : DBNull.Value, 0, DBNull.Value, DBNull.Value, DBNull.Value);
        using var reader = table.CreateDataReader();
        reader.Read();
        var result = reader.GetRowParser<RunPosition>()(reader);
        if (hasTimestamp)
        {
            Assert.Equal(TimeSpan.Zero, result.AcceptedAtUtc!.Value.Offset);
            Assert.Equal(date, result.AcceptedAtUtc.Value.UtcDateTime);
        }
        else Assert.Null(result.AcceptedAtUtc);
        void Add<T>(string name) => table.Columns.Add(name, typeof(T));
    }
    [Test]
    public void Dapper_maps_nonnullable_and_nullable_run_timestamps_to_UTC()
    {
        InitializeHandlers();
        var table = new DataTable();
        table.Columns.Add("JobId", typeof(Guid));
        table.Columns.Add("RunNo", typeof(short));
        table.Columns.Add("ClientRequestId", typeof(string));
        table.Columns.Add("Status", typeof(string));
        table.Columns.Add("ErrorCode", typeof(string));
        table.Columns.Add("ErrorMessage", typeof(string));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));
        table.Columns.Add("CompletedAtUtc", typeof(DateTime));
        table.Columns.Add("PageCount", typeof(short));
        table.Columns.Add("ContractVersion", typeof(string));
        table.Columns.Add("ResultJson", typeof(string));
        table.Columns.Add("FlaggedFields", typeof(short));
        var date = new DateTime(2026, 10, 15, 12, 30, 0, DateTimeKind.Unspecified);
        table.Rows.Add(Guid.NewGuid(), (short)1, "request", "Completed", DBNull.Value, DBNull.Value,
            date, date, (short)2, "1.0", "{}", (short)0);
        using var reader = table.CreateDataReader();
        reader.Read();
        var result = reader.GetRowParser<RunOutcome>()(reader);
        Assert.Equal(TimeSpan.Zero, result.CreatedAtUtc.Offset);
        Assert.Equal(date, result.CreatedAtUtc.UtcDateTime);
        Assert.Equal(result.CreatedAtUtc, result.CompletedAtUtc);
    }

    [Test]
    public void Dapper_maps_all_four_B9_columns_including_SQL_UTC_timestamp()
    {
        InitializeHandlers();
        var table = new DataTable();
        table.Columns.Add("MeterCode", typeof(string));
        table.Columns.Add("FirstUsageAtUtc", typeof(DateTime));
        table.Columns.Add("UnpricedRows", typeof(int));
        table.Columns.Add("UnpricedPages", typeof(int));
        var date = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Unspecified);
        table.Rows.Add("DI_CUSTOM_EXTRACT", date, 3, 8);
        using var reader = table.CreateDataReader();
        reader.Read();
        var result = reader.GetRowParser<UnpricedUsage>()(reader);
        Assert.Equal("DI_CUSTOM_EXTRACT", result.MeterCode);
        Assert.Equal(3, result.UnpricedRows);
        Assert.Equal(8, result.UnpricedPages);
        Assert.Equal(TimeSpan.Zero, result.FirstUsageAtUtc.Offset);
        Assert.Equal(date, result.FirstUsageAtUtc.UtcDateTime);
    }

    private static void InitializeHandlers()
    {
        var options = Options.Create(new DocAiSqlOptions());
        _ = new SqlDocAiStore(new SqlConnectionFactory(options), new SqlStatementCatalog(), options);
    }
}
