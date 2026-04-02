namespace FoodStreetGuide.Models;

public class FoodDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
    public int PoiId { get; set; }

    public List<FoodTranslationDto> Translations { get; set; }
}