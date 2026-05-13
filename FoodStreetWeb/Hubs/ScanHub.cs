using Microsoft.AspNetCore.SignalR;

namespace FoodStreetWeb.Hubs
{
    public class ScanHub : Hub
    {
        // Subscribe to receive real-time scan and narration events
        public async Task Subscribe(string restaurantId = "")
        {
            var groupName = string.IsNullOrEmpty(restaurantId) ? "all-scans" : $"restaurant-{restaurantId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        // Unsubscribe from receiving events
        public async Task Unsubscribe(string restaurantId = "")
        {
            var groupName = string.IsNullOrEmpty(restaurantId) ? "all-scans" : $"restaurant-{restaurantId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        // Called when client connects
        public override async Task OnConnectedAsync()
        {
            // Subscribe to all-scans by default
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-scans");
            await base.OnConnectedAsync();
        }
    }
}
