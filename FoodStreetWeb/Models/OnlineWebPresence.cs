namespace FoodStreetWeb.Models
{
    public class OnlineWebPresence
    {
        public string PresenceId { get; set; } = string.Empty;
        public string VisitorId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string TabId { get; set; } = string.Empty;
        public int RestaurantId { get; set; }
        public string Role { get; set; } = "Du khách";
        public bool IsFromQr { get; set; }
        public string LastPath { get; set; } = "/";
        public DateTime LastSeenUtc { get; set; }
    }
}