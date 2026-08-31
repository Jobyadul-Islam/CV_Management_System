using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttributeUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttributeUsage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeUsage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeUsage_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttributeUsage_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeUsage_AttributeId",
                table: "AttributeUsage",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeUsage_UserId_AttributeId",
                table: "AttributeUsage",
                columns: new[] { "UserId", "AttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeUsage_UserId_LastUsedAt",
                table: "AttributeUsage",
                columns: new[] { "UserId", "LastUsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttributeUsage");
        }
    }
}
