using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttributeValidationTuning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxLength",
                table: "Attributes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxValue",
                table: "Attributes",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinLength",
                table: "Attributes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinValue",
                table: "Attributes",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegexPattern",
                table: "Attributes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxLength",
                table: "Attributes");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "Attributes");

            migrationBuilder.DropColumn(
                name: "MinLength",
                table: "Attributes");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "Attributes");

            migrationBuilder.DropColumn(
                name: "RegexPattern",
                table: "Attributes");
        }
    }
}
