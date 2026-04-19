namespace FoodStreetWeb.Models
{
    public class DeviceConnectionHistory
    {
        public long Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // connect | disconnect
        public DateTime EventTimeUtc { get; set; }
        public string? Note { get; set; }
    }
}
