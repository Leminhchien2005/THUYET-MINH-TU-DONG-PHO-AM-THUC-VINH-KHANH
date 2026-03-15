using FoodStreetWeb.Models;

namespace FoodStreetWeb.Models
{
    public class PoiListViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double Radius { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        public string OwnerName { get; set; }
    }
}