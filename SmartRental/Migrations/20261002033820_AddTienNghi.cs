using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddTienNghi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TienNghis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenTienNghi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TienNghis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhongTienNghis",
                columns: table => new
                {
                    PhongtroId = table.Column<int>(type: "int", nullable: false),
                    TienNghiId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhongTienNghis", x => new { x.PhongtroId, x.TienNghiId });
                    table.ForeignKey(
                        name: "FK_PhongTienNghis_Phongtros_PhongtroId",
                        column: x => x.PhongtroId,
                        principalTable: "Phongtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhongTienNghis_TienNghis_TienNghiId",
                        column: x => x.TienNghiId,
                        principalTable: "TienNghis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhongTienNghis_TienNghiId",
                table: "PhongTienNghis",
                column: "TienNghiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhongTienNghis");

            migrationBuilder.DropTable(
                name: "TienNghis");
        }
    }
}
