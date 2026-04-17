using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Models
{
    [Index(nameof(RestaurantId), nameof(ScanTime))]
    [Index(nameof(ScanTime))]
    public class ScanLog
    {
        public long Id { get; set; }

        public string DeviceId { get; set; } = string.Empty;

        public int RestaurantId { get; set; }

        public DateTime ScanTime { get; set; }
    }
}
