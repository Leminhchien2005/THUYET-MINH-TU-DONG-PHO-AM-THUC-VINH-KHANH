using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;

namespace FoodStreetWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PoisApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        private static readonly SemaphoreSlim _queue = new SemaphoreSlim(10);

        public PoisApiController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // GET: api/PoisApi
        // =========================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetPois()
        {
            Console.WriteLine($"WAIT  : {DateTime.Now:HH:mm:ss.fff}");

            await _queue.WaitAsync();

            Console.WriteLine($"START : {DateTime.Now:HH:mm:ss.fff}");

            try
            {
                var pois = await _context.Pois
                .Where(p => p.Status == PoiStatus.Approved)
                .Include(p => p.Translations)
                .Include(p => p.Foods)
                    .ThenInclude(f => f.Translations)
                .ToListAsync();

                var result = pois.Select(poi => new
                {
                    poi.Id,
                    poi.Name,
                    poi.Latitude,
                    poi.Longitude,
                    poi.Radius,
                    poi.Description,

                    ImageUrl = BuildPublicImageUrl(poi.ImageUrl),

                    poi.OwnerId,
                    poi.Status,
                    poi.DistanceKm,
                    poi.Priority,

                    Foods = poi.Foods.Select(food => new
                    {
                        food.Id,
                        food.Name,
                        food.Price,
                        food.Description,

                        ImageUrl = BuildPublicImageUrl(food.ImageUrl),

                        food.PoiId,

                        Translations = food.Translations.Select(t => new
                        {
                            t.Id,
                            t.FoodId,
                            t.LanguageCode,
                            t.Name,
                            t.Description
                        })
                    }),

                    Translations = poi.Translations.Select(t => new
                    {
                        t.Id,
                        t.PoiId,
                        t.LanguageCode,
                        t.Name,
                        t.Description
                    })
                });

                return Ok(result);
            }
            finally
            {
                Console.WriteLine($"END   : {DateTime.Now:HH:mm:ss.fff}");
                _queue.Release();
            }
        }


        // =========================
        // GET: api/PoisApi/5
        // =========================
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetPoi(int id)
        {
            var poi = await _context.Pois
                .Where(p => p.Status == PoiStatus.Approved)
                .Include(p => p.Translations)
                .Include(p => p.Foods)
                    .ThenInclude(f => f.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            var result = new
            {
                poi.Id,
                poi.Name,
                poi.Latitude,
                poi.Longitude,
                poi.Radius,
                poi.Description,

                ImageUrl = BuildPublicImageUrl(poi.ImageUrl),

                poi.OwnerId,
                poi.Status,
                poi.DistanceKm,
                poi.Priority,

                Foods = poi.Foods.Select(food => new
                {
                    food.Id,
                    food.Name,
                    food.Price,
                    food.Description,

                    ImageUrl = BuildPublicImageUrl(food.ImageUrl),

                    food.PoiId,

                    Translations = food.Translations.Select(t => new
                    {
                        t.Id,
                        t.FoodId,
                        t.LanguageCode,
                        t.Name,
                        t.Description
                    })
                }),

                Translations = poi.Translations.Select(t => new
                {
                    t.Id,
                    t.PoiId,
                    t.LanguageCode,
                    t.Name,
                    t.Description
                })
            };

            return Ok(result);
        }

        private string BuildPublicImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return string.Empty;

            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
                return imageUrl;

            if (!imageUrl.StartsWith('/'))
                imageUrl = "/" + imageUrl;

            return $"{Request.Scheme}://{Request.Host}{imageUrl}";
        }
    }
}