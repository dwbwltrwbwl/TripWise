using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pinnedMessageId",
                table: "Chats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pinnedAt",
                table: "Chats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pinnedById",
                table: "Chats",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chats_pinnedMessageId",
                table: "Chats",
                column: "pinnedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_pinnedById",
                table: "Chats",
                column: "pinnedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_ChatMessages_pinnedMessageId",
                table: "Chats",
                column: "pinnedMessageId",
                principalTable: "ChatMessages",
                principalColumn: "idMessage",
                onDelete: ReferentialAction.Restrict); // ИСПРАВЛЕНО

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_Users_pinnedById",
                table: "Chats",
                column: "pinnedById",
                principalTable: "Users",
                principalColumn: "idUser",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chats_ChatMessages_pinnedMessageId",
                table: "Chats");

            migrationBuilder.DropForeignKey(
                name: "FK_Chats_Users_pinnedById",
                table: "Chats");

            migrationBuilder.DropIndex(
                name: "IX_Chats_pinnedMessageId",
                table: "Chats");

            migrationBuilder.DropIndex(
                name: "IX_Chats_pinnedById",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "pinnedMessageId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "pinnedAt",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "pinnedById",
                table: "Chats");
        }
    }
}