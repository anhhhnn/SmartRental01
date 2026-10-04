using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateConversationsByParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CuocTroChuyens_NguoiThueId_ChuTroId_PhongtroId",
                table: "CuocTroChuyens");

            migrationBuilder.AddColumn<int>(
                name: "PhongtroId",
                table: "TinNhans",
                type: "int",
                nullable: true);

            // Preserve the original room context, then merge per-room conversations into the oldest pair conversation.
            migrationBuilder.Sql(@"
UPDATE m SET PhongtroId = c.PhongtroId
FROM TinNhans m INNER JOIN CuocTroChuyens c ON c.Id = m.CuocTroChuyenId
WHERE m.PhongtroId IS NULL;

;WITH ConversationGroups AS (
    SELECT Id, MIN(Id) OVER (PARTITION BY NguoiThueId, ChuTroId) AS MainId
    FROM CuocTroChuyens
)
UPDATE m SET CuocTroChuyenId = g.MainId
FROM TinNhans m INNER JOIN ConversationGroups g ON g.Id = m.CuocTroChuyenId
WHERE g.Id <> g.MainId;

;WITH ConversationGroups AS (
    SELECT Id, MIN(Id) OVER (PARTITION BY NguoiThueId, ChuTroId) AS MainId
    FROM CuocTroChuyens
)
DELETE c FROM CuocTroChuyens c INNER JOIN ConversationGroups g ON g.Id = c.Id
WHERE g.Id <> g.MainId;");

            migrationBuilder.CreateIndex(
                name: "IX_TinNhans_PhongtroId",
                table: "TinNhans",
                column: "PhongtroId");

            migrationBuilder.CreateIndex(
                name: "IX_CuocTroChuyens_NguoiThueId_ChuTroId",
                table: "CuocTroChuyens",
                columns: new[] { "NguoiThueId", "ChuTroId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TinNhans_Phongtros_PhongtroId",
                table: "TinNhans",
                column: "PhongtroId",
                principalTable: "Phongtros",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TinNhans_Phongtros_PhongtroId",
                table: "TinNhans");

            migrationBuilder.DropIndex(
                name: "IX_TinNhans_PhongtroId",
                table: "TinNhans");

            migrationBuilder.DropIndex(
                name: "IX_CuocTroChuyens_NguoiThueId_ChuTroId",
                table: "CuocTroChuyens");

            migrationBuilder.DropColumn(
                name: "PhongtroId",
                table: "TinNhans");

            migrationBuilder.CreateIndex(
                name: "IX_CuocTroChuyens_NguoiThueId_ChuTroId_PhongtroId",
                table: "CuocTroChuyens",
                columns: new[] { "NguoiThueId", "ChuTroId", "PhongtroId" });
        }
    }
}
