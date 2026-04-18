using SQLite;

namespace FoodStreetGuide.Models;

public class PoiTranslation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int PoiId { get; set; }

    public string LanguageCode { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
}