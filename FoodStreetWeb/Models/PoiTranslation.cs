using System.Text.Json.Serialization;

namespace FoodStreetWeb.Models
{
    public class PoiTranslation
    {
        public int Id { get; set; }

        public int PoiId { get; set; }

        public string LanguageCode { get; set; } = null!; // en, zh

        public string? Name { get; set; }

        public string? Description { get; set; }

        [JsonIgnore]
        public Poi? Poi { get; set; }
    }
}