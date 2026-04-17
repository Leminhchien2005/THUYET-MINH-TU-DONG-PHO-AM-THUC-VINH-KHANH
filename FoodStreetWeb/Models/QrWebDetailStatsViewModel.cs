namespace FoodStreetWeb.Models
{
    public class QrWebDetailAccessItemViewModel
    {
        public int PoiId { get; set; }
        public string PoiName { get; set; }
        public DateTime AccessedAtUtc { get; set; }
    }

    public class AppAccessItemViewModel
    {
        public string DeviceId { get; set; }
        public DateTime AccessedAtUtc { get; set; }
    }

    public class QrWebDetailPoiStatsViewModel
    {
        public int PoiId { get; set; }
        public string PoiName { get; set; }
        public int ScanCount { get; set; }
        public int WebDetailCount { get; set; }
    }

    public class QrWebDetailStatsViewModel
    {
        public int TotalScanCount { get; set; }
        public int TotalWebDetailCount { get; set; }
        public int TotalAppAccessCount { get; set; }
        public int RealtimeWindowMinutes { get; set; }
        public int RealtimeAccessCount { get; set; }
        public int RealtimeAppAccessCount { get; set; }
        public int RealtimeTotalAccessCount { get; set; }
        public List<QrWebDetailPoiStatsViewModel> Items { get; set; } = new();
        public List<QrWebDetailAccessItemViewModel> LatestAccesses { get; set; } = new();
        public List<AppAccessItemViewModel> LatestAppAccesses { get; set; } = new();
    }
}
