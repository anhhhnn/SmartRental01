using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SmartRental.Models
{
    public class Phongtro
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string TieuDe { get; set; } = string.Empty;

        [Required]
        public string DiaChi { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string KhuVuc { get; set; } = string.Empty;

        [Precision(18, 2)]
        [Range(typeof(decimal), "400000", "79228162514264337593543950335", ErrorMessage = "Giá phòng phải từ 400.000 VNĐ trở lên.")]
        public decimal Gia { get; set; }

        [Range(10, double.MaxValue, ErrorMessage = "Diện tích phòng phải từ 10 m² trở lên.")]
        public double DienTich { get; set; }

        public string? MoTa { get; set; }

        public string? HinhAnh { get; set; }

        public DateTime NgayDang { get; set; } = DateTime.Now;

        public bool TrangThai { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int SoLuongPhong { get; set; } = 1;

        [Range(1, int.MaxValue, ErrorMessage = "Số người ở tối đa phải từ 1 trở lên.")]
        public int SoNguoiToiDa { get; set; } = 2;

        // Kept separate from availability; a landlord may hide an available listing.
        public bool IsVisible { get; set; } = true;

        public string? OwnerId { get; set; }

        public ApplicationUser? Owner { get; set; }

        public ICollection<PhongTienNghi> PhongTienNghis { get; set; }
            = new List<PhongTienNghi>();

        public ICollection<YeuThich> YeuThichs { get; set; }
            = new List<YeuThich>();

        public ICollection<LichSuXem> LichSuXems { get; set; }
            = new List<LichSuXem>();

        public ICollection<DanhGia> DanhGias { get; set; }
            = new List<DanhGia>();

        public ICollection<PhongtroHinhAnh> HinhAnhs { get; set; } = new List<PhongtroHinhAnh>();
        public ICollection<CuocTroChuyen> CuocTroChuyens { get; set; } = new List<CuocTroChuyen>();
        public ICollection<TinNhan> TinNhans { get; set; } = new List<TinNhan>();
        public ICollection<LichXemPhong> LichXemPhongs { get; set; } = new List<LichXemPhong>();
    }
}
