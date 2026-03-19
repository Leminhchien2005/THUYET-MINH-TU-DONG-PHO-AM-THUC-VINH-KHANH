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

        // GET: api/PoisApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Poi>>> GetPois()
        {
            // chỉ trả POI đã được admin duyệt
            return await _context.Pois
                .Where(p => p.Status == PoiStatus.Approved)
                .ToListAsync();
        }

        // GET: api/PoisApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Poi>> GetPoi(int id)
        {
            var poi = await _context.Pois
                .Where(p => p.Status == PoiStatus.Approved)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            return poi;
        }
    }
}