using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSystemTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Создаем таблицу Chats
            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    idChat = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "private"),
                    idTrip = table.Column<int>(type: "int", nullable: true),
                    createdById = table.Column<int>(type: "int", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    lastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.idChat);
                    table.ForeignKey(
                        name: "FK_Chats_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.SetNull); // ✅ ИСПРАВЛЕНО: SetNull
                    table.ForeignKey(
                        name: "FK_Chats_Users_createdById",
                        column: x => x.createdById,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Restrict); // ✅ ИСПРАВЛЕНО: Restrict
                });

            // 2. Создаем дефолтный чат для существующих сообщений
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Chats])
                BEGIN
                    INSERT INTO [dbo].[Chats] ([name], [type], [createdById], [createdAt])
                    VALUES ('Общий чат', 'group', 1, GETUTCDATE());
                END
            ");

            // 3. Удаляем старые внешние ключи
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_PointsOfInterest_idPoint", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Trips_idTrip", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Users_idUser", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropIndex(name: "IX_ChatMessages_idPoint", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropIndex(name: "IX_ChatMessages_idTrip", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropIndex(name: "IX_ChatMessages_idUser", table: "ChatMessages"); } catch { }

            // 4. Добавляем новые колонки
            migrationBuilder.AddColumn<string>(
                name: "attachmentName",
                table: "ChatMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "attachmentSize",
                table: "ChatMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachmentType",
                table: "ChatMessages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachmentUrl",
                table: "ChatMessages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "editedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idChat",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "replyToId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            // 5. Обновляем sentAt default value
            migrationBuilder.AlterColumn<DateTime>(
                name: "sentAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            // 6. Делаем idTrip nullable
            migrationBuilder.AlterColumn<int>(
                name: "idTrip",
                table: "ChatMessages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // 7. Обновляем существующие записи - привязываем к дефолтному чату
            migrationBuilder.Sql(@"
                DECLARE @DefaultChatId INT = (SELECT TOP 1 [idChat] FROM [dbo].[Chats]);
                UPDATE [dbo].[ChatMessages] 
                SET [idChat] = @DefaultChatId 
                WHERE [idChat] IS NULL;
            ");

            // 8. Делаем idChat NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "idChat",
                table: "ChatMessages",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 9. Создаем таблицу ChatMembers
            migrationBuilder.CreateTable(
                name: "ChatMembers",
                columns: table => new
                {
                    idChatMember = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idChat = table.Column<int>(type: "int", nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    joinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    lastReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "member")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMembers", x => x.idChatMember);
                    table.ForeignKey(
                        name: "FK_ChatMembers_Chats_idChat",
                        column: x => x.idChat,
                        principalTable: "Chats",
                        principalColumn: "idChat",
                        onDelete: ReferentialAction.Cascade); // ✅ CASCADE (если удаляем чат, удаляем участников)
                    table.ForeignKey(
                        name: "FK_ChatMembers_Users_idUser",
                        column: x => x.idUser,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Restrict); // ✅ ИСПРАВЛЕНО: Restrict
                });

            // 10. Создаем таблицу ChatMessageReads
            migrationBuilder.CreateTable(
                name: "ChatMessageReads",
                columns: table => new
                {
                    idChatMessageRead = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idMessage = table.Column<int>(type: "int", nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    readAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageReads", x => x.idChatMessageRead);
                    table.ForeignKey(
                        name: "FK_ChatMessageReads_Messages",
                        column: x => x.idMessage,
                        principalTable: "ChatMessages",
                        principalColumn: "idMessage",
                        onDelete: ReferentialAction.Cascade); // ✅ CASCADE
                    table.ForeignKey(
                        name: "FK_ChatMessageReads_Users_idUser",
                        column: x => x.idUser,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Restrict); // ✅ ИСПРАВЛЕНО: Restrict
                });

            // 11. Создаем индексы
            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_idChat",
                table: "ChatMessages",
                column: "idChat");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_replyToId",
                table: "ChatMessages",
                column: "replyToId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_sentAt",
                table: "ChatMessages",
                column: "sentAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMembers_ChatId_UserId",
                table: "ChatMembers",
                columns: new[] { "idChat", "idUser" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMembers_idUser",
                table: "ChatMembers",
                column: "idUser");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReads_idUser",
                table: "ChatMessageReads",
                column: "idUser");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReads_MessageId_UserId",
                table: "ChatMessageReads",
                columns: new[] { "idMessage", "idUser" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chats_createdById",
                table: "Chats",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_idTrip",
                table: "Chats",
                column: "idTrip");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_lastMessageAt",
                table: "Chats",
                column: "lastMessageAt");

            // 12. Добавляем внешние ключи - БЕЗ МНОЖЕСТВЕННЫХ КАСКАДОВ!
            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Chats",
                table: "ChatMessages",
                column: "idChat",
                principalTable: "Chats",
                principalColumn: "idChat",
                onDelete: ReferentialAction.Cascade); // ✅ НУЖНО для чатов

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ReplyTo",
                table: "ChatMessages",
                column: "replyToId",
                principalTable: "ChatMessages",
                principalColumn: "idMessage",
                onDelete: ReferentialAction.Restrict); // ✅ ИСПРАВЛЕНО: Restrict (не Cascade!)

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users",
                table: "ChatMessages",
                column: "idUser",
                principalTable: "Users",
                principalColumn: "idUser",
                onDelete: ReferentialAction.Restrict); // ✅ ИСПРАВЛЕНО: Restrict

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Trips",
                table: "ChatMessages",
                column: "idTrip",
                principalTable: "Trips",
                principalColumn: "idTrip",
                onDelete: ReferentialAction.SetNull); // ✅ ИСПРАВЛЕНО: SetNull
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Удаляем новые FK
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Chats", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_ReplyTo", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Users", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Trips", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Points", table: "ChatMessages"); } catch { }

            // 2. Удаляем новые таблицы
            try { migrationBuilder.DropTable(name: "ChatMembers"); } catch { }
            try { migrationBuilder.DropTable(name: "ChatMessageReads"); } catch { }
            try { migrationBuilder.DropTable(name: "Chats"); } catch { }

            // 3. Удаляем новые индексы
            try { migrationBuilder.DropIndex(name: "IX_ChatMessages_idChat", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropIndex(name: "IX_ChatMessages_replyToId", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropIndex(name: "IX_ChatMessages_sentAt", table: "ChatMessages"); } catch { }

            // 4. Удаляем новые колонки
            try { migrationBuilder.DropColumn(name: "attachmentName", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropColumn(name: "attachmentSize", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropColumn(name: "attachmentType", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropColumn(name: "attachmentUrl", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropColumn(name: "editedAt", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropColumn(name: "idChat", table: "ChatMessages"); } catch { }
            try { migrationBuilder.DropColumn(name: "replyToId", table: "ChatMessages"); } catch { }

            // 5. Возвращаем старые настройки
            migrationBuilder.AlterColumn<DateTime>(
                name: "sentAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<int>(
                name: "idTrip",
                table: "ChatMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 6. Восстанавливаем старые FK с правильными onDelete
            try
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_ChatMessages_PointsOfInterest_idPoint",
                    table: "ChatMessages",
                    column: "idPoint",
                    principalTable: "PointsOfInterest",
                    principalColumn: "idPoint",
                    onDelete: ReferentialAction.SetNull); // ✅ SetNull
            }
            catch { }

            try
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_ChatMessages_Trips_idTrip",
                    table: "ChatMessages",
                    column: "idTrip",
                    principalTable: "Trips",
                    principalColumn: "idTrip",
                    onDelete: ReferentialAction.Cascade); // ✅ Cascade
            }
            catch { }

            try
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_ChatMessages_Users_idUser",
                    table: "ChatMessages",
                    column: "idUser",
                    principalTable: "Users",
                    principalColumn: "idUser",
                    onDelete: ReferentialAction.Restrict); // ✅ Restrict
            }
            catch { }
        }
    }
}