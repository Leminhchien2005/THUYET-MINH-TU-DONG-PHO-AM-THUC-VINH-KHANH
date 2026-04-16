using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Index(int id)
        {
            _logger.LogInformation("QR Code scanned for restaurant: {RestaurantId}, IP: {IP}, UserAgent: {UserAgent}",
                id, HttpContext.Connection.RemoteIpAddress, Request.Headers["User-Agent"]);

            var restaurant = await _context.Pois
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null)
                return NotFound("Không tìm thấy quán.");

            // Lấy bản dịch theo ngôn ngữ hiện tại (nếu có)
            var langCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var translation = restaurant.Translations.FirstOrDefault(t => t.LanguageCode == langCode)
                              ?? restaurant.Translations.FirstOrDefault(t => t.LanguageCode == "vi");

            var restaurantData = new
            {
                Id = restaurant.Id,
                Name = translation?.Name ?? restaurant.Name,
                Description = translation?.Description ?? restaurant.Description,
                ImageUrl = restaurant.ImageUrl,
                WebDetailUrl = $"/restaurant/{id}/detail"
            };


            return View("Landing", restaurantData);
        }

        [HttpGet("restaurant/{id}/detail")]
        public async Task<IActionResult> WebDetail(int id, string lang = null)
        {

            // Ưu tiên tham số lang trên URL, nếu không có thì dùng cookie (hoặc mặc định)
            string langCode;
            if (!string.IsNullOrEmpty(lang) && (lang == "vi" || lang == "en" || lang == "zh"))
                langCode = lang;
            else
                langCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; // vẫn hỗ trợ cookie nếu có

            _logger.LogInformation($"WebDetail - langCode: {langCode}");

            var restaurant = await _context.Pois
                .Include(p => p.Translations)
                .Include(p => p.Foods.Where(f => !f.IsDeleted)).ThenInclude(f => f.Translations)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null) return NotFound();

            var poiTrans = restaurant.Translations.FirstOrDefault(t => t.LanguageCode == langCode)
                           ?? restaurant.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
            ViewBag.PoiName = poiTrans?.Name ?? restaurant.Name;
            ViewBag.PoiDescription = poiTrans?.Description ?? restaurant.Description;

            var foodsWithTranslation = restaurant.Foods
                .Where(f => !f.IsDeleted)
                .Select(f => new
            {
                f.Id,
                f.Price,
                f.ImageUrl,
                TranslatedName = f.Translations.FirstOrDefault(t => t.LanguageCode == langCode)?.Name
                                 ?? f.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                                 ?? f.Name,
                TranslatedDescription = f.Translations.FirstOrDefault(t => t.LanguageCode == langCode)?.Description
                                        ?? f.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Description
                                        ?? f.Description
            }).ToList();
            ViewBag.Foods = foodsWithTranslation;

            ViewBag.CurrentLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            return View("Detail", restaurant);
        }

        [HttpGet("test-cookie")]
        public IActionResult TestCookie()
        {
            var cookie = Request.Cookies["ASPNETCORE_CULTURE"];
            var culture = CultureInfo.CurrentUICulture.Name;
            return Content($"Cookie: {cookie ?? "null"}\nCurrentUICulture: {culture}");
        }
    }
}