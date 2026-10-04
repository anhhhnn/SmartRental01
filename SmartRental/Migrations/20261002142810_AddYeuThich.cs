using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    public partial class AddYeuThich : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YeuThichs",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PhongtroId = table.Column<int>(type: "int", nullable: false),
                    NgayThem = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuThichs", x => new { x.UserId, x.PhongtroId });
                    table.ForeignKey(name: "FK_YeuThichs_AspNetUsers_UserId", column: x => x.UserId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_YeuThichs_Phongtros_PhongtroId", column: x => x.PhongtroId, principalTable: "Phongtros", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_YeuThichs_PhongtroId", table: "YeuThichs", column: "PhongtroId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "YeuThichs");
        }
    }
}
