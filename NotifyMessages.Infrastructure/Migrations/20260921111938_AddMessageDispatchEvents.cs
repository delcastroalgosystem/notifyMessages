using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageDispatchEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MESSAGE_DISPATCH_EVENTS",
                schema: "NotifyMsg",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DISPATCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    EVENT_TYPE = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EVENT_DATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PROVIDER_RESPONSE = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MESSAGE_DISPATCH_EVENTS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MESSAGE_DISPATCH_EVENTS_MESSAGE_DISPATCH_DISPATCH_ID",
                        column: x => x.DISPATCH_ID,
                        principalSchema: "NotifyMsg",
                        principalTable: "MESSAGE_DISPATCH",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDispatchEvent_DispatchId",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH_EVENTS",
                column: "DISPATCH_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MESSAGE_DISPATCH_EVENTS",
                schema: "NotifyMsg");
        }
    }
}
