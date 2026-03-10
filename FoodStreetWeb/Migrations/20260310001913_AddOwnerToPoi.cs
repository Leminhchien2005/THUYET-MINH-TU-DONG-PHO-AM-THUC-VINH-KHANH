using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodStreetWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerToPoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Pois",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Pois_OwnerId",
                table: "Pois",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pois_AspNetUsers_OwnerId",
                table: "Pois",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pois_AspNetUsers_OwnerId",
                table: "Pois");

            migrationBuilder.DropIndex(
                name: "IX_Pois_OwnerId",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Pois");
        }
    }
}
