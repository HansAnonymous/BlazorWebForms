using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260811120400_AddApprovalStepColumns")]
    public partial class AddApprovalStepColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcceptorMode",
                table: "ApprovalSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AcceptorsJson",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "ApprovalSteps",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DelegatedToEmail",
                table: "ApprovalSteps",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DelegatedToName",
                table: "ApprovalSteps",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DelegatedUtc",
                table: "ApprovalSteps",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptorMode",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "AcceptorsJson",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "DelegatedToEmail",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "DelegatedToName",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "DelegatedUtc",
                table: "ApprovalSteps");
        }
    }
}
