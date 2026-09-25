using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyMessages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchBatchForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_MESSAGE_DISPATCH_DISPATCH_BATCH_BATCH_ID",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH",
                column: "BATCH_ID",
                principalSchema: "NotifyMsg",
                principalTable: "DISPATCH_BATCH",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MESSAGE_DISPATCH_DISPATCH_BATCH_BATCH_ID",
                schema: "NotifyMsg",
                table: "MESSAGE_DISPATCH");
        }
    }
}
