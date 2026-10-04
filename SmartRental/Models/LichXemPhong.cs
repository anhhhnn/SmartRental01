using System.ComponentModel.DataAnnotations;
namespace SmartRental.Models;
public class LichXemPhong
{
    public int Id { get; set; }
    public int PhongtroId { get; set; }
    public string NguoiThueId { get; set; } = string.Empty;
    public string ChuTroId { get; set; } = string.Empty;
    [Required, StringLength(150)] public string HoTen { get; set; } = string.Empty;
    [StringLength(30)] public string? SoDienThoai { get; set; }
    public DateTime ThoiGianXem { get; set; }
    [StringLength(1000)] public string? GhiChu { get; set; }
    [StringLength(30)] public string TrangThai { get; set; } = TrangThaiLich.ChoXacNhan;
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
    [StringLength(500)] public string? LyDoHuy { get; set; }
    public Phongtro Phongtro { get; set; } = null!;
    public ApplicationUser NguoiThue { get; set; } = null!;
    public ApplicationUser ChuTro { get; set; } = null!;
}
public static class TrangThaiLich { public const string ChoXacNhan="ChoXacNhan"; public const string DaDongY="DaDongY"; public const string BaoBan="BaoBan"; public const string DaHuy="DaHuy"; }
