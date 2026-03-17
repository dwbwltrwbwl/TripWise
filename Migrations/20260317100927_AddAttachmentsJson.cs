using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachmentsJson",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attachmentsJson",
                table: "ChatMessages");
        }
    }
}