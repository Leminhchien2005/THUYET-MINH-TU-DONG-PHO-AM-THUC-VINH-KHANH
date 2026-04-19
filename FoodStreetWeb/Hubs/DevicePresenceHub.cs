using FoodStreetWeb.Data;
using FoodStreetWeb.Models;
using FoodStreetWeb.Services;
using Microsoft.AspNetCore.SignalR;

namespace FoodStreetWeb.Hubs
{
    public class DevicePresenceHub : Hub
    {
        private readonly OnlineDeviceStore _onlineDeviceStore;
        private readonly IServiceScopeFactory _scopeFactory;

        public DevicePresenceHub(OnlineDeviceStore onlineDeviceStore, IServiceScopeFactory scopeFactory)
        {
            _onlineDeviceStore = onlineDeviceStore;
            _scopeFactory = scopeFactory;
        }

        public override async Task OnConnectedAsync()
        {
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var normalizedDeviceId = deviceId.Trim();
                _onlineDeviceStore.Register(Context.ConnectionId, normalizedDeviceId);
                await LogDeviceEventAsync(normalizedDeviceId, Context.ConnectionId, "connect");
            }

            await base.OnConnectedAsync();
        }

        public async Task RegisterDevice(string deviceId)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var normalizedDeviceId = deviceId.Trim();
                var previousDeviceId = _onlineDeviceStore.GetDeviceIdByConnection(Context.ConnectionId);

                _onlineDeviceStore.Register(Context.ConnectionId, normalizedDeviceId);

                if (!string.Equals(previousDeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    await LogDeviceEventAsync(normalizedDeviceId, Context.ConnectionId, "connect");
                }
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var deviceId = _onlineDeviceStore.GetDeviceIdByConnection(Context.ConnectionId);
            _onlineDeviceStore.RemoveConnection(Context.ConnectionId);

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                await LogDeviceEventAsync(deviceId, Context.ConnectionId, "disconnect");
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task LogDeviceEventAsync(string deviceId, string connectionId, string eventType)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                dbContext.DeviceConnectionHistories.Add(new DeviceConnectionHistory
                {
                    DeviceId = deviceId,
                    ConnectionId = connectionId,
                    EventType = eventType,
                    EventTimeUtc = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();
            }
            catch
            {
                // Không làm ảnh hưởng flow online/disconnect hiện tại.
            }
        }
    }
}
