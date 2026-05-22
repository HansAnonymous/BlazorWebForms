using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260521090000_AddEntryFilesMetadata")]
    public partial class AddEntryFilesMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UploadedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFiles_Entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryFiles_EntryId",
                table: "EntryFiles",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFiles_EntryId_FieldId",
                table: "EntryFiles",
                columns: new[] { "EntryId", "FieldId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryFiles");
        }
    }
}
