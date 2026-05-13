namespace FoodStreetWeb.Models
{
    /// <summary>
    /// Track số lần nghe thuyết minh theo quán và ngôn ngữ
    /// </summary>
    public class NarrationLog
    {
        public long Id { get; set; }

        public int RestaurantId { get; set; }

        public int PoiId { get; set; }

        /// <summary>
        /// Ngôn ngữ của thuyết minh (vi, en, zh)
        /// </summary>
        public string Language { get; set; } = "vi";

        /// <summary>
        /// ID thiết bị/người dùng
        /// </summary>
        public string DeviceId { get; set; } = "unknown-device";

        /// <summary>
        /// Thời gian nghe thuyết minh (Vietnam time)
        /// </summary>
        public DateTime ListenTime { get; set; }

        /// <summary>
        /// Thời gian khi được tạo (UTC)
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        // Navigation
        public Poi? Poi { get; set; }
    }
}
