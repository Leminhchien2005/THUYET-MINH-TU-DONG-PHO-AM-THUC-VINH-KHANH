using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodStreetWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "PoiRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PoiRequests_PoiId",
                table: "PoiRequests",
                column: "PoiId");

            migrationBuilder.AddForeignKey(
                name: "FK_PoiRequests_Pois_PoiId",
                table: "PoiRequests",
                column: "PoiId",
                principalTable: "Pois",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PoiRequests_Pois_PoiId",
                table: "PoiRequests");

            migrationBuilder.DropIndex(
                name: "IX_PoiRequests_PoiId",
                table: "PoiRequests");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "PoiRequests");
        }
    }
}
