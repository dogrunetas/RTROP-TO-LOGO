using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTROPToLogoIntegration.Migrations
{
    /// <inheritdoc />
    public partial class AddLogIncomingRequestsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOG_INCOMING_REQUESTS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOG_INCOMING_REQUESTS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOG_INCOMING_REQUESTS_TransactionId",
                table: "LOG_INCOMING_REQUESTS",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOG_INCOMING_REQUESTS");
        }
    }
}
