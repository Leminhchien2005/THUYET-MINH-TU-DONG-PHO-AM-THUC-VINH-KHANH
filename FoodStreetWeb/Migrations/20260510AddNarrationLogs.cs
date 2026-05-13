using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodStreetWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddNarrationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NarrationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Pomelo.EntityFrameworkCore.MySql:ValueGenerationStrategy", "IdentityColumn"),
                    RestaurantId = table.Column<int>(type: "int", nullable: false),
                    PoiId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "vi")
                        .Annotation("Pomelo.EntityFrameworkCore.MySql:CharSet", "utf8mb4"),
                    DeviceId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("Pomelo.EntityFrameworkCore.MySql:CharSet", "utf8mb4"),
                    ListenTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarrationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NarrationLogs_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Pomelo.EntityFrameworkCore.MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NarrationLogs_ListenTime",
                table: "NarrationLogs",
                column: "ListenTime");

            migrationBuilder.CreateIndex(
                name: "IX_NarrationLogs_RestaurantId_ListenTime",
                table: "NarrationLogs",
                columns: new[] { "RestaurantId", "ListenTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NarrationLogs");
        }
    }
}
