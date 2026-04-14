namespace FoodStreetWeb.Models
{
    public class FoodRequest
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; } = "/images/default.png";

        public int PoiRequestId { get; set; }

        public int? FoodId { get; set; }

        public FoodRequestType RequestType { get; set; } = FoodRequestType.Create;
    }

    public enum FoodRequestType
    {
        Create = 0,
        Update = 1,
        Delete = 2
    }
}