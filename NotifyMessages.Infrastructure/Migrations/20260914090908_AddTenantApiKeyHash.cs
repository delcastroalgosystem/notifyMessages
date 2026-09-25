using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantApiKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "API_KEY_HASH",
                table: "TENANT",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_ApiKeyHash",
                table: "TENANT",
                column: "API_KEY_HASH",
                unique: true,
                filter: "[API_KEY_HASH] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenant_ApiKeyHash",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "API_KEY_HASH",
                table: "TENANT");
        }
    }
}
