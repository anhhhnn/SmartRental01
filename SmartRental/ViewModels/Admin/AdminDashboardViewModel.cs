namespace SmartRental.ViewModels.Admin
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalLandlords { get; set; }
        public int TotalTenants { get; set; }
        public int TotalRooms { get; set; }
        public int ActiveRooms { get; set; }
        public int HiddenRooms { get; set; }
        public int TotalFavorites { get; set; }
        public int TotalAmenities { get; set; }
        public int TotalReviews { get; set; }
    }
}
