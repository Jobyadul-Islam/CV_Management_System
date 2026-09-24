using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuiltInKeysCascadeAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cvs_Positions_PositionId",
                table: "Cvs");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderSentAt",
                table: "Cvs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemKey",
                table: "Attributes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Backfill keys for built-ins seeded before SystemKey existed (matched by their seeded names).
            migrationBuilder.Sql("""
                UPDATE Attributes SET SystemKey = 'FirstName' WHERE IsBuiltIn = 1 AND Name = N'First Name';
                UPDATE Attributes SET SystemKey = 'LastName' WHERE IsBuiltIn = 1 AND Name = N'Last Name';
                UPDATE Attributes SET SystemKey = 'Location' WHERE IsBuiltIn = 1 AND Name = N'Location';
                UPDATE Attributes SET SystemKey = 'PersonalPhoto' WHERE IsBuiltIn = 1 AND Name = N'Personal Photo';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Attributes_SystemKey",
                table: "Attributes",
                column: "SystemKey",
                unique: true,
                filter: "[SystemKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Cvs_Positions_PositionId",
                table: "Cvs",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cvs_Positions_PositionId",
                table: "Cvs");

            migrationBuilder.DropIndex(
                name: "IX_Attributes_SystemKey",
                table: "Attributes");

            migrationBuilder.DropColumn(
                name: "LastReminderSentAt",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "SystemKey",
                table: "Attributes");

            migrationBuilder.AddForeignKey(
                name: "FK_Cvs_Positions_PositionId",
                table: "Cvs",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
