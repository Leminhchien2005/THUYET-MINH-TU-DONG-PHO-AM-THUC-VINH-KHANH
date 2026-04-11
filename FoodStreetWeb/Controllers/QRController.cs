using Microsoft.AspNetCore.Mvc;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
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

        public QRController(AppDbContext context)
        {
            _context = context;
        }

        // =======================================
        // TẠO QR MỚI
        // =======================================
        [HttpPost("generate")]
        public IActionResult GenerateQR()
        {
            var code = Guid.NewGuid().ToString("N");

            var qr = new QRCodeEntity
            {
                Code = code,
                ExpireAt = DateTime.UtcNow.AddMinutes(30)
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
        public IActionResult RedeemQR(string code)
        {
            var qr = _context.QRCodes.FirstOrDefault(x => x.Code == code);

            if (qr == null)
                return BadRequest("QR không tồn tại");

            if (qr.IsUsed)
                return BadRequest("QR đã được sử dụng");

            if (qr.ExpireAt < DateTime.UtcNow)
                return BadRequest("QR đã hết hạn");

            qr.IsUsed = true;
            qr.UsedAt = DateTime.UtcNow;

            _context.SaveChanges();

            return Redirect($"/restaurant/{qr.PoiId}");
        }
    }
}