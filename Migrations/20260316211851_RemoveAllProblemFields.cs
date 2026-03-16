using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWise.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAllProblemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Удаляем внешние ключи, если они существуют
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ChatMessages_PointsOfInterest_PointsOfInterestIdPoint')
                BEGIN
                    ALTER TABLE [ChatMessages] DROP CONSTRAINT [FK_ChatMessages_PointsOfInterest_PointsOfInterestIdPoint]
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ChatMessages_Trips_TripIdTrip')
                BEGIN
                    ALTER TABLE [ChatMessages] DROP CONSTRAINT [FK_ChatMessages_Trips_TripIdTrip]
                END
            ");

            // Удаляем индексы
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_PointsOfInterestIdPoint')
                BEGIN
                    DROP INDEX [IX_ChatMessages_PointsOfInterestIdPoint] ON [ChatMessages]
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_TripIdTrip')
                BEGIN
                    DROP INDEX [IX_ChatMessages_TripIdTrip] ON [ChatMessages]
                END
            ");

            // Удаляем колонки
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE name = 'PointsOfInterestIdPoint' AND object_id = OBJECT_ID('ChatMessages'))
                BEGIN
                    ALTER TABLE [ChatMessages] DROP COLUMN [PointsOfInterestIdPoint]
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE name = 'TripIdTrip' AND object_id = OBJECT_ID('ChatMessages'))
                BEGIN
                    ALTER TABLE [ChatMessages] DROP COLUMN [TripIdTrip]
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Здесь можно ничего не писать
        }
    }
}