using FoodStreetWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private readonly AppDbContext _context;

    // tối đa 10 request tải audio cùng lúc
    private static readonly SemaphoreSlim _queue =
        new SemaphoreSlim(10);

    // debug
    private static int _running = 0;

    // dùng chung HttpClient
    private static readonly HttpClient _httpClient =
        new HttpClient();

    public AudioController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{poiId}/{lang}")]
    public async Task<IActionResult> GetAudio(
        int poiId,
        string lang)
    {
        Console.WriteLine(
            $"WAIT  : {DateTime.Now:HH:mm:ss.fff}"
        );

        await _queue.WaitAsync();

        Interlocked.Increment(ref _running);

        Console.WriteLine(
            $"START : {DateTime.Now:HH:mm:ss.fff} | RUNNING = {_running}"
        );

        try
        {
            var audio = await _context.AudioTranslations
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.PoiId == poiId &&
                    x.LanguageCode == lang);

            if (audio == null ||
                string.IsNullOrWhiteSpace(audio.AudioUrl))
            {
                return NotFound();
            }

            // tải FULL audio từ cloudinary
            var bytes = await _httpClient.GetByteArrayAsync(
                audio.AudioUrl
            );

            Console.WriteLine(
                $"PLAY  : {DateTime.Now:HH:mm:ss.fff} | RUNNING = {_running}"
            );

            // trả binary audio
            return File(
                bytes,
                "audio/wav"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"ERROR : {ex}"
            );

            return StatusCode(500, ex.ToString());
        }
        finally
        {
            Interlocked.Decrement(ref _running);

            Console.WriteLine(
                $"END   : {DateTime.Now:HH:mm:ss.fff} | RUNNING = {_running}"
            );

            _queue.Release();
        }
    }
}