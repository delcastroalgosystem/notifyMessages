using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MESSAGE_DISPATCH",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TENANT_ID = table.Column<int>(type: "int", nullable: false),
                    TEMPLATE_ID = table.Column<int>(type: "int", nullable: false),
                    IDEMPOTENCY_KEY = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EXTERNAL_ID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RECIPIENT_NAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RECIPIENT_CONTACT = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CONTEXT_DATA_JSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CURRENT_STATUS = table.Column<int>(type: "int", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    PROCESSED_AT = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RETRY_COUNT = table.Column<int>(type: "int", nullable: false),
                    ERROR_LOG = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MESSAGE_DISPATCH", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TEMPLATE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CHANNEL = table.Column<int>(type: "int", nullable: false),
                    PROVIDER_TYPE = table.Column<int>(type: "int", nullable: false),
                    USE_CAMPAIGN_MODE = table.Column<bool>(type: "bit", nullable: false),
                    SUBJECT = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HTML_BODY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TEXT_BODY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SENDER_ID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SENDER_NAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EXTERNAL_TEMPLATE_ID = table.Column<int>(type: "int", nullable: true),
                    LIST_ID = table.Column<int>(type: "int", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "bit", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UPDATED_AT = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TEMPLATE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TENANT",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NAME = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DOCUMENT_ID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ACTIVE = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TENANT", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TENANT_PROVIDER_CONFIG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TENANT_ID = table.Column<int>(type: "int", nullable: false),
                    PROVIDER_TYPE = table.Column<int>(type: "int", nullable: false),
                    IS_GLOBAL = table.Column<bool>(type: "bit", nullable: false),
                    API_KEY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BASE_URL = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DOMAIN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SENDER_ID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SENDER_NAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ACCOUNT_SID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AUTH_TOKEN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FROM_NUMBER = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LIST_ID = table.Column<int>(type: "int", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "bit", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UPDATED_AT = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TENANT_PROVIDER_CONFIG", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDispatch_IdempotencyKey",
                table: "MESSAGE_DISPATCH",
                column: "IDEMPOTENCY_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageDispatch_QueueProcessing",
                table: "MESSAGE_DISPATCH",
                columns: new[] { "CURRENT_STATUS", "CREATED_AT" });

            migrationBuilder.CreateIndex(
                name: "IX_Template_Name",
                table: "TEMPLATE",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Name",
                table: "TENANT",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProviderConfig_Tenant_Provider",
                table: "TENANT_PROVIDER_CONFIG",
                columns: new[] { "TENANT_ID", "PROVIDER_TYPE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MESSAGE_DISPATCH");

            migrationBuilder.DropTable(
                name: "TEMPLATE");

            migrationBuilder.DropTable(
                name: "TENANT");

            migrationBuilder.DropTable(
                name: "TENANT_PROVIDER_CONFIG");
        }
    }
}
