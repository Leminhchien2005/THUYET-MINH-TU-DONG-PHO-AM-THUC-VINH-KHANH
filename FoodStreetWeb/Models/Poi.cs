using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodStreetWeb.Models
{
    public class Poi
    {
        [Key] // tương đương PrimaryKey
        public int Id { get; set; }

        public string? Name { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // Bán kính kích hoạt (đơn vị: mét)
        public double Radius { get; set; }

        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        // Không lưu vào DB
        [NotMapped] // tương đương Ignore
        public double DistanceKm { get; set; }
    }
}