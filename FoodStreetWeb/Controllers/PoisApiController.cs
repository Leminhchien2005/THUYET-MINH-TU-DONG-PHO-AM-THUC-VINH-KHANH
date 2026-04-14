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

                ImageUrl = string.IsNullOrEmpty(poi.ImageUrl)
                    ? ""
                    : $"{Request.Scheme}://{Request.Host}{poi.ImageUrl}",

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

                    ImageUrl = string.IsNullOrEmpty(food.ImageUrl)
                        ? ""
                        : $"{Request.Scheme}://{Request.Host}{food.ImageUrl}",

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

                ImageUrl = string.IsNullOrEmpty(poi.ImageUrl)
                    ? ""
                    : $"{Request.Scheme}://{Request.Host}{poi.ImageUrl}",

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

                    ImageUrl = string.IsNullOrEmpty(food.ImageUrl)
                        ? ""
                        : $"{Request.Scheme}://{Request.Host}{food.ImageUrl}",

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
    }
}