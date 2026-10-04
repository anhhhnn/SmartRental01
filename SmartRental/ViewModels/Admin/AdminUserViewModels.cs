namespace SmartRental.ViewModels.Admin
{
    public class AdminUserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? HoTen { get; set; }
        public string? PhoneNumber { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public bool IsLocked { get; set; }
    }

    public class AdminUserDetailsViewModel : AdminUserListItemViewModel
    {
        public string? DiaChi { get; set; }
        public int RoomCount { get; set; }
        public int FavoriteCount { get; set; }
        public string CreatedDateMessage { get; set; } = "Không có dữ liệu";
    }
}
