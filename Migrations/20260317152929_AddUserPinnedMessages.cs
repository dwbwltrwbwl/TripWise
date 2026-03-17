using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPinnedMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPinnedMessages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: false),
                    chatId = table.Column<int>(type: "int", nullable: false),
                    messageId = table.Column<int>(type: "int", nullable: false),
                    pinnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPinnedMessages", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserPinnedMessages_ChatMessages_messageId",
                        column: x => x.messageId,
                        principalTable: "ChatMessages",
                        principalColumn: "idMessage",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPinnedMessages_Chats_chatId",
                        column: x => x.chatId,
                        principalTable: "Chats",
                        principalColumn: "idChat",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPinnedMessages_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPinnedMessages_chatId",
                table: "UserPinnedMessages",
                column: "chatId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPinnedMessages_messageId",
                table: "UserPinnedMessages",
                column: "messageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPinnedMessages_userId_chatId",
                table: "UserPinnedMessages",
                columns: new[] { "userId", "chatId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPinnedMessages");
        }
    }
}