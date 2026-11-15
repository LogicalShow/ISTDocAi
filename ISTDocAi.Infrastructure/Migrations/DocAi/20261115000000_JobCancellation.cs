using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ISTDocAi.Infrastructure.Migrations.DocAi;

[DbContext(typeof(DocAiDbContext))]
[Migration("20261115000000_JobCancellation")]
public sealed class JobCancellation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var batch in SqlBatchReader.Split(SqlBatchReader.ReadResource("JobCancellation.sql")))
            migrationBuilder.Sql(SqlBatchReader.PrepareBatch(batch));
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder) =>
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Job cancellation audit cannot be rolled back.");
}
