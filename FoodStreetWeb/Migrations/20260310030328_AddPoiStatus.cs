using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodStreetWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Pois",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Pois");
        }
    }
}
