namespace SmartRental.Models
{
    public class YeuThich
    {
        public string UserId { get; set; } = string.Empty;

        public int PhongtroId { get; set; }

        public DateTime NgayThem { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; } = null!;

        public Phongtro Phongtro { get; set; } = null!;
    }
}
