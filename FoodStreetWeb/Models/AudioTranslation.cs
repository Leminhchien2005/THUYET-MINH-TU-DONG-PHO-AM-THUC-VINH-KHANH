namespace FoodStreetWeb.Models;

public class AudioTranslation
{
    public int Id { get; set; }

    public int PoiId { get; set; }

    public string LanguageCode { get; set; }

    public string? AudioUrl { get; set; }

    public Poi Poi { get; set; }
}