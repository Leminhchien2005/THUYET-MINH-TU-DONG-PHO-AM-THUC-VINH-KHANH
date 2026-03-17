namespace FoodStreetGuide.Models
{
    public class RouteCache
    {
        public int Id { get; set; }

        public double StartLat { get; set; }
        public double StartLon { get; set; }

        public double EndLat { get; set; }
        public double EndLon { get; set; }

        public string PointsJson { get; set; }
    }
}