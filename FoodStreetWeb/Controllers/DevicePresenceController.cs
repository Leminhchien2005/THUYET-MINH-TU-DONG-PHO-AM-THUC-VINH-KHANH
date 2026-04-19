using FoodStreetWeb.Data;
using FoodStreetWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DevicePresenceController : ControllerBase
    {
        private readonly OnlineDeviceStore _onlineDeviceStore;
        private readonly AppDbContext _dbContext;

        public DevicePresenceController(OnlineDeviceStore onlineDeviceStore, AppDbContext dbContext)
        {
            _onlineDeviceStore = onlineDeviceStore;
            _dbContext = dbContext;
        }

        [HttpGet("online-devices")]
        public IActionResult GetOnlineDevices()
        {
            var devices = _onlineDeviceStore.GetOnlineDevices();
            return Ok(new { count = devices.Count, devices });
        }

        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] DeviceHeartbeatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                return BadRequest("DeviceId không hợp lệ");

            _onlineDeviceStore.TouchHeartbeat(request.DeviceId.Trim());
            return Ok(new { ok = true });
        }

        [HttpGet("is-online/{deviceId}")]
        public IActionResult IsOnline(string deviceId)
        {
            return Ok(new { deviceId, online = _onlineDeviceStore.IsOnline(deviceId) });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetConnectionHistory(
            [FromQuery] string? deviceId,
            [FromQuery] string? source,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? take = null)
        {
            // backward-compatible: nếu client cũ truyền take thì dùng take như pageSize của trang 1
            if (take.HasValue && take.Value > 0)
            {
                page = 1;
                pageSize = take.Value;
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _dbContext.DeviceConnectionHistories.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var normalized = deviceId.Trim();
                query = query.Where(x => x.DeviceId == normalized);
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                var normalizedSource = source.Trim().ToLowerInvariant();
                if (normalizedSource == "web")
                {
                    query = query.Where(x => x.ConnectionId.StartsWith("web:"));
                }
                else if (normalizedSource == "app")
                {
                    query = query.Where(x => !x.ConnectionId.StartsWith("web:"));
                }
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(x => x.EventTimeUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => x.EventTimeUtc <= toUtc.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .Select(x => new
                {
                    x.Id,
                    x.DeviceId,
                    x.ConnectionId,
                    Source = x.ConnectionId.StartsWith("web:") ? "web" : "app",
                    x.EventType,
                    x.EventTimeUtc,
                    x.Note,
                    IsConnectedPriority = x.EventType == "connect"
                        && !query.Any(y => y.DeviceId == x.DeviceId
                            && y.ConnectionId == x.ConnectionId
                            && (y.EventTimeUtc > x.EventTimeUtc
                                || (y.EventTimeUtc == x.EventTimeUtc && y.Id > x.Id)))
                })
                .OrderByDescending(x => x.IsConnectedPriority)
                .ThenByDescending(x => x.EventTimeUtc)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                count = items.Count,
                totalCount,
                page,
                pageSize,
                totalPages,
                items
            });
        }

        [HttpGet("history-devices")]
        public async Task<IActionResult> GetHistoryDevices(
            [FromQuery] string? source,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int take = 500)
        {
            take = Math.Clamp(take, 1, 2000);

            var query = _dbContext.DeviceConnectionHistories.AsNoTracking();

            if (fromUtc.HasValue)
            {
                query = query.Where(x => x.EventTimeUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => x.EventTimeUtc <= toUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                var normalizedSource = source.Trim().ToLowerInvariant();
                if (normalizedSource == "web")
                {
                    query = query.Where(x => x.ConnectionId.StartsWith("web:"));
                }
                else if (normalizedSource == "app")
                {
                    query = query.Where(x => !x.ConnectionId.StartsWith("web:"));
                }
            }

            var devices = await query
                .Select(x => x.DeviceId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .Take(take)
                .ToListAsync();

            return Ok(new { count = devices.Count, devices });
        }

        public class DeviceHeartbeatRequest
        {
            public string DeviceId { get; set; } = string.Empty;
        }
    }
}
