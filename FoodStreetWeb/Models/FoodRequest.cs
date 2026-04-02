namespace FoodStreetWeb.Models
{
    public class FoodRequest
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }

        public int PoiRequestId { get; set; }

        public int? FoodId { get; set; }
    }
}