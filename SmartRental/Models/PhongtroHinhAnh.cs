using System.ComponentModel.DataAnnotations;

namespace SmartRental.Models;

public class PhongtroHinhAnh
{
    public int Id { get; set; }
    public int PhongtroId { get; set; }
    [Required, StringLength(500)] public string DuongDan { get; set; } = string.Empty;
    public int ThuTu { get; set; }
    public bool IsAnhChinh { get; set; }
    public Phongtro Phongtro { get; set; } = null!;
}
