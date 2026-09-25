using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttributeIsOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOptional",
                table: "Attributes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing databases: the built-in Location field becomes optional (new ones get it from the seeder).
            migrationBuilder.Sql("UPDATE Attributes SET IsOptional = 1 WHERE SystemKey = 'Location';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOptional",
                table: "Attributes");
        }
    }
}
