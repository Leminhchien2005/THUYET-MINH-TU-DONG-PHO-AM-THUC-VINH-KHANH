using System.Collections.Concurrent;

namespace FoodStreetWeb.Services
{
    public class OnlineDeviceStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _deviceConnections = new();
        private readonly ConcurrentDictionary<string, string> _connectionToDevice = new();
        private readonly ConcurrentDictionary<string, DateTime> _connectedAt = new();
        private readonly ConcurrentDictionary<string, DeviceZoneState> _deviceZones = new();

        public void Register(string connectionId, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(deviceId))
                return;

            if (_connectionToDevice.TryGetValue(connectionId, out var oldDeviceId))
            {
                if (string.Equals(oldDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                    return;

                RemoveConnection(connectionId);
            }

            var connections = _deviceConnections.GetOrAdd(deviceId, _ => new ConcurrentDictionary<string, byte>());
            connections[connectionId] = 0;
            _connectionToDevice[connectionId] = deviceId;

            _connectedAt.TryAdd(deviceId, DateTime.UtcNow);
        }

        public void TouchHeartbeat(string deviceId)
        {
            // Giữ lại method để tương thích API heartbeat cũ.
            if (string.IsNullOrWhiteSpace(deviceId)) return;
            _connectedAt.TryAdd(deviceId, DateTime.UtcNow);
        }

        public string? GetDeviceIdByConnection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                return null;

            return _connectionToDevice.TryGetValue(connectionId, out var deviceId)
                ? deviceId
                : null;
        }

        public void RemoveConnection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                return;

            if (!_connectionToDevice.TryRemove(connectionId, out var deviceId))
                return;

            if (_deviceConnections.TryGetValue(deviceId, out var connections))
            {
                connections.TryRemove(connectionId, out _);

                if (connections.IsEmpty)
                {
                    _deviceConnections.TryRemove(deviceId, out _);
                    _connectedAt.TryRemove(deviceId, out _);
                    _deviceZones.TryRemove(deviceId, out _);
                }
            }
        }

        public void UpdateDeviceZone(string deviceId, int restaurantId)
        {
            UpdateDeviceZone(deviceId, new[] { restaurantId });
        }

        public void UpdateDeviceZone(string deviceId, IEnumerable<int> restaurantIds)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || restaurantIds == null)
                return;

            _deviceZones[deviceId] = new DeviceZoneState
            {
                RestaurantIds = restaurantIds.Where(x => x > 0).Distinct().ToHashSet(),
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        public IReadOnlyDictionary<string, DeviceZoneState> GetDeviceZones()
        {
            return _deviceZones.ToDictionary(x => x.Key, x => x.Value);
        }

        public IReadOnlyList<OnlineDeviceInfo> GetOnlineDevices()
        {
            return _deviceConnections
                .Select(x => new OnlineDeviceInfo
                {
                    DeviceId = x.Key,
                    ConnectionCount = x.Value.Count,
                    ConnectedAtUtc = _connectedAt.TryGetValue(x.Key, out var connectedAt)
                        ? connectedAt
                        : DateTime.UtcNow
                })
                .OrderBy(x => x.DeviceId)
                .ToList();
        }

        public bool IsOnline(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return false;

            return _deviceConnections.TryGetValue(deviceId, out var connections) && !connections.IsEmpty;
        }

        public class OnlineDeviceInfo
        {
            public string DeviceId { get; set; } = string.Empty;
            public DateTime ConnectedAtUtc { get; set; }
            public int ConnectionCount { get; set; }
        }

        public class DeviceZoneState
        {
            public HashSet<int> RestaurantIds { get; set; } = new();
            public DateTime UpdatedAtUtc { get; set; }
        }
    }
}
