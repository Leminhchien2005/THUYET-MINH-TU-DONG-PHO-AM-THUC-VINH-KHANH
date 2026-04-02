using SQLite;

namespace FoodStreetGuide.Models
{
    public class Poi
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string? Name { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // Bán kính kích hoạt (đơn vị: mét)
        public double Radius { get; set; }

        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        // Không lưu vào DB
        [Ignore]
        public double DistanceKm { get; set; }
    }
}