using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace FoodStreetGuide.Services
{
    /// <summary>
    /// SignalR client connection để nghe sự kiện scan & thuyết minh từ server
    /// Hoạt động cho cả app và web
    /// </summary>
    public class ScanHubClient
    {
        private HubConnection? _hubConnection;
        private readonly string _hubUrl;
        private readonly ScanNarrationHub _narrationHub;
        private int? _currentRestaurantFilter = null;

        public event EventHandler<ScanEventData>? OnScanReceived;
        public event EventHandler<string>? OnConnectionStatusChanged;

        public ScanHubClient(string hubUrl, ScanNarrationHub narrationHub)
        {
            _hubUrl = hubUrl;
            _narrationHub = narrationHub;
        }

        /// <summary>
        /// Kết nối với SignalR Hub
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    Debug.WriteLine("Already connected to ScanHub");
                    return;
                }

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl)
                    .WithAutomaticReconnect(new[] { 
                        TimeSpan.Zero,
                        TimeSpan.Zero,
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(3),
                        TimeSpan.FromSeconds(5)
                    })
                    .Build();

                // Listen for scan events
                _hubConnection.On<ScanEventData>("OnScanReceived", (data) =>
                {
                    Debug.WriteLine($"🎙️ ScanHub received event: {data.RestaurantId}");
                    OnScanReceived?.Invoke(this, data);
                    _narrationHub.HandleScanEvent(data);
                });

                _hubConnection.Reconnecting += (error) =>
                {
                    Debug.WriteLine($"🔄 ScanHub reconnecting: {error?.Message}");
                    OnConnectionStatusChanged?.Invoke(this, "Reconnecting");
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += (connectionId) =>
                {
                    Debug.WriteLine($"✅ ScanHub reconnected: {connectionId}");
                    OnConnectionStatusChanged?.Invoke(this, "Connected");
                    // Re-subscribe after reconnect
                    _ = SubscribeToScansAsync(_currentRestaurantFilter);
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += (error) =>
                {
                    Debug.WriteLine($"❌ ScanHub closed: {error?.Message}");
                    OnConnectionStatusChanged?.Invoke(this, "Disconnected");
                    return Task.CompletedTask;
                };

                await _hubConnection.StartAsync();
                Debug.WriteLine("🔌 Connected to ScanHub");
                OnConnectionStatusChanged?.Invoke(this, "Connected");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to connect to ScanHub: {ex.Message}");
                OnConnectionStatusChanged?.Invoke(this, $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ngắt kết nối với SignalR Hub
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_hubConnection != null)
                {
                    await _hubConnection.StopAsync();
                    await _hubConnection.DisposeAsync();
                    _hubConnection = null;
                    Debug.WriteLine("Disconnected from ScanHub");
                    OnConnectionStatusChanged?.Invoke(this, "Disconnected");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disconnecting from ScanHub: {ex.Message}");
            }
        }

        /// <summary>
        /// Subscribe để nhận sự kiện scan từ nhà hàng cụ thể hoặc tất cả
        /// </summary>
        public async Task SubscribeToScansAsync(int? restaurantId = null)
        {
            try
            {
                if (_hubConnection?.State != HubConnectionState.Connected)
                {
                    Debug.WriteLine("Not connected to ScanHub");
                    return;
                }

                _currentRestaurantFilter = restaurantId;
                var restaurantIdStr = restaurantId?.ToString() ?? "";

                await _hubConnection.InvokeAsync("Subscribe", restaurantIdStr);
                Debug.WriteLine($"✅ Subscribed to scans for restaurant: {(restaurantId == null ? "All" : restaurantId.ToString())}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error subscribing to scans: {ex.Message}");
            }
        }

        /// <summary>
        /// Unsubscribe từ sự kiện scan
        /// </summary>
        public async Task UnsubscribeFromScansAsync(int? restaurantId = null)
        {
            try
            {
                if (_hubConnection?.State != HubConnectionState.Connected)
                {
                    Debug.WriteLine("Not connected to ScanHub");
                    return;
                }

                var restaurantIdStr = restaurantId?.ToString() ?? "";
                await _hubConnection.InvokeAsync("Unsubscribe", restaurantIdStr);
                Debug.WriteLine($"Unsubscribed from scans for restaurant: {(restaurantId == null ? "All" : restaurantId.ToString())}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unsubscribing from scans: {ex.Message}");
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái kết nối
        /// </summary>
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public HubConnectionState? ConnectionState => _hubConnection?.State;
    }
}
