using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddLichSuXem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LichSuXems",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PhongtroId = table.Column<int>(type: "int", nullable: false),
                    LanXemCuoi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoLanXem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuXems", x => new { x.UserId, x.PhongtroId });
                    table.ForeignKey(
                        name: "FK_LichSuXems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LichSuXems_Phongtros_PhongtroId",
                        column: x => x.PhongtroId,
                        principalTable: "Phongtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LichSuXems_LanXemCuoi",
                table: "LichSuXems",
                column: "LanXemCuoi");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuXems_PhongtroId",
                table: "LichSuXems",
                column: "PhongtroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LichSuXems");
        }
    }
}
