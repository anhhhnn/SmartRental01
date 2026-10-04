using Microsoft.AspNetCore.Identity;

namespace SmartRental.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? HoTen { get; set; }

        public string? DiaChi { get; set; }

        public ICollection<Phongtro> Phongtros { get; set; }
            = new List<Phongtro>();

        public ICollection<YeuThich> YeuThichs { get; set; }
            = new List<YeuThich>();

        public ICollection<LichSuXem> LichSuXems { get; set; }
            = new List<LichSuXem>();

        public ICollection<DanhGia> DanhGias { get; set; }
            = new List<DanhGia>();

        public ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();
    }
}
