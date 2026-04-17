namespace FoodStreetWeb.Models
{
    public class OnlineVisitorItemViewModel
    {
        public string VisitorId { get; set; }
        public string Role { get; set; }
        public bool IsFromQr { get; set; }
        public string LastPath { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }

    public class OnlineUsersStatsViewModel
    {
        public int TotalOnline { get; set; }
        public int QrOnline { get; set; }
        public int GuestOnline { get; set; }
        public int AdminOnline { get; set; }
        public int RestaurantOnline { get; set; }
        public List<OnlineVisitorItemViewModel> Visitors { get; set; } = new();
    }
}
