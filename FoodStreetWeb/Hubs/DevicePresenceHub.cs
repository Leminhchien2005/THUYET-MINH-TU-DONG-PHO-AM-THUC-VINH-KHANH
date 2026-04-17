using FoodStreetWeb.Services;
using Microsoft.AspNetCore.SignalR;

namespace FoodStreetWeb.Hubs
{
    public class DevicePresenceHub : Hub
    {
        private readonly OnlineDeviceStore _onlineDeviceStore;

        public DevicePresenceHub(OnlineDeviceStore onlineDeviceStore)
        {
            _onlineDeviceStore = onlineDeviceStore;
        }

        public override Task OnConnectedAsync()
        {
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                _onlineDeviceStore.Register(Context.ConnectionId, deviceId.Trim());
            }

            return base.OnConnectedAsync();
        }

        public Task RegisterDevice(string deviceId)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                _onlineDeviceStore.Register(Context.ConnectionId, deviceId.Trim());
            }

            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _onlineDeviceStore.RemoveConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
