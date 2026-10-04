using System.ComponentModel.DataAnnotations;

namespace SmartRental.ViewModels.Profile;

public class ProfileViewModel
{
    public string? HoTen { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DiaChi { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá {1} ký tự.")]
    [Display(Name = "Họ tên")]
    public string HoTen { get; set; } = string.Empty;
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }
    [StringLength(250, ErrorMessage = "Địa chỉ không được vượt quá {1} ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? DiaChi { get; set; }
    public string? Email { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, ErrorMessage = "Mật khẩu phải dài từ {2} đến {1} ký tự.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
