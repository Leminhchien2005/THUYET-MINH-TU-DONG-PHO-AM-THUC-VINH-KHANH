using Microsoft.AspNetCore.Http;
namespace FoodStreetWeb.Models
{
    public class PoiEditViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; }
        public string? ImageUrl { get; set; }

        public List<FoodEditItem> Foods { get; set; }
        public IFormFile ImageFile { get; set; }
    }

    public class FoodEditItem
    {
        public int Id { get; set; }   // quan trọng để biết món nào update
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsExisting { get; set; }
        public IFormFile ImageFile { get; set; }
    }
}
