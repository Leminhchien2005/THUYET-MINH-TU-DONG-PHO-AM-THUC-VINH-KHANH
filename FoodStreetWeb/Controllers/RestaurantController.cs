using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FoodStreetWeb.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FoodStreetWeb.Controllers
{
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class RestaurantController : Controller
    {
        private readonly ILogger<RestaurantController> _logger;
        private readonly AppDbContext _context;
        private readonly IHubContext<ScanHub> _hubContext;

        public RestaurantController(
            ILogger<RestaurantController> logger,
            AppDbContext context,
            IHubContext<ScanHub> hubContext)
        {
            _logger = logger;
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("restaurant/{id}")]
        [HttpGet("{id}")]
        public async Task<IActionResult> Index(int id, [FromQuery] bool scanLogged = false, [FromQuery] string? deviceId = null)
        {
            _logger.LogInformation("QR Code scanned for restaurant: {RestaurantId}, IP: {IP}, UserAgent: {UserAgent}",
                id, HttpContext.Connection.RemoteIpAddress, Request.Headers["User-Agent"]);

            var restaurant = await _context.Pois
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null)
                return NotFound("Không tìm thấy quán.");

            if (!scanLogged)
            {
                var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId)
                    ? Request.Cookies["DeviceId"] ?? "web-visitor"
                    : deviceId.Trim();

                _context.ScanLogs.Add(new ScanLog
                {
                    DeviceId = normalizedDeviceId,
                    RestaurantId = id,
                    ScanTime = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                await BroadcastScanEventAsync(id, restaurant.Name, normalizedDeviceId);
            }

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

        [HttpGet("{id}/detail")]
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

            var nearbyCandidates = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .Where(p => p.Id != id && p.Status == PoiStatus.Approved)
                .ToListAsync();

            var nearbyRestaurants = nearbyCandidates
                .Select(p =>
                {
                    var trans = p.Translations.FirstOrDefault(t => t.LanguageCode == langCode)
                                ?? p.Translations.FirstOrDefault(t => t.LanguageCode == "vi");

                    return new
                    {
                        p.Id,
                        Name = trans?.Name ?? p.Name,
                        p.ImageUrl,
                        DistanceKm = CalculateDistanceKm(restaurant.Latitude, restaurant.Longitude, p.Latitude, p.Longitude)
                    };
                })
                .OrderBy(x => x.DistanceKm)
                .Take(6)
                .ToList();

            ViewBag.NearbyRestaurants = nearbyRestaurants;
            ViewBag.HasNearbyRestaurants = nearbyRestaurants.Count > 0;

            ViewBag.CurrentLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            return View("Detail", restaurant);
        }

        [HttpGet("tdetail")]
        [HttpGet("restaurant/tdetail")]
        public async Task<IActionResult> TDetail(string? deeplink = null, int? selectedId = null, string? qr = null)
        {
            if (string.Equals(qr, "tdetail", StringComparison.OrdinalIgnoreCase))
            {
                var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    deviceId = Request.Cookies["DeviceId"] ?? "web-visitor";
                }

                _context.ScanLogs.Add(new ScanLog
                {
                    DeviceId = deviceId.Trim(),
                    RestaurantId = 0,
                    ScanTime = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                await BroadcastScanEventAsync(0, "QR danh sách quán", deviceId.Trim());
            }

            var restaurants = await _context.Pois
                .AsNoTracking()
                .Where(p => p.Status == PoiStatus.Approved)
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();

            ViewBag.DeepLink = string.IsNullOrWhiteSpace(deeplink)
                ? "foodstreet://restaurants/tdetail"
                : deeplink;
            ViewBag.SelectedId = selectedId;

            return View(restaurants);
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                    * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double ToRadians(double degrees)
            => degrees * Math.PI / 180;

        private async Task BroadcastScanEventAsync(int restaurantId, string? restaurantName, string deviceId)
        {
            try
            {
                var scanEvent = new
                {
                    restaurantId,
                    restaurantName = restaurantName ?? "Unknown",
                    scanTime = DateTime.UtcNow,
                    deviceId,
                    language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                    audioUrl = (string?)null,
                    crowdStatus = "updated"
                };

                await _hubContext.Clients.Group("all-scans").SendAsync("OnScanReceived", scanEvent);
                await _hubContext.Clients.Group($"restaurant-{restaurantId}").SendAsync("OnScanReceived", scanEvent);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SignalR broadcast failed for restaurant scan {RestaurantId}", restaurantId);
            }
        }

        [HttpGet("test-cookie")]
        public IActionResult TestCookie()
        {
            var cookie = Request.Cookies["ASPNETCORE_CULTURE"];
            var culture = CultureInfo.CurrentUICulture.Name;
            return Content($"Cookie: {cookie ?? "null"}\nCurrentUICulture: {culture}");
        }

        [HttpPost("log-narration-listen")]
        public async Task<IActionResult> LogNarrationListen([FromBody] NarrationListenLogRequest request)
        {
            if (request == null || request.RestaurantId <= 0)
                return BadRequest("Invalid restaurant ID");

            try
            {
                var language = string.IsNullOrWhiteSpace(request.Language) ? "vi" : request.Language;
                var deviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? "unknown-device" : request.DeviceId.Trim();

                var narrationLog = new NarrationLog
                {
                    RestaurantId = request.RestaurantId,
                    PoiId = request.RestaurantId,
                    Language = language,
                    DeviceId = deviceId,
                    ListenTime = DateTime.UtcNow,  // Lưu dưới dạng UTC
                    CreatedUtc = DateTime.UtcNow
                };

                _context.NarrationLogs.Add(narrationLog);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error logging narration listen: {ex.Message}");
                return StatusCode(500, "Error logging narration");
            }
        }
    }

    public class NarrationListenLogRequest
    {
        public int RestaurantId { get; set; }
        public string Language { get; set; } = "vi";
        public string DeviceId { get; set; } = "unknown-device";
    }
}
