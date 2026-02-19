using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTROPToLogoIntegration.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRefreshTokensTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "USER_REFRESH_TOKENS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USER_REFRESH_TOKENS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_USER_REFRESH_TOKENS_Token",
                table: "USER_REFRESH_TOKENS",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USER_REFRESH_TOKENS_UserId",
                table: "USER_REFRESH_TOKENS",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "USER_REFRESH_TOKENS");
        }
    }
}
