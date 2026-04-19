using Microsoft.AspNetCore.Mvc;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
using FoodStreetWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OnlineController : ControllerBase
{
    private readonly OnlineUsersService _onlineService;
    private readonly AppDbContext _dbContext;
    private const string DeviceCookieName = "DeviceId";

    public class DetailPresenceRequest
    {
        public int RestaurantId { get; set; }
        public string? DeviceId { get; set; }
        public string? TabId { get; set; }
    }

    public OnlineController(OnlineUsersService onlineService, AppDbContext dbContext)
    {
        _onlineService = onlineService;
        _dbContext = dbContext;
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetOnlineCount()
    {
        var count = await _onlineService.GetOnlineCountAsync();
        return Ok(new { online = count });
    }

    [HttpGet("web-detail-count")]
    public async Task<IActionResult> GetWebDetailOnlineCount([FromQuery] int? restaurantId = null)
    {
        var count = await _onlineService.GetRestaurantDetailOnlineCountAsync(restaurantId);
        return Ok(new { online = count, restaurantId });
    }

    [HttpPost("heartbeat-detail")]
    public async Task<IActionResult> HeartbeatDetail([FromBody] DetailPresenceRequest request)
    {
        if (request.RestaurantId <= 0)
        {
            return BadRequest(new { success = false });
        }

        var visitorId = Request.Cookies["VisitorId"];
        if (string.IsNullOrWhiteSpace(visitorId))
        {
            return BadRequest(new { success = false });
        }

        var deviceId = Request.Cookies[DeviceCookieName];
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = request.DeviceId?.Trim();
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return BadRequest(new { success = false });
        }

        var tabId = string.IsNullOrWhiteSpace(request.TabId) ? "web" : request.TabId.Trim();

        var role = "Du khách";
        if (User?.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
                role = "Admin";
            else if (User.IsInRole("RestaurantOwner"))
                role = "Nhà hàng";
        }

        var isFromQr = Request.Cookies["FromQrVisitor"] == "1";

        var webConnectionId = $"web:{tabId}";

        await _onlineService.MarkVisitorOnlineDetailAsync(
            visitorId,
            deviceId,
            tabId,
            request.RestaurantId,
            role,
            isFromQr,
            $"/restaurant/{request.RestaurantId}/detail");

        var latestWebEvent = await _dbContext.DeviceConnectionHistories
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId && x.ConnectionId == webConnectionId)
            .OrderByDescending(x => x.EventTimeUtc)
            .Select(x => x.EventType)
            .FirstOrDefaultAsync();

        if (!string.Equals(latestWebEvent, "connect", StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.DeviceConnectionHistories.Add(new DeviceConnectionHistory
            {
                DeviceId = deviceId,
                ConnectionId = webConnectionId,
                EventType = "connect",
                EventTimeUtc = DateTime.UtcNow,
                Note = $"web-detail:{request.RestaurantId}"
            });
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    [HttpPost("leave-detail")]
    public async Task<IActionResult> LeaveWebDetail([FromBody] DetailPresenceRequest request)
    {
        if (request.RestaurantId <= 0)
        {
            return Ok(new { success = true });
        }

        var deviceId = Request.Cookies[DeviceCookieName];
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = request.DeviceId?.Trim();
        }

        var tabId = string.IsNullOrWhiteSpace(request.TabId) ? "web" : request.TabId.Trim();

        var visitorId = Request.Cookies["VisitorId"];
        if (!string.IsNullOrWhiteSpace(visitorId) && !string.IsNullOrWhiteSpace(deviceId))
        {
            var webConnectionId = $"web:{tabId}";
            var latestWebEvent = await _dbContext.DeviceConnectionHistories
                .AsNoTracking()
                .Where(x => x.DeviceId == deviceId && x.ConnectionId == webConnectionId)
                .OrderByDescending(x => x.EventTimeUtc)
                .Select(x => x.EventType)
                .FirstOrDefaultAsync();

            await _onlineService.MarkVisitorLeftDetailAsync(
                visitorId,
                deviceId,
                tabId,
                request.RestaurantId);

            if (string.Equals(latestWebEvent, "connect", StringComparison.OrdinalIgnoreCase))
            {
                _dbContext.DeviceConnectionHistories.Add(new DeviceConnectionHistory
                {
                    DeviceId = deviceId,
                    ConnectionId = webConnectionId,
                    EventType = "disconnect",
                    EventTimeUtc = DateTime.UtcNow,
                    Note = $"web-detail:{request.RestaurantId}"
                });
                await _dbContext.SaveChangesAsync();
            }
        }

        return Ok(new { success = true });
    }
}