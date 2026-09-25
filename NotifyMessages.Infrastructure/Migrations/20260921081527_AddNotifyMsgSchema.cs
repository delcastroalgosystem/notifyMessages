using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifyMsgSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "NotifyMsg");

            migrationBuilder.RenameTable(
                name: "TENANT_PROVIDER_CONFIG",
                newName: "TENANT_PROVIDER_CONFIG",
                newSchema: "NotifyMsg");

            migrationBuilder.RenameTable(
                name: "TENANT",
                newName: "TENANT",
                newSchema: "NotifyMsg");

            migrationBuilder.RenameTable(
                name: "TEMPLATE",
                newName: "TEMPLATE",
                newSchema: "NotifyMsg");

            migrationBuilder.RenameTable(
                name: "MESSAGE_DISPATCH",
                newName: "MESSAGE_DISPATCH",
                newSchema: "NotifyMsg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "TENANT_PROVIDER_CONFIG",
                schema: "NotifyMsg",
                newName: "TENANT_PROVIDER_CONFIG");

            migrationBuilder.RenameTable(
                name: "TENANT",
                schema: "NotifyMsg",
                newName: "TENANT");

            migrationBuilder.RenameTable(
                name: "TEMPLATE",
                schema: "NotifyMsg",
                newName: "TEMPLATE");

            migrationBuilder.RenameTable(
                name: "MESSAGE_DISPATCH",
                schema: "NotifyMsg",
                newName: "MESSAGE_DISPATCH");
        }
    }
}
