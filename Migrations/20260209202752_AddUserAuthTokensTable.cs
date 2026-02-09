using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAuthTokensTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ЗАКОММЕНТИРУЙТЕ ВСЁ ЭТО:
            /*
            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    idExpenseCategory = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseCategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.idExpenseCategory);
                });
            */

            // ... ЗАКОММЕНТИРУЙТЕ ВСЕ ДРУГИЕ CreateTable ...

            // ДОБАВЬТЕ ЭТО В НАЧАЛО метода Up():
            migrationBuilder.CreateTable(
                name: "UserAuthTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAuthTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAuthTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAuthTokens_ExpiresAt",
                table: "UserAuthTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserAuthTokens_Token",
                table: "UserAuthTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAuthTokens_UserId",
                table: "UserAuthTokens",
                column: "UserId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ЗАКОММЕНТИРУЙТЕ ВСЁ ЭТО:
            /*
            migrationBuilder.DropTable(
                name: "ChatMessages");
            */

            // ... ЗАКОММЕНТИРУЙТЕ ВСЕ ДРУГИЕ DropTable ...

            // ДОБАВЬТЕ ЭТО В НАЧАЛО метода Down():
            migrationBuilder.DropTable(
                name: "UserAuthTokens");

            // ... ОСТАЛЬНОЙ КОД ОСТАВЬТЕ ЗАКОММЕНТИРОВАННЫМ ...
        }
    }
}
