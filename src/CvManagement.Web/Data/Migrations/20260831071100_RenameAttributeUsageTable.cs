using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameAttributeUsageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttributeUsage_AspNetUsers_UserId",
                table: "AttributeUsage");

            migrationBuilder.DropForeignKey(
                name: "FK_AttributeUsage_Attributes_AttributeId",
                table: "AttributeUsage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AttributeUsage",
                table: "AttributeUsage");

            migrationBuilder.RenameTable(
                name: "AttributeUsage",
                newName: "AttributeUsages");

            migrationBuilder.RenameIndex(
                name: "IX_AttributeUsage_UserId_LastUsedAt",
                table: "AttributeUsages",
                newName: "IX_AttributeUsages_UserId_LastUsedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AttributeUsage_UserId_AttributeId",
                table: "AttributeUsages",
                newName: "IX_AttributeUsages_UserId_AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_AttributeUsage_AttributeId",
                table: "AttributeUsages",
                newName: "IX_AttributeUsages_AttributeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AttributeUsages",
                table: "AttributeUsages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AttributeUsages_AspNetUsers_UserId",
                table: "AttributeUsages",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttributeUsages_Attributes_AttributeId",
                table: "AttributeUsages",
                column: "AttributeId",
                principalTable: "Attributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttributeUsages_AspNetUsers_UserId",
                table: "AttributeUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_AttributeUsages_Attributes_AttributeId",
                table: "AttributeUsages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AttributeUsages",
                table: "AttributeUsages");

            migrationBuilder.RenameTable(
                name: "AttributeUsages",
                newName: "AttributeUsage");

            migrationBuilder.RenameIndex(
                name: "IX_AttributeUsages_UserId_LastUsedAt",
                table: "AttributeUsage",
                newName: "IX_AttributeUsage_UserId_LastUsedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AttributeUsages_UserId_AttributeId",
                table: "AttributeUsage",
                newName: "IX_AttributeUsage_UserId_AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_AttributeUsages_AttributeId",
                table: "AttributeUsage",
                newName: "IX_AttributeUsage_AttributeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AttributeUsage",
                table: "AttributeUsage",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AttributeUsage_AspNetUsers_UserId",
                table: "AttributeUsage",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttributeUsage_Attributes_AttributeId",
                table: "AttributeUsage",
                column: "AttributeId",
                principalTable: "Attributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
