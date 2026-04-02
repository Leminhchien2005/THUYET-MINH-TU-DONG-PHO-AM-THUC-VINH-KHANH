using System.Text.Json.Serialization;

namespace FoodStreetWeb.Models
{
    public class FoodTranslation
    {
        public int Id { get; set; }

        public int FoodId { get; set; }

        public string LanguageCode { get; set; } = null!;

        public string? Name { get; set; }

        public string? Description { get; set; }

        [JsonIgnore]
        public Food? Food { get; set; }
    }
}