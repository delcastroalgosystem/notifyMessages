using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggersBatchesSuppressions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "REF_NAME",
                schema: "NotifyMsg",
                table: "TENANT",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SANDBOX_CONTACT",
                schema: "NotifyMsg",
                table: "TENANT",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SENDING_ENABLED",
                schema: "NotifyMsg",
                table: "TENANT",
                type: "bit",
                nullable: false,
                // Tenants existentes continuam a enviar (o modelo usa true por omissão)
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TIME_ZONE",
                schema: "NotifyMsg",
                table: "TENANT",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Europe/Lisbon");

            migrationBuilder.AddColumn<int>(
                name: "TENANT_ID",
                schema: "NotifyMsg",
                table: "TEMPLATE",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BATCH_ID",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXTERNAL_KEY",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SCHEDULED_AT",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SENT_TO",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TRIGGER_ID",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DISPATCH_BATCH",
                schema: "NotifyMsg",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TENANT_ID = table.Column<int>(type: "int", nullable: false),
                    TRIGGER_ID = table.Column<int>(type: "int", nullable: true),
                    SOURCE = table.Column<int>(type: "int", nullable: false),
                    RUN_DATE = table.Column<DateOnly>(type: "date", nullable: true),
                    REFERENCE_DATE = table.Column<DateOnly>(type: "date", nullable: true),
                    STATUS = table.Column<int>(type: "int", nullable: false),
                    RECEIVED = table.Column<int>(type: "int", nullable: false),
                    ACCEPTED = table.Column<int>(type: "int", nullable: false),
                    DUPLICATES = table.Column<int>(type: "int", nullable: false),
                    SUPPRESSED = table.Column<int>(type: "int", nullable: false),
                    REJECTED = table.Column<int>(type: "int", nullable: false),
                    DETAILS = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FILE_NAME = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    CREATED_BY = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    APPROVED_BY = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    APPROVED_AT = table.Column<DateTime>(type: "datetime2", nullable: true),
                    COMPLETED_AT = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DISPATCH_BATCH", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MESSAGE_TRIGGER",
                schema: "NotifyMsg",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TENANT_ID = table.Column<int>(type: "int", nullable: false),
                    CODE = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TEMPLATE_ID = table.Column<int>(type: "int", nullable: false),
                    SCHEDULE_DAY = table.Column<byte>(type: "tinyint", nullable: true),
                    SCHEDULE_TIME = table.Column<TimeOnly>(type: "time", nullable: false),
                    START_DATE = table.Column<DateOnly>(type: "date", nullable: true),
                    PARAMETERS = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    REQUIRES_APPROVAL = table.Column<bool>(type: "bit", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "bit", nullable: false),
                    CREATED_BY = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UPDATED_BY = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UPDATED_AT = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MESSAGE_TRIGGER", x => x.ID);
                    table.CheckConstraint("CK_MessageTrigger_ScheduleDay", "[SCHEDULE_DAY] IS NULL OR [SCHEDULE_DAY] BETWEEN 1 AND 28");
                });

            migrationBuilder.CreateTable(
                name: "SUPPRESSION",
                schema: "NotifyMsg",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TENANT_ID = table.Column<int>(type: "int", nullable: false),
                    CONTACT = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    REASON = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SOURCE = table.Column<int>(type: "int", nullable: false),
                    CREATED_BY = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUPPRESSION", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_RefName",
                schema: "NotifyMsg",
                table: "TENANT",
                column: "REF_NAME",
                unique: true,
                filter: "[REF_NAME] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TenantId",
                schema: "NotifyMsg",
                table: "TEMPLATE",
                column: "TENANT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDispatch_BatchId",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                column: "BATCH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDispatch_Tenant_ExternalKey",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                columns: new[] { "TENANT_ID", "EXTERNAL_KEY" },
                unique: true,
                filter: "[EXTERNAL_KEY] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchBatch_Tenant_CreatedAt",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH",
                columns: new[] { "TENANT_ID", "CREATED_AT" });

            migrationBuilder.CreateIndex(
                name: "IX_DispatchBatch_Trigger_RunDate",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH",
                columns: new[] { "TRIGGER_ID", "RUN_DATE" },
                unique: true,
                filter: "[TRIGGER_ID] IS NOT NULL AND [RUN_DATE] IS NOT NULL AND [SOURCE] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTrigger_Tenant_Name",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER",
                columns: new[] { "TENANT_ID", "NAME" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppression_Tenant_Contact",
                schema: "NotifyMsg",
                table: "SUPPRESSION",
                columns: new[] { "TENANT_ID", "CONTACT" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DISPATCH_BATCH",
                schema: "NotifyMsg");

            migrationBuilder.DropTable(
                name: "MESSAGE_TRIGGER",
                schema: "NotifyMsg");

            migrationBuilder.DropTable(
                name: "SUPPRESSION",
                schema: "NotifyMsg");

            migrationBuilder.DropIndex(
                name: "IX_Tenant_RefName",
                schema: "NotifyMsg",
                table: "TENANT");

            migrationBuilder.DropIndex(
                name: "IX_Template_TenantId",
                schema: "NotifyMsg",
                table: "TEMPLATE");

            migrationBuilder.DropIndex(
                name: "IX_MessageDispatch_BatchId",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");

            migrationBuilder.DropIndex(
                name: "IX_MessageDispatch_Tenant_ExternalKey",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");

            migrationBuilder.DropColumn(
                name: "REF_NAME",
                schema: "NotifyMsg",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "SANDBOX_CONTACT",
                schema: "NotifyMsg",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "SENDING_ENABLED",
                schema: "NotifyMsg",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "TIME_ZONE",
                schema: "NotifyMsg",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "TENANT_ID",
                schema: "NotifyMsg",
                table: "TEMPLATE");

            migrationBuilder.DropColumn(
                name: "BATCH_ID",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");

            migrationBuilder.DropColumn(
                name: "EXTERNAL_KEY",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");

            migrationBuilder.DropColumn(
                name: "SCHEDULED_AT",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");

            migrationBuilder.DropColumn(
                name: "SENT_TO",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");

            migrationBuilder.DropColumn(
                name: "TRIGGER_ID",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");
        }
    }
}
