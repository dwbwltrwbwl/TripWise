using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightPassengers");

            migrationBuilder.DropTable(
                name: "FlightOrders");

            migrationBuilder.CreateTable(
                name: "FlightBookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BookingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FlightId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Airline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AirlineCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AirlineLogo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlightNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DepartureCity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArrivalCity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartureAirport = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ArrivalAirport = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DepartureDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArrivalDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnFlightId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReturnAirline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnFlightNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReturnDepartureDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnArrivalDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "RUB"),
                    Passengers = table.Column<int>(type: "int", nullable: false),
                    FlightClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "economy"),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    ReturnDuration = table.Column<int>(type: "int", nullable: true),
                    Transfers = table.Column<int>(type: "int", nullable: false),
                    ReturnTransfers = table.Column<int>(type: "int", nullable: true),
                    Aircraft = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Baggage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HandLuggage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Meal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRoundTrip = table.Column<bool>(type: "bit", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PassengersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeatNumbers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BookingReference = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightBookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlightBookings_BookingNumber",
                table: "FlightBookings",
                column: "BookingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightBookings_BookingReference",
                table: "FlightBookings",
                column: "BookingReference");

            migrationBuilder.CreateIndex(
                name: "IX_FlightBookings_DepartureDateTime",
                table: "FlightBookings",
                column: "DepartureDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_FlightBookings_Status",
                table: "FlightBookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FlightBookings_TicketNumber",
                table: "FlightBookings",
                column: "TicketNumber");

            migrationBuilder.CreateIndex(
                name: "IX_FlightBookings_UserId",
                table: "FlightBookings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightBookings");

            migrationBuilder.CreateTable(
                name: "FlightOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Airline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArrivalAirport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArrivalCity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArrivalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartureAirport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartureCity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FlightId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlightNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsReturn = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SearchId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Transfers = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightPassengers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Baggage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MealPreference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeatNumber = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightPassengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightPassengers_FlightOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "FlightOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlightOrders_UserId",
                table: "FlightOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPassengers_OrderId",
                table: "FlightPassengers",
                column: "OrderId");
        }
    }
}
