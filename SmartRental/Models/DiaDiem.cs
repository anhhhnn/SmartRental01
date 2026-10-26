using System.ComponentModel.DataAnnotations;

namespace SmartRental.Models;

public class DiaDiem
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Ten { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Loai { get; set; }

    [MaxLength(300)]
    public string? DiaChi { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    [MaxLength(500)]
    public string? Alias { get; set; }

    public bool IsActive { get; set; } = true;
}
