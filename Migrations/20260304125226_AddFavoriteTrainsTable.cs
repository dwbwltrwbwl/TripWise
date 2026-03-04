using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteTrainsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FavoriteTrains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TrainGroupId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ForwardTrainNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReturnTrainNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DepartureStation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArrivalStation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartureStationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ArrivalStationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartureDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnDepartureDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArrivalDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnArrivalDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "RUB"),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    ReturnDuration = table.Column<int>(type: "int", nullable: true),
                    TrainBrand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsFirm = table.Column<bool>(type: "bit", nullable: false),
                    IsRoundTrip = table.Column<bool>(type: "bit", nullable: false),
                    Passengers = table.Column<int>(type: "int", nullable: false),
                    BookingUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteTrains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteTrains_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTrains_TrainGroupId",
                table: "FavoriteTrains",
                column: "TrainGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTrains_UserId_TrainGroupId",
                table: "FavoriteTrains",
                columns: new[] { "UserId", "TrainGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoriteTrains");
        }
    }
}
