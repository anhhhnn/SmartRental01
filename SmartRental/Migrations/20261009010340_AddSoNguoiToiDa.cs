using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddSoNguoiToiDa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SoNguoiToiDa",
                table: "Phongtros",
                type: "int",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoNguoiToiDa",
                table: "Phongtros");
        }
    }
}
