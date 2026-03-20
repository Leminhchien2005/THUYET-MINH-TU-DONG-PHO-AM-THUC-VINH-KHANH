using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodStreetWeb.Migrations
{
    /// <inheritdoc />
    public partial class FixRadiusType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Radius",
                table: "PoiRequests",
                type: "double",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Radius",
                table: "PoiRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double");
        }
    }
}
