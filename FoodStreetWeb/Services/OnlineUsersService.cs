using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Services
{
    public class OnlineUsersService
    {
        private readonly AppDbContext _dbContext;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

        public OnlineUsersService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static bool ShouldIgnorePresencePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;

            return path.StartsWith("/api/online", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/hubs/device-presence", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPresenceId(string visitorId, string deviceId, int restaurantId)
            => $"{visitorId}:{deviceId}:{restaurantId}";

        public async Task UpdateUserAsync(string visitorId, string deviceId, string tabId, string role, bool isFromQr, string? path)
        {
            if (string.IsNullOrWhiteSpace(visitorId)
                || string.IsNullOrWhiteSpace(deviceId)
                || ShouldIgnorePresencePath(path))
                return;

            var restaurantId = TryGetRestaurantIdFromPath(path);
            if (restaurantId is null)
                return;

            await MarkVisitorOnlineDetailAsync(visitorId, deviceId, tabId, restaurantId.Value, role, isFromQr, path);
        }

        public async Task MarkVisitorOnlineDetailAsync(
            string visitorId,
            string deviceId,
            string tabId,
            int restaurantId,
            string role,
            bool isFromQr,
            string? path = null)
        {
            if (string.IsNullOrWhiteSpace(visitorId) || string.IsNullOrWhiteSpace(deviceId))
                return;

            var normalizedTabId = string.IsNullOrWhiteSpace(tabId) ? "web" : tabId;

            var now = DateTime.UtcNow;
            await CleanupExpiredUsersAsync(now, saveChanges: false);

            var presenceId = BuildPresenceId(visitorId, deviceId, restaurantId);
            var current = await _dbContext.OnlineWebPresences.FirstOrDefaultAsync(x => x.PresenceId == presenceId);

            if (current == null)
            {
                _dbContext.OnlineWebPresences.Add(new OnlineWebPresence
                {
                    PresenceId = presenceId,
                    VisitorId = visitorId,
                    DeviceId = deviceId,
                    TabId = normalizedTabId,
                    RestaurantId = restaurantId,
                    Role = role,
                    IsFromQr = isFromQr,
                    LastPath = string.IsNullOrWhiteSpace(path) ? $"/restaurant/{restaurantId}/detail" : path,
                    LastSeenUtc = now
                });
            }
            else
            {
                current.Role = role;
                current.IsFromQr = current.IsFromQr || isFromQr;
                current.TabId = normalizedTabId;
                if (!string.IsNullOrWhiteSpace(path) && !ShouldIgnorePresencePath(path))
                {
                    current.LastPath = path;
                }
                current.LastSeenUtc = now;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<int> GetOnlineCountAsync()
        {
            await CleanupExpiredUsersAsync();

            return await _dbContext.OnlineWebPresences
                .AsNoTracking()
                .Select(x => x.DeviceId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetRestaurantDetailOnlineCountAsync(int? restaurantId = null)
        {
            await CleanupExpiredUsersAsync();

            var query = _dbContext.OnlineWebPresences
                .AsNoTracking()
                .Where(x => IsRestaurantDetailPath(x.LastPath, restaurantId));

            return await query
                .Select(x => x.DeviceId)
                .Distinct()
                .CountAsync();
        }

        public async Task MarkVisitorLeftDetailAsync(string visitorId, string deviceId, string tabId, int? restaurantId = null)
        {
            if (string.IsNullOrWhiteSpace(visitorId))
                return;

            await CleanupExpiredUsersAsync(saveChanges: false);

            var query = _dbContext.OnlineWebPresences
                .Where(x => x.VisitorId == visitorId);

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                query = query.Where(x => x.DeviceId == deviceId);
            }

            if (restaurantId.HasValue)
            {
                query = query.Where(x => x.RestaurantId == restaurantId.Value);
            }

            var items = await query.ToListAsync();
            if (items.Count == 0)
                return;

            _dbContext.OnlineWebPresences.RemoveRange(items);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<OnlineUsersStatsViewModel> GetOnlineStatsAsync()
        {
            await CleanupExpiredUsersAsync();

            var snapshot = await _dbContext.OnlineWebPresences
                .AsNoTracking()
                .OrderByDescending(x => x.LastSeenUtc)
                .ToListAsync();

            return new OnlineUsersStatsViewModel
            {
                TotalOnline = snapshot.Select(x => x.DeviceId).Distinct().Count(),
                QrOnline = snapshot.Where(x => x.IsFromQr).Select(x => x.DeviceId).Distinct().Count(),
                GuestOnline = snapshot.Where(x => x.Role == "Du khách").Select(x => x.DeviceId).Distinct().Count(),
                AdminOnline = snapshot.Where(x => x.Role == "Admin").Select(x => x.DeviceId).Distinct().Count(),
                RestaurantOnline = snapshot.Where(x => x.Role == "Nhà hàng").Select(x => x.DeviceId).Distinct().Count(),
                Visitors = snapshot
                    .GroupBy(x => x.DeviceId)
                    .Select(g => g.OrderByDescending(x => x.LastSeenUtc).First())
                    .Select(x => new OnlineVisitorItemViewModel
                    {
                        VisitorId = x.VisitorId,
                        Role = x.Role,
                        IsFromQr = x.IsFromQr,
                        LastPath = x.LastPath,
                        LastSeenUtc = x.LastSeenUtc
                    })
                    .OrderByDescending(x => x.LastSeenUtc)
                    .ToList()
            };
        }

        private async Task CleanupExpiredUsersAsync(DateTime? nowUtc = null, bool saveChanges = true)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            var cutoff = now - _timeout;

            var expired = await _dbContext.OnlineWebPresences
                .Where(x => x.LastSeenUtc < cutoff)
                .ToListAsync();

            if (expired.Count == 0)
                return;

            var deviceIds = expired
                .Select(x => x.DeviceId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var connectionIds = expired
                .Select(x => $"web:{(string.IsNullOrWhiteSpace(x.TabId) ? "web" : x.TabId)}")
                .Distinct()
                .ToList();

            var latestEventByKey = await _dbContext.DeviceConnectionHistories
                .AsNoTracking()
                .Where(x => deviceIds.Contains(x.DeviceId) && connectionIds.Contains(x.ConnectionId))
                .GroupBy(x => new { x.DeviceId, x.ConnectionId })
                .Select(g => new
                {
                    g.Key.DeviceId,
                    g.Key.ConnectionId,
                    EventType = g.OrderByDescending(x => x.EventTimeUtc)
                                 .Select(x => x.EventType)
                                 .FirstOrDefault()
                })
                .ToDictionaryAsync(
                    x => $"{x.DeviceId}|{x.ConnectionId}",
                    x => x.EventType ?? string.Empty);

            var timeoutDisconnectEvents = new List<DeviceConnectionHistory>();

            foreach (var item in expired)
            {
                var normalizedTabId = string.IsNullOrWhiteSpace(item.TabId) ? "web" : item.TabId;
                var connectionId = $"web:{normalizedTabId}";
                var key = $"{item.DeviceId}|{connectionId}";

                latestEventByKey.TryGetValue(key, out var latestEventType);

                if (!string.Equals(latestEventType, "disconnect", StringComparison.OrdinalIgnoreCase))
                {
                    timeoutDisconnectEvents.Add(new DeviceConnectionHistory
                    {
                        DeviceId = item.DeviceId,
                        ConnectionId = connectionId,
                        EventType = "disconnect",
                        EventTimeUtc = now,
                        Note = $"timeout:web-detail:{item.RestaurantId}"
                    });
                }
            }

            if (timeoutDisconnectEvents.Count > 0)
            {
                _dbContext.DeviceConnectionHistories.AddRange(timeoutDisconnectEvents);
            }

            _dbContext.OnlineWebPresences.RemoveRange(expired);
            if (saveChanges)
            {
                await _dbContext.SaveChangesAsync();
            }
        }

        private static int? TryGetRestaurantIdFromPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var cleanedPath = path.Split('?', '#')[0].Trim();
            var segments = cleanedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3) return null;
            if (!segments[0].Equals("restaurant", StringComparison.OrdinalIgnoreCase)) return null;
            if (!segments[2].Equals("detail", StringComparison.OrdinalIgnoreCase)) return null;
            if (!int.TryParse(segments[1], out var id)) return null;
            return id;
        }

        private static bool IsRestaurantDetailPath(string? path, int? restaurantId)
        {
            var parsedId = TryGetRestaurantIdFromPath(path);
            if (!parsedId.HasValue) return false;
            return !restaurantId.HasValue || restaurantId.Value == parsedId.Value;
        }
    }
}