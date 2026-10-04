namespace SmartRental.ViewModels.Admin
{
    public class AdminFavoriteViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public int PhongtroId { get; set; }
        public string RoomTitle { get; set; } = string.Empty;
        public DateTime NgayThem { get; set; }
    }
}
