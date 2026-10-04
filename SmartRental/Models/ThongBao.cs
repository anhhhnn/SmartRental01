using System.ComponentModel.DataAnnotations;
namespace SmartRental.Models;
public class ThongBao
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    [Required, StringLength(200)] public string TieuDe { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string NoiDung { get; set; } = string.Empty;
    [StringLength(50)] public string Loai { get; set; } = string.Empty;
    public bool DaDoc { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
    [StringLength(500)] public string? Link { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
