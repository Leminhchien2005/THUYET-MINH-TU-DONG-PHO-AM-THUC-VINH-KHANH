using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FoodStreetWeb.Data;
using System.Linq;

namespace FoodStreetWeb.Controllers
{
    public class RestaurantController : Controller
    {
        private readonly ILogger<RestaurantController> _logger;
        private readonly AppDbContext _context; 

        public RestaurantController(ILogger<RestaurantController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context; 
        }

        [HttpGet("restaurant/{id}")]
        public IActionResult Index(int id) 
        {
            _logger.LogInformation("QR Code scanned for restaurant: {RestaurantId}, IP: {IP}, UserAgent: {UserAgent}", 
                id, HttpContext.Connection.RemoteIpAddress, Request.Headers["User-Agent"]);

            // Truy xuất thông tin từ database (Sử dụng bảng Pois thay vì Restaurants)
            var restaurant = _context.Pois.FirstOrDefault(r => r.Id == id);

            if(restaurant == null)
            {
                return NotFound("Không tìm thấy quán.");
            }

            var restaurantData = new 
            {
                Id = restaurant.Id,
                Name = restaurant.Name,
                Description = restaurant.Description,
                ImageUrl = restaurant.ImageUrl,
                WebDetailUrl = $"/restaurant/{id}/detail"
            };

            return View("Landing", restaurantData);
        }

        [HttpGet("restaurant/{id}/detail")]
        public IActionResult WebDetail(int id)
        {
            // Lấy dữ liệu thực tế từ database
            var restaurant = _context.Pois.FirstOrDefault(r => r.Id == id);

            if(restaurant == null)
            {
                return NotFound("Không tìm thấy quán.");
            }

            // Trả về view kèm model là Object Poi thực tế
            return View("Detail", restaurant);
        }
    }
}