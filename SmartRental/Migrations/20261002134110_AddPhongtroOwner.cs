using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddPhongtroOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Phongtros",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Phongtros_OwnerId",
                table: "Phongtros",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Phongtros_AspNetUsers_OwnerId",
                table: "Phongtros",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Phongtros_AspNetUsers_OwnerId",
                table: "Phongtros");

            migrationBuilder.DropIndex(
                name: "IX_Phongtros_OwnerId",
                table: "Phongtros");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Phongtros");
        }
    }
}
