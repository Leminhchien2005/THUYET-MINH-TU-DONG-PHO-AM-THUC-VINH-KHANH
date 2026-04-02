using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace FoodStreetWeb.Models
{
    public class CreatePoiWithFoodsViewModel
    {
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; }
        public string Description { get; set; }
        public string? ImageUrl { get; set; }

        public List<FoodInputModel> Foods { get; set; } = new();

        public IFormFile ImageFile { get; set; }
    }

    public class FoodInputModel
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public string? ImageUrl { get; set; }

        public IFormFile ImageFile { get; set; }
    }
}