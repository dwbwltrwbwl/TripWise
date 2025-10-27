using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class test1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    idRole = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.idRole);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    idUser = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    passwordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    age = table.Column<int>(type: "int", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    idRole = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.idUser);
                    table.ForeignKey(
                        name: "FK_Users_Roles_idRole",
                        column: x => x.idRole,
                        principalTable: "Roles",
                        principalColumn: "idRole",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    idTrip = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    startDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    endDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    totalBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    createdById = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.idTrip);
                    table.ForeignKey(
                        name: "FK_Trips_Users_createdById",
                        column: x => x.createdById,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    idDocument = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    fileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    filePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    uploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    uploadedById = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.idDocument);
                    table.ForeignKey(
                        name: "FK_Documents_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Users_uploadedById",
                        column: x => x.uploadedById,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "PointsOfInterest",
                columns: table => new
                {
                    idPoint = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    longitude = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    plannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    plannedTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    bookingLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    addedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsOfInterest", x => x.idPoint);
                    table.ForeignKey(
                        name: "FK_PointsOfInterest_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PointsOfInterest_Users_addedById",
                        column: x => x.addedById,
                        principalTable: "Users",
                        principalColumn: "idUser");
                });

            migrationBuilder.CreateTable(
                name: "TripParticipants",
                columns: table => new
                {
                    idTripParticipant = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    participantRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    joinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripParticipants", x => x.idTripParticipant);
                    table.ForeignKey(
                        name: "FK_TripParticipants_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripParticipants_Users_idUser",
                        column: x => x.idUser,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    idMessage = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    idPoint = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.idMessage);
                    table.ForeignKey(
                        name: "FK_ChatMessages_PointsOfInterest_idPoint",
                        column: x => x.idPoint,
                        principalTable: "PointsOfInterest",
                        principalColumn: "idPoint");
                    table.ForeignKey(
                        name: "FK_ChatMessages_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Users_idUser",
                        column: x => x.idUser,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    idExpense = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    expenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    paidById = table.Column<int>(type: "int", nullable: false),
                    idPoint = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.idExpense);
                    table.ForeignKey(
                        name: "FK_Expenses_PointsOfInterest_idPoint",
                        column: x => x.idPoint,
                        principalTable: "PointsOfInterest",
                        principalColumn: "idPoint");
                    table.ForeignKey(
                        name: "FK_Expenses_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_Users_paidById",
                        column: x => x.paidById,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "votingSystems",
                columns: table => new
                {
                    IdVote = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    question = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    idTrip = table.Column<int>(type: "int", nullable: false),
                    createdById = table.Column<int>(type: "int", nullable: false),
                    idPoint = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votingSystems", x => x.IdVote);
                    table.ForeignKey(
                        name: "FK_votingSystems_PointsOfInterest_idPoint",
                        column: x => x.idPoint,
                        principalTable: "PointsOfInterest",
                        principalColumn: "idPoint");
                    table.ForeignKey(
                        name: "FK_votingSystems_Trips_idTrip",
                        column: x => x.idTrip,
                        principalTable: "Trips",
                        principalColumn: "idTrip",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_votingSystems_Users_createdById",
                        column: x => x.createdById,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseShares",
                columns: table => new
                {
                    idExpenseShare = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idExpense = table.Column<int>(type: "int", nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    shareAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    isPaid = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseShares", x => x.idExpenseShare);
                    table.ForeignKey(
                        name: "FK_ExpenseShares_Expenses_idExpense",
                        column: x => x.idExpense,
                        principalTable: "Expenses",
                        principalColumn: "idExpense",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpenseShares_Users_idUser",
                        column: x => x.idUser,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "VoteOptions",
                columns: table => new
                {
                    idVoteOption = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    optionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    idVote = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteOptions", x => x.idVoteOption);
                    table.ForeignKey(
                        name: "FK_VoteOptions_votingSystems_idVote",
                        column: x => x.idVote,
                        principalTable: "votingSystems",
                        principalColumn: "IdVote",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserVotes",
                columns: table => new
                {
                    idUserVote = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idVoteOption = table.Column<int>(type: "int", nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    votedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVotes", x => x.idUserVote);
                    table.ForeignKey(
                        name: "FK_UserVotes_Users_idUser",
                        column: x => x.idUser,
                        principalTable: "Users",
                        principalColumn: "idUser",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_UserVotes_VoteOptions_idVoteOption",
                        column: x => x.idVoteOption,
                        principalTable: "VoteOptions",
                        principalColumn: "idVoteOption",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_idPoint",
                table: "ChatMessages",
                column: "idPoint");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_idTrip",
                table: "ChatMessages",
                column: "idTrip");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_idUser",
                table: "ChatMessages",
                column: "idUser");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_idTrip",
                table: "Documents",
                column: "idTrip");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_uploadedById",
                table: "Documents",
                column: "uploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_idPoint",
                table: "Expenses",
                column: "idPoint");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_idTrip",
                table: "Expenses",
                column: "idTrip");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_paidById",
                table: "Expenses",
                column: "paidById");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseShares_idExpense",
                table: "ExpenseShares",
                column: "idExpense");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseShares_idUser",
                table: "ExpenseShares",
                column: "idUser");

            migrationBuilder.CreateIndex(
                name: "IX_PointsOfInterest_addedById",
                table: "PointsOfInterest",
                column: "addedById");

            migrationBuilder.CreateIndex(
                name: "IX_PointsOfInterest_idTrip",
                table: "PointsOfInterest",
                column: "idTrip");

            migrationBuilder.CreateIndex(
                name: "IX_TripParticipants_idTrip",
                table: "TripParticipants",
                column: "idTrip");

            migrationBuilder.CreateIndex(
                name: "IX_TripParticipants_idUser",
                table: "TripParticipants",
                column: "idUser");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_createdById",
                table: "Trips",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_Users_idRole",
                table: "Users",
                column: "idRole");

            migrationBuilder.CreateIndex(
                name: "IX_UserVotes_idUser",
                table: "UserVotes",
                column: "idUser");

            migrationBuilder.CreateIndex(
                name: "IX_UserVotes_idVoteOption",
                table: "UserVotes",
                column: "idVoteOption");

            migrationBuilder.CreateIndex(
                name: "IX_VoteOptions_idVote",
                table: "VoteOptions",
                column: "idVote");

            migrationBuilder.CreateIndex(
                name: "IX_votingSystems_createdById",
                table: "votingSystems",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_votingSystems_idPoint",
                table: "votingSystems",
                column: "idPoint");

            migrationBuilder.CreateIndex(
                name: "IX_votingSystems_idTrip",
                table: "votingSystems",
                column: "idTrip");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "ExpenseShares");

            migrationBuilder.DropTable(
                name: "TripParticipants");

            migrationBuilder.DropTable(
                name: "UserVotes");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "VoteOptions");

            migrationBuilder.DropTable(
                name: "votingSystems");

            migrationBuilder.DropTable(
                name: "PointsOfInterest");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
