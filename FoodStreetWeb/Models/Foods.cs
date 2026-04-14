using System.Text.Json.Serialization;

namespace FoodStreetWeb.Models
{
    public class Food
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public string ImageUrl { get; set; } = "/images/default.png";

        public bool IsDeleted { get; set; } = false;

        public int PoiId { get; set; }

        [JsonIgnore]
        public Poi Poi { get; set; }

        public ICollection<FoodTranslation>? Translations { get; set; }
    }
}