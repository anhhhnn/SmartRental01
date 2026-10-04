namespace SmartRental.Models;
public class CuocTroChuyen
{
    public int Id { get; set; }
    public int? PhongtroId { get; set; }
    public string NguoiThueId { get; set; } = string.Empty;
    public string ChuTroId { get; set; } = string.Empty;
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
    public DateTime LanCapNhatCuoi { get; set; } = DateTime.UtcNow;
    public Phongtro? Phongtro { get; set; }
    public ApplicationUser NguoiThue { get; set; } = null!;
    public ApplicationUser ChuTro { get; set; } = null!;
    public ICollection<TinNhan> TinNhans { get; set; } = new List<TinNhan>();
}
