using System.ComponentModel.DataAnnotations;

namespace SmartRental.Models
{
    public class DanhGia
    {
        public string UserId { get; set; } = string.Empty;

        public int PhongtroId { get; set; }

        [Range(1, 5, ErrorMessage = "Số sao phải từ 1 đến 5.")]
        public int SoSao { get; set; }

        [StringLength(1000, ErrorMessage = "Bình luận không được vượt quá 1000 ký tự.")]
        public string? NoiDung { get; set; }

        public DateTime NgayDanhGia { get; set; }

        public DateTime? NgayCapNhat { get; set; }

        public ApplicationUser User { get; set; } = null!;

        public Phongtro Phongtro { get; set; } = null!;
    }
}
