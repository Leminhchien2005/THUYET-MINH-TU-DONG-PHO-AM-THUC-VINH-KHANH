using FoodStreetWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private readonly AppDbContext _context;

    public AudioController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{poiId}/{lang}")]
    public async Task<IActionResult> GetAudio(
        int poiId,
        string lang)
    {
        var audio = await _context.AudioTranslations
            .FirstOrDefaultAsync(x =>
                x.PoiId == poiId &&
                x.LanguageCode == lang);

        if (audio == null ||
            string.IsNullOrWhiteSpace(audio.AudioUrl))
        {
            return NotFound();
        }

        return Ok(audio.AudioUrl);
    }
}