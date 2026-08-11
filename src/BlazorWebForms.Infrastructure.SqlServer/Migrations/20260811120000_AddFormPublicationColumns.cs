using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260811120000_AddFormPublicationColumns")]
    public partial class AddFormPublicationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicationAccessPasswordHash",
                table: "Forms",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PublicationAutoSaveIntervalSeconds",
                table: "Forms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationCapReachedMessage",
                table: "Forms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublicationCloseUtc",
                table: "Forms",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationClosedMessage",
                table: "Forms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicationConfirmationMessage",
                table: "Forms",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicationConfirmationRedirectUrl",
                table: "Forms",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PublicationMaxSubmissions",
                table: "Forms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationNotYetOpenMessage",
                table: "Forms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublicationOpenUtc",
                table: "Forms",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicationRequireCaptcha",
                table: "Forms",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicationAccessPasswordHash",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationAutoSaveIntervalSeconds",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationCapReachedMessage",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationCloseUtc",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationClosedMessage",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationConfirmationMessage",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationConfirmationRedirectUrl",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationMaxSubmissions",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationNotYetOpenMessage",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationOpenUtc",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicationRequireCaptcha",
                table: "Forms");
        }
    }
}
