using System.ComponentModel.DataAnnotations;

namespace FoodStreetWeb.Models
{
    public enum PoiRequestType
    {
        Create,
        Update,
        Delete
    }

    public class PoiRequest
    {
        public int Id { get; set; }

        public int? PoiId { get; set; }

        public PoiRequestType RequestType { get; set; }

        public string OwnerId { get; set; }

        public string Name { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double Radius { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; } = "/images/default.png";

        public PoiStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public Poi? Poi { get; set; }

        public string? RejectReason { get; set; }
    }
}
