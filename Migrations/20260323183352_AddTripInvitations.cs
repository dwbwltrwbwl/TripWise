using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddTripInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TripInvitations",
                columns: table => new
                {
                    idInvitation = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    inviterId = table.Column<int>(type: "int", nullable: false),
                    invitedId = table.Column<int>(type: "int", nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    invitedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    respondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripInvitations", x => x.idInvitation);
                    table.ForeignKey(
                        name: "FK_TripInvitations_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripInvitations_Users_invitedId",
                        column: x => x.invitedId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripInvitations_Users_inviterId",
                        column: x => x.inviterId,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripInvitations_InvitedId",
                table: "TripInvitations",
                column: "invitedId");

            migrationBuilder.CreateIndex(
                name: "IX_TripInvitations_inviterId",
                table: "TripInvitations",
                column: "inviterId");

            migrationBuilder.CreateIndex(
                name: "IX_TripInvitations_Status",
                table: "TripInvitations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_TripInvitations_Trip_Invited_Status",
                table: "TripInvitations",
                columns: new[] { "idTrip", "invitedId", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripInvitations");
        }
    }
}
