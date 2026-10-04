using System.ComponentModel.DataAnnotations;
namespace SmartRental.Models;
public class TinNhan
{
    public int Id { get; set; }
    public int CuocTroChuyenId { get; set; }
    public int? PhongtroId { get; set; }
    public string NguoiGuiId { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string NoiDung { get; set; } = string.Empty;
    public DateTime NgayGui { get; set; } = DateTime.UtcNow;
    public bool DaDoc { get; set; }
    [StringLength(30)] public string LoaiTinNhan { get; set; } = "Thuong";
    public CuocTroChuyen CuocTroChuyen { get; set; } = null!;
    public Phongtro? Phongtro { get; set; }
    public ApplicationUser NguoiGui { get; set; } = null!;
}
