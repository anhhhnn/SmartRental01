using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalMessagingBookingAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "Phongtros",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SoLuongPhong",
                table: "Phongtros",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CuocTroChuyens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhongtroId = table.Column<int>(type: "int", nullable: true),
                    NguoiThueId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChuTroId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LanCapNhatCuoi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuocTroChuyens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuocTroChuyens_AspNetUsers_ChuTroId",
                        column: x => x.ChuTroId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuocTroChuyens_AspNetUsers_NguoiThueId",
                        column: x => x.NguoiThueId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuocTroChuyens_Phongtros_PhongtroId",
                        column: x => x.PhongtroId,
                        principalTable: "Phongtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LichXemPhongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhongtroId = table.Column<int>(type: "int", nullable: false),
                    NguoiThueId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChuTroId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SoDienThoai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ThoiGianXem = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LyDoHuy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichXemPhongs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LichXemPhongs_AspNetUsers_ChuTroId",
                        column: x => x.ChuTroId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LichXemPhongs_AspNetUsers_NguoiThueId",
                        column: x => x.NguoiThueId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LichXemPhongs_Phongtros_PhongtroId",
                        column: x => x.PhongtroId,
                        principalTable: "Phongtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhongtroHinhAnhs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhongtroId = table.Column<int>(type: "int", nullable: false),
                    DuongDan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    IsAnhChinh = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhongtroHinhAnhs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhongtroHinhAnhs_Phongtros_PhongtroId",
                        column: x => x.PhongtroId,
                        principalTable: "Phongtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThongBaos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DaDoc = table.Column<bool>(type: "bit", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBaos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThongBaos_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TinNhans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuocTroChuyenId = table.Column<int>(type: "int", nullable: false),
                    NguoiGuiId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NgayGui = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaDoc = table.Column<bool>(type: "bit", nullable: false),
                    LoaiTinNhan = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinNhans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TinNhans_AspNetUsers_NguoiGuiId",
                        column: x => x.NguoiGuiId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TinNhans_CuocTroChuyens_CuocTroChuyenId",
                        column: x => x.CuocTroChuyenId,
                        principalTable: "CuocTroChuyens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuocTroChuyens_ChuTroId",
                table: "CuocTroChuyens",
                column: "ChuTroId");

            migrationBuilder.CreateIndex(
                name: "IX_CuocTroChuyens_NguoiThueId_ChuTroId_PhongtroId",
                table: "CuocTroChuyens",
                columns: new[] { "NguoiThueId", "ChuTroId", "PhongtroId" });

            migrationBuilder.CreateIndex(
                name: "IX_CuocTroChuyens_PhongtroId",
                table: "CuocTroChuyens",
                column: "PhongtroId");

            migrationBuilder.CreateIndex(
                name: "IX_LichXemPhongs_ChuTroId_TrangThai_ThoiGianXem",
                table: "LichXemPhongs",
                columns: new[] { "ChuTroId", "TrangThai", "ThoiGianXem" });

            migrationBuilder.CreateIndex(
                name: "IX_LichXemPhongs_NguoiThueId",
                table: "LichXemPhongs",
                column: "NguoiThueId");

            migrationBuilder.CreateIndex(
                name: "IX_LichXemPhongs_PhongtroId",
                table: "LichXemPhongs",
                column: "PhongtroId");

            migrationBuilder.CreateIndex(
                name: "IX_PhongtroHinhAnhs_PhongtroId_ThuTu",
                table: "PhongtroHinhAnhs",
                columns: new[] { "PhongtroId", "ThuTu" });

            migrationBuilder.CreateIndex(
                name: "IX_ThongBaos_UserId_DaDoc_NgayTao",
                table: "ThongBaos",
                columns: new[] { "UserId", "DaDoc", "NgayTao" });

            migrationBuilder.CreateIndex(
                name: "IX_TinNhans_CuocTroChuyenId_NgayGui",
                table: "TinNhans",
                columns: new[] { "CuocTroChuyenId", "NgayGui" });

            migrationBuilder.CreateIndex(
                name: "IX_TinNhans_NguoiGuiId",
                table: "TinNhans",
                column: "NguoiGuiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LichXemPhongs");

            migrationBuilder.DropTable(
                name: "PhongtroHinhAnhs");

            migrationBuilder.DropTable(
                name: "ThongBaos");

            migrationBuilder.DropTable(
                name: "TinNhans");

            migrationBuilder.DropTable(
                name: "CuocTroChuyens");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "Phongtros");

            migrationBuilder.DropColumn(
                name: "SoLuongPhong",
                table: "Phongtros");
        }
    }
}
