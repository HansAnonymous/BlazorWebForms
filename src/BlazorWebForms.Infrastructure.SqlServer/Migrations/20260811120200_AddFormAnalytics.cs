using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260811120200_AddFormAnalytics")]
    public partial class AddFormAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormAnalytics",
                columns: table => new
                {
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    StartCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    SubmissionCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    AbandonCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    AverageCompletionSeconds = table.Column<double>(type: "float", nullable: false, defaultValue: 0.0),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormAnalytics", x => x.FormId);
                    table.ForeignKey(
                        name: "FK_FormAnalytics_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormAnalytics");
        }
    }
}
