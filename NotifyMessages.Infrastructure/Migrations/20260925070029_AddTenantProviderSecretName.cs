using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProviderSecretName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SECRET_NAME",
                schema: "NotifyMsg",
                table: "TENANT_PROVIDER_CONFIG",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SECRET_NAME",
                schema: "NotifyMsg",
                table: "TENANT_PROVIDER_CONFIG");
        }
    }
}
