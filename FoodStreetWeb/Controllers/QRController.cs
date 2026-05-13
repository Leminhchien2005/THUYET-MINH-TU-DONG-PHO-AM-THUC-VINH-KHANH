using Microsoft.AspNetCore.Mvc;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
using FoodStreetWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace FoodStreetWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QRController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ScanHub> _hubContext;
        private const bool EnforceSingleUseQr = false;
        private const bool EnforceQrExpiration = false;

        public QRController(AppDbContext context, IHubContext<ScanHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // =======================================
        // TẠO QR MỚI
        // =======================================
        [HttpPost("generate")]
        public IActionResult GenerateQR([FromQuery] int poiId)
        {
            // Kiểm tra poi có tồn tại không
            var poi = _context.Pois.FirstOrDefault(x => x.Id == poiId);
            if (poi == null)
                return BadRequest("Nhà hàng không tồn tại");

            var code = Guid.NewGuid().ToString("N");

            var now = GetVietnamNow();

            var qr = new QRCodeEntity
            {
                Code = code,
                PoiId = poiId,  // Set PoiId từ parameter
                ExpireAt = now.AddMinutes(30)
            };

            _context.QRCodes.Add(qr);
            _context.SaveChanges();

            var qrUrl = $"{Request.Scheme}://{Request.Host}/api/qr/redeem/{code}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new BitmapByteQRCode(qrData);

            byte[] qrBytes = qrCode.GetGraphic(20);

            return File(qrBytes, "image/png");
        }

        // =======================================
        // SCAN QR
        // =======================================
        [HttpGet("redeem/{code}")]
        public async Task<IActionResult> RedeemQR(string code, [FromQuery] string? deviceId = null, [FromQuery] string? language = "vi")
        {
            var qr = _context.QRCodes.FirstOrDefault(x => x.Code == code);

            if (qr == null)
                return BadRequest("QR không tồn tại");

            if (EnforceSingleUseQr && qr.IsUsed)
                return BadRequest("QR đã được sử dụng");

            var now = GetVietnamNow();

            if (EnforceQrExpiration && qr.ExpireAt < now)
                return BadRequest("QR đã hết hạn");

            if (EnforceSingleUseQr)
            {
                qr.IsUsed = true;
                qr.UsedAt = now;
            }

            // Lưu scan log với UTC time để lọc đúng theo ngày
            _context.ScanLogs.Add(new ScanLog
            {
                DeviceId = string.IsNullOrWhiteSpace(deviceId) ? "unknown-device" : deviceId.Trim(),
                RestaurantId = qr.PoiId,
                ScanTime = DateTime.UtcNow  // Lưu dưới dạng UTC
            });

            // 🎙️ LOG NARRATION PLAYBACK
            try
            {
                var poi = await _context.Pois.FirstOrDefaultAsync(p => p.Id == qr.PoiId);
                var audio = poi != null 
                    ? await _context.AudioTranslations.FirstOrDefaultAsync(a => 
                        a.PoiId == qr.PoiId && a.LanguageCode == language)
                    : null;

                // Nếu có audio, log lần nghe thuyết minh
                if (audio != null && !string.IsNullOrWhiteSpace(audio.AudioUrl))
                {
                    _context.NarrationLogs.Add(new NarrationLog
                    {
                        RestaurantId = qr.PoiId,
                        PoiId = qr.PoiId,
                        Language = language ?? "vi",
                        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? "unknown-device" : deviceId.Trim(),
                        ListenTime = DateTime.UtcNow,  // Lưu dưới dạng UTC
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging narration: {ex.Message}");
            }

            _context.SaveChanges();

            // 🔥 BROADCAST SCAN EVENT + NARRATION DATA IN REAL-TIME
            try
            {
                // Lấy thông tin quán và thuyết minh âm thanh
                var poi = await _context.Pois.FirstOrDefaultAsync(p => p.Id == qr.PoiId);
                var audio = poi != null 
                    ? await _context.AudioTranslations.FirstOrDefaultAsync(a => 
                        a.PoiId == qr.PoiId && a.LanguageCode == language)
                    : null;

                var scanEvent = new
                {
                    restaurantId = qr.PoiId,
                    restaurantName = poi?.Name ?? "Unknown",
                    scanTime = now,
                    deviceId = deviceId ?? "unknown-device",
                    language = language ?? "vi",
                    audioUrl = audio?.AudioUrl ?? null,
                    crowdStatus = "updated" // Để notify front-end cần refresh heatmap
                };

                // Broadcast to all subscribers (both web and app)
                await _hubContext.Clients.Group("all-scans").SendAsync("OnScanReceived", scanEvent);
                await _hubContext.Clients.Group($"restaurant-{qr.PoiId}").SendAsync("OnScanReceived", scanEvent);
            }
            catch (Exception ex)
            {
                // SignalR not critical for operation, continue if it fails
                System.Diagnostics.Debug.WriteLine($"SignalR broadcast failed: {ex.Message}");
            }

            return Redirect($"/restaurant/{qr.PoiId}?scanLogged=true");
        }

        private static DateTime GetVietnamNow()
        {
            var utcNow = DateTime.UtcNow;
            var zoneIds = new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" };

            foreach (var zoneId in zoneIds)
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                    return TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return utcNow.AddHours(7);
        }
    }
}
