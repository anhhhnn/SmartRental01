namespace SmartRental.Models;

public class LichSuXem
{
    public string UserId { get; set; } = string.Empty;

    public int PhongtroId { get; set; }

    public DateTime LanXemCuoi { get; set; } = DateTime.UtcNow;

    public int SoLanXem { get; set; } = 1;

    public ApplicationUser User { get; set; } = null!;

    public Phongtro Phongtro { get; set; } = null!;
}
