namespace SmartRental.ViewModels.Admin
{
    public class AdminRoomViewModel
    {
        public int Id { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string? HinhAnh { get; set; }
        public decimal Gia { get; set; }
        public double DienTich { get; set; }
        public string OwnerName { get; set; } = "Chưa gán chủ phòng";
        public bool IsVisible { get; set; }
        public int SoLuongPhong { get; set; }
        public DateTime NgayDang { get; set; }
        public int FavoriteCount { get; set; }
    }
}
