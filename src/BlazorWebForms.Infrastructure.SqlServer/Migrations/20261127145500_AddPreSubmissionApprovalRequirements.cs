using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebForms.Infrastructure.SqlServer.Migrations;

[DbContext(typeof(BlazorWebFormsDbContext))]
[Migration("20261127145500_AddPreSubmissionApprovalRequirements")]
public partial class AddPreSubmissionApprovalRequirements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPreSubmission",
            table: "ApprovalSteps",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "RequireSignature",
            table: "ApprovalSteps",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsPreSubmission", table: "ApprovalSteps");
        migrationBuilder.DropColumn(name: "RequireSignature", table: "ApprovalSteps");
    }
}
