using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISTDocAi.Infrastructure.Migrations.DocAi
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Database-level setting must run before transactional schema DDL.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.databases WHERE name = DB_NAME() AND is_read_committed_snapshot_on = 0)
                BEGIN
                    DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET READ_COMMITTED_SNAPSHOT ON';
                    EXEC(@sql);
                END;
                """, suppressTransaction: true);
            foreach (var file in new[] { "InitialSchema.sql", "InitialRoles.sql" })
                foreach (var batch in SqlBatchReader.Split(SqlBatchReader.ReadResource(file)))
                    migrationBuilder.Sql(SqlBatchReader.PrepareBatch(batch));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The initial DocAi schema cannot be rolled back.");
        }
    }
}
