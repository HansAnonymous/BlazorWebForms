using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BlazorWebFormsDbContext))]
    [Migration("20260521093000_AddConcurrencyAndAdminIndexes")]
    public partial class AddConcurrencyAndAdminIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Forms",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Entries",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_EntryId_Order",
                table: "ApprovalSteps",
                columns: new[] { "EntryId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_Status",
                table: "ApprovalSteps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_FormId_Status_SubmittedUtc",
                table: "Entries",
                columns: new[] { "FormId", "Status", "SubmittedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_FormId_SubmittedUtc",
                table: "Entries",
                columns: new[] { "FormId", "SubmittedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_Status",
                table: "Entries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_SubmittedUtc",
                table: "Entries",
                column: "SubmittedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EntryRevisions_EntryId_RevisionNumber",
                table: "EntryRevisions",
                columns: new[] { "EntryId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntrySearchIndex_EntryId_Key",
                table: "EntrySearchIndex",
                columns: new[] { "EntryId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_FormNotifications_FormId_Email",
                table: "FormNotifications",
                columns: new[] { "FormId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormPermissions_FormId_UserId",
                table: "FormPermissions",
                columns: new[] { "FormId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Forms_Key",
                table: "Forms",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Forms_PublicationSlug",
                table: "Forms",
                column: "PublicationSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Forms_UpdatedUtc",
                table: "Forms",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_FormId_VersionNumber",
                table: "FormVersions",
                columns: new[] { "FormId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalSteps_EntryId_Order",
                table: "ApprovalSteps");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalSteps_Status",
                table: "ApprovalSteps");

            migrationBuilder.DropIndex(
                name: "IX_Entries_FormId_Status_SubmittedUtc",
                table: "Entries");

            migrationBuilder.DropIndex(
                name: "IX_Entries_FormId_SubmittedUtc",
                table: "Entries");

            migrationBuilder.DropIndex(
                name: "IX_Entries_Status",
                table: "Entries");

            migrationBuilder.DropIndex(
                name: "IX_Entries_SubmittedUtc",
                table: "Entries");

            migrationBuilder.DropIndex(
                name: "IX_EntryRevisions_EntryId_RevisionNumber",
                table: "EntryRevisions");

            migrationBuilder.DropIndex(
                name: "IX_EntrySearchIndex_EntryId_Key",
                table: "EntrySearchIndex");

            migrationBuilder.DropIndex(
                name: "IX_FormNotifications_FormId_Email",
                table: "FormNotifications");

            migrationBuilder.DropIndex(
                name: "IX_FormPermissions_FormId_UserId",
                table: "FormPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Forms_Key",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_Forms_PublicationSlug",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_Forms_UpdatedUtc",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_FormVersions_FormId_VersionNumber",
                table: "FormVersions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Entries");
        }
    }
}
