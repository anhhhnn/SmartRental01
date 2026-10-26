using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomCoordinatesAndLandmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Phongtros",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Phongtros",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiaDiems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaDiems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaDiems_Ten",
                table: "DiaDiems",
                column: "Ten",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaDiems");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Phongtros");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Phongtros");
        }
    }
}
