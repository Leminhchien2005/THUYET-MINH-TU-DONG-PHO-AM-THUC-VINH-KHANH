using System.Collections.Concurrent;
using FoodStreetWeb.Models;

namespace FoodStreetWeb.Services
{
    public class OnlineUsersService
    {
        private readonly ConcurrentDictionary<string, OnlineVisitorItemViewModel> _users = new();
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        public void UpdateUser(string visitorId, string role, bool isFromQr, string path)
        {
            var now = DateTime.UtcNow;

            _users.AddOrUpdate(
                visitorId,
                _ => new OnlineVisitorItemViewModel
                {
                    VisitorId = visitorId,
                    Role = role,
                    IsFromQr = isFromQr,
                    LastPath = path,
                    LastSeenUtc = now
                },
                (_, current) => new OnlineVisitorItemViewModel
                {
                    VisitorId = current.VisitorId,
                    Role = role,
                    IsFromQr = isFromQr || current.IsFromQr,
                    LastPath = path,
                    LastSeenUtc = now
                });
        }

        public int GetOnlineCount()
        {
            CleanupExpiredUsers();
            return _users.Count;
        }

        public OnlineUsersStatsViewModel GetOnlineStats()
        {
            CleanupExpiredUsers();

            var snapshot = _users.Values.ToList();

            return new OnlineUsersStatsViewModel
            {
                TotalOnline = snapshot.Count,
                QrOnline = snapshot.Count(x => x.IsFromQr),
                GuestOnline = snapshot.Count(x => x.Role == "Du khách"),
                AdminOnline = snapshot.Count(x => x.Role == "Admin"),
                RestaurantOnline = snapshot.Count(x => x.Role == "Nhà hàng"),
                Visitors = snapshot
                    .OrderByDescending(x => x.LastSeenUtc)
                    .ToList()
            };
        }

        private void CleanupExpiredUsers()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _users
                .Where(u => now - u.Value.LastSeenUtc > _timeout)
                .Select(u => u.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _users.TryRemove(key, out _);
            }
        }
    }
}