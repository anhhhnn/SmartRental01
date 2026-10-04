namespace SmartRental.Models
{
    public class PhongTienNghi
    {
        public int PhongtroId { get; set; }

        public Phongtro Phongtro { get; set; } = null!;


        public int TienNghiId { get; set; }

        public TienNghi TienNghi { get; set; } = null!;
    }
}