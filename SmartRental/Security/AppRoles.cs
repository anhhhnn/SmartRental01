namespace SmartRental.Security
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string ChuTro = "ChuTro";
        public const string NguoiThue = "NguoiThue";

        public const string CanManageRooms = Admin + "," + ChuTro;

        public static readonly string[] All =
        {
            Admin,
            ChuTro,
            NguoiThue
        };
    }
}
