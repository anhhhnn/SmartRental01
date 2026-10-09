using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRental.Migrations
{
    /// <inheritdoc />
    public partial class AddPhongtroKhuVuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KhuVuc",
                table: "Phongtros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Phongtros]
                SET [KhuVuc] = CASE
                    WHEN [DiaChi] LIKE N'%Đồng Quang%' THEN N'Đồng Quang'
                    WHEN [DiaChi] LIKE N'%Túc Duyên%' THEN N'Túc Duyên'
                    WHEN [DiaChi] LIKE N'%Phan Đình Phùng%' THEN N'Phan Đình Phùng'
                    WHEN [DiaChi] LIKE N'%Trưng Vương%' THEN N'Trưng Vương'
                    WHEN [DiaChi] LIKE N'%Hoàng Văn Thụ%' THEN N'Hoàng Văn Thụ'
                    WHEN [DiaChi] LIKE N'%Quang Trung%' THEN N'Quang Trung'
                    WHEN [DiaChi] LIKE N'%Tân Thịnh%' THEN N'Tân Thịnh'
                    WHEN [DiaChi] LIKE N'%Tích Lương%' THEN N'Tích Lương'
                    WHEN [DiaChi] LIKE N'%Trung Thành%' THEN N'Trung Thành'
                    WHEN [DiaChi] LIKE N'%Gia Sàng%' THEN N'Gia Sàng'
                    WHEN [DiaChi] LIKE N'%Phú Xá%' THEN N'Phú Xá'
                    WHEN [DiaChi] LIKE N'%Cam Giá%' THEN N'Cam Giá'
                    WHEN [DiaChi] LIKE N'%Hương Sơn%' THEN N'Hương Sơn'
                    WHEN [DiaChi] LIKE N'%Quan Triều%' THEN N'Quan Triều'
                    WHEN [DiaChi] LIKE N'%Quang Vinh%' THEN N'Quang Vinh'
                    WHEN [DiaChi] LIKE N'%Đồng Bẩm%' THEN N'Đồng Bẩm'
                    WHEN [DiaChi] LIKE N'%Chùa Hang%' THEN N'Chùa Hang'
                    WHEN [DiaChi] LIKE N'%Thịnh Đán%' THEN N'Thịnh Đán'
                    WHEN [DiaChi] LIKE N'%Quyết Thắng%' THEN N'Quyết Thắng'
                    WHEN [DiaChi] LIKE N'%Sơn Cẩm%' THEN N'Sơn Cẩm'
                    WHEN [DiaChi] LIKE N'%Tân Long%' THEN N'Tân Long'
                    ELSE N'Đồng Quang'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "KhuVuc",
                table: "Phongtros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KhuVuc",
                table: "Phongtros");
        }
    }
}
