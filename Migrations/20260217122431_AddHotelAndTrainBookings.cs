using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelAndTrainBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HotelBookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BookingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HotelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HotelAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HotelPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HotelWebsite = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HotelLatitude = table.Column<double>(type: "float", nullable: false),
                    HotelLongitude = table.Column<double>(type: "float", nullable: false),
                    AccommodationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: true),
                    CheckInDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckOutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nights = table.Column<int>(type: "int", nullable: false),
                    Guests = table.Column<int>(type: "int", nullable: false),
                    Rooms = table.Column<int>(type: "int", nullable: false),
                    PricePerNight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "RUB"),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpecialRequests = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HotelBookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_BookingNumber",
                table: "HotelBookings",
                column: "BookingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_CheckInDate",
                table: "HotelBookings",
                column: "CheckInDate");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_Status",
                table: "HotelBookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_UserId",
                table: "HotelBookings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelBookings");
        }
    }
}
