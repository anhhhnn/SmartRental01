namespace SmartRental.ViewModels.Admin
{
    public class AdminDashboardViewModel
    {
        public int SelectedYear { get; set; }
        public IReadOnlyList<int> AvailableYears { get; set; } = [];
        public int TotalUsers { get; set; }
        public int TotalLandlords { get; set; }
        public int TotalTenants { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
        public int UnavailableRooms { get; set; }
        public int TotalFavorites { get; set; }
        public long TotalViews { get; set; }
        public int PendingBookings { get; set; }
        public int TotalReviews { get; set; }
        public int[] MonthlyNewRooms { get; set; } = new int[12];
        public int[] RoomStatusDistribution { get; set; } = new int[2];
        public int[] UserRoleDistribution { get; set; } = new int[3];
        public int[] RatingDistribution { get; set; } = new int[5];
        public int[] BookingStatusDistribution { get; set; } = new int[4];
        public IReadOnlyList<AdminTopRoomViewModel> TopRooms { get; set; } = [];
        public IReadOnlyList<AdminRecentRoomViewModel> RecentRooms { get; set; } = [];
    }

    public class AdminTopRoomViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public long ViewCount { get; set; }
        public int FavoriteCount { get; set; }
        public double? AverageRating { get; set; }
        public int RoomQuantity { get; set; }
    }

    public class AdminRecentRoomViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime PostedAt { get; set; }
        public decimal Price { get; set; }
        public int RoomQuantity { get; set; }
    }
}
