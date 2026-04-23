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

        [HttpPost("enter-zone")]
        public IActionResult EnterZone([FromBody] EnterZoneRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                return BadRequest(new { ok = false });

            var normalizedDeviceId = request.DeviceId.Trim();
            if (!_onlineDeviceStore.IsOnline(normalizedDeviceId))
                return Ok(new { ok = true });

            var restaurantIds = (request.RestaurantIds ?? new List<int>())
                .Append(request.RestaurantId)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (restaurantIds.Count == 0)
                return BadRequest(new { ok = false });

            _onlineDeviceStore.UpdateDeviceZone(normalizedDeviceId, restaurantIds);
            return Ok(new { ok = true });
        }

        [HttpGet("online-device-zones")]
        public async Task<IActionResult> GetOnlineDeviceZones([FromQuery] int take = 200)
        {
            take = Math.Clamp(take, 1, 1000);

            var now = DateTime.UtcNow;
            var webCutoff = now.AddSeconds(-15);

            var appOnline = _onlineDeviceStore.GetOnlineDevices()
                .Select(x => new OnlineDeviceZoneRow
                {
                    DeviceId = x.DeviceId,
                    Source = "app",
                    LastSeenUtc = x.ConnectedAtUtc
                })
                .ToList();

            var appZones = _onlineDeviceStore.GetDeviceZones();

            var latestWebPresence = await _dbContext.OnlineWebPresences
                .AsNoTracking()
                .Where(x => x.LastSeenUtc >= webCutoff)
                .GroupBy(x => x.DeviceId)
                .Select(g => g
                    .OrderByDescending(x => x.LastSeenUtc)
                    .Select(x => new LatestDeviceRestaurantSeen
                    {
                        DeviceId = x.DeviceId,
                        RestaurantId = x.RestaurantId,
                        SeenAtUtc = x.LastSeenUtc
                    })
                    .FirstOrDefault()!)
                .ToListAsync();

            var restaurantIds = appZones.Values
                .SelectMany(x => x.RestaurantIds)
                .Concat(latestWebPresence.Where(x => x != null).Select(x => x!.RestaurantId))
                .Distinct()
                .ToList();

            var poiNames = restaurantIds.Count == 0
                ? new Dictionary<int, string>()
                : await _dbContext.Pois
                    .AsNoTracking()
                    .Where(x => restaurantIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })
                    .ToDictionaryAsync(x => x.Id, x => x.Name ?? $"POI #{x.Id}");

            var appRows = appOnline.Select(x =>
            {
                appZones.TryGetValue(x.DeviceId, out var zone);
                var zoneRestaurantIds = zone?.RestaurantIds?.ToList() ?? new List<int>();

                return zoneRestaurantIds.Select(restaurantId => new OnlineDeviceZoneRow
                {
                    DeviceId = x.DeviceId,
                    Source = x.Source,
                    RestaurantId = restaurantId,
                    RestaurantName = poiNames.TryGetValue(restaurantId, out var name)
                        ? name
                        : "-",
                    LastSeenUtc = zone?.UpdatedAtUtc ?? x.LastSeenUtc
                });
            })
            .SelectMany(x => x)
            .Where(x => x.RestaurantId > 0);

            var webRows = latestWebPresence
                .Where(x => x != null)
                .Select(x => new OnlineDeviceZoneRow
                {
                    DeviceId = x!.DeviceId,
                    Source = "web",
                    RestaurantId = x.RestaurantId,
                    RestaurantName = poiNames.TryGetValue(x.RestaurantId, out var name) ? name : "-",
                    LastSeenUtc = x.SeenAtUtc
                });

            var items = appRows
                .Concat(webRows)
                .OrderByDescending(x => x.LastSeenUtc)
                .Take(take)
                .ToList();

            return Ok(new { count = items.Count, items });
        }

        private class LatestDeviceRestaurantSeen
        {
            public string DeviceId { get; set; } = string.Empty;
            public int RestaurantId { get; set; }
            public DateTime SeenAtUtc { get; set; }
        }

        private class OnlineDeviceZoneRow
        {
            public string DeviceId { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public int? RestaurantId { get; set; }
            public string RestaurantName { get; set; } = "-";
            public DateTime LastSeenUtc { get; set; }
        }

        public class EnterZoneRequest
        {
            public string DeviceId { get; set; } = string.Empty;
            public int RestaurantId { get; set; }
            public List<int> RestaurantIds { get; set; } = new();
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
