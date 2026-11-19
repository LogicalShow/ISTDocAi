using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISTDocAi.Infrastructure.Migrations.DocAi
{
    /// <inheritdoc />
    public partial class JobCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAtUtc",
                schema: "dbo",
                table: "JobRuns",
                type: "datetime2(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelRequestedAtUtc",
                schema: "dbo",
                table: "JobRuns");
        }
    }
}
