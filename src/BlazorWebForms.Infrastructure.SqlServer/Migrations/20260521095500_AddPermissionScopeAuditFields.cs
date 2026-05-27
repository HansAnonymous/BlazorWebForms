using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260521095500_AddPermissionScopeAuditFields")]
    public partial class AddPermissionScopeAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScopeType",
                table: "FormPermissions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Form");

            migrationBuilder.AddColumn<string>(
                name: "ScopeValue",
                table: "FormPermissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedUtc",
                table: "FormPermissions",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "FormPermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_FormPermissions_FormId_ScopeType_ScopeValue",
                table: "FormPermissions",
                columns: new[] { "FormId", "ScopeType", "ScopeValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormPermissions_FormId_ScopeType_ScopeValue",
                table: "FormPermissions");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "FormPermissions");

            migrationBuilder.DropColumn(
                name: "ScopeValue",
                table: "FormPermissions");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "FormPermissions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "FormPermissions");
        }
    }
}
