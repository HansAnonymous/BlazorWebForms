using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260811120300_AddEntryQuizColumns")]
    public partial class AddEntryQuizColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedUtc",
                table: "Entries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Score",
                table: "Entries",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuizPassed",
                table: "Entries",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedUtc",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "QuizPassed",
                table: "Entries");
        }
    }
}
