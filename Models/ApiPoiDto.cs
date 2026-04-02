namespace FoodStreetGuide.Models;

public class ApiPoiDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }

    public List<FoodDto> Foods { get; set; } = new();
    public List<ApiPoiTranslationDto> Translations { get; set; }
}