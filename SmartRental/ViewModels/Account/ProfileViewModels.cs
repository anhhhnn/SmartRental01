using System.ComponentModel.DataAnnotations;

namespace SmartRental.ViewModels.Account
{
    public class ProfileViewModel
    {
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? DiaChi { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }

    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        [Display(Name = "Họ tên")]
        public string HoTen { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string? DiaChi { get; set; }

        public string? Email { get; set; }
    }
}
