using SQLite;

namespace FoodStreetGuide.Models;

public class FoodTranslation
{
    [PrimaryKey]
    public int Id { get; set; }

    public int FoodId { get; set; }

    public string LanguageCode { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
}