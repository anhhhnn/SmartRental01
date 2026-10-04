namespace SmartRental.ViewModels.Admin
{
    public class AdminDanhGiaViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int PhongtroId { get; set; }
        public string RoomTitle { get; set; } = string.Empty;
        public int SoSao { get; set; }
        public string? NoiDung { get; set; }
        public DateTime NgayDanhGia { get; set; }
    }
}
