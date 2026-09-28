using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggerInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DispatchBatch_Trigger_RunDate",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH");

            migrationBuilder.AddColumn<int>(
                name: "INTERVAL_MINUTES",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LAST_RUN_AT",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RUN_AT",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MessageTrigger_IntervalMinutes",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER",
                sql: "[INTERVAL_MINUTES] IS NULL OR [INTERVAL_MINUTES] BETWEEN 5 AND 1440");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchBatch_Trigger_RunDate",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH",
                columns: new[] { "TRIGGER_ID", "RUN_DATE" },
                unique: true,
                filter: "[TRIGGER_ID] IS NOT NULL AND [RUN_DATE] IS NOT NULL AND [SOURCE] = 1 AND [RUN_AT] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MessageTrigger_IntervalMinutes",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER");

            migrationBuilder.DropIndex(
                name: "IX_DispatchBatch_Trigger_RunDate",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH");

            migrationBuilder.DropColumn(
                name: "INTERVAL_MINUTES",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER");

            migrationBuilder.DropColumn(
                name: "LAST_RUN_AT",
                schema: "NotifyMsg",
                table: "MESSAGE_TRIGGER");

            migrationBuilder.DropColumn(
                name: "RUN_AT",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchBatch_Trigger_RunDate",
                schema: "NotifyMsg",
                table: "DISPATCH_BATCH",
                columns: new[] { "TRIGGER_ID", "RUN_DATE" },
                unique: true,
                filter: "[TRIGGER_ID] IS NOT NULL AND [RUN_DATE] IS NOT NULL AND [SOURCE] = 1");
        }
    }
}
