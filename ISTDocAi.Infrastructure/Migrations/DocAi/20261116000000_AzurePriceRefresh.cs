using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISTDocAi.Infrastructure.Migrations.DocAi
{
    /// <inheritdoc />
    public partial class AzurePriceRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AzurePriceChecks",
                schema: "dbo",
                columns: table => new
                {
                    CheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(16)", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(64)", nullable: true),
                    ObservedPrice = table.Column<decimal>(type: "decimal(19,8)", nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    ProposedEffectiveFrom = table.Column<DateTime>(type: "date", nullable: true),
                    MatchedRowCount = table.Column<int>(type: "int", nullable: true),
                    MeterId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    TierMinimumUnits = table.Column<decimal>(type: "decimal(19,8)", nullable: true),
                    AzureEffectiveFrom = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    WarningFingerprint = table.Column<byte[]>(type: "binary(32)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzurePriceChecks", x => new { x.CheckId, x.MeterCode })
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_AzurePriceChecks_MeterCode", "(MeterCode IN ('DI_CUSTOM_EXTRACT', 'DI_CUSTOM_CLASSIFY'))");
                    table.CheckConstraint("CK_AzurePriceChecks_Outcome", "(Outcome IN ('NoChange','Updated','Refused','Failed'))");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AzurePriceChecks_CheckedAtUtc",
                schema: "dbo",
                table: "AzurePriceChecks",
                column: "CheckedAtUtc",
                descending: new bool[0]);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzurePriceChecks",
                schema: "dbo");
        }
    }
}
