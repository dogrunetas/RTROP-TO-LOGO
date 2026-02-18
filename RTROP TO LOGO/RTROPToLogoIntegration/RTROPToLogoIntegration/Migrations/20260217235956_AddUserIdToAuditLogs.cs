using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTROPToLogoIntegration.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "LOG_INCOMING_REQUESTS",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "LOG_INCOMING_REQUESTS");
        }
    }
}
