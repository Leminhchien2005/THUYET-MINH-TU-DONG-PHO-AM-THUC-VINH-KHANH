using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoodStreetGuide.Services
{
    /// <summary>
    /// Service để listen sự kiện quét QR và phát thuyết minh âm thanh
    /// Kết nối với SignalR Hub ScanHub trên server
    /// </summary>
    public class ScanNarrationHub
    {
        private readonly IConnectivity _connectivity;

        public event EventHandler<ScanEventArgs>? OnScanReceived;
        public event EventHandler<string>? OnNarrationStarted;
        public event EventHandler<string>? OnNarrationEnded;

        public ScanNarrationHub()
        {
            _connectivity = Connectivity.Current;
        }

        /// <summary>
        /// Phát thuyết minh âm thanh cho nhà hàng
        /// </summary>
        public async Task PlayNarration(int restaurantId, string audioUrl, string language = "vi")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    Debug.WriteLine($"No narration URL for restaurant {restaurantId}");
                    return;
                }

                // Check connectivity
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    Debug.WriteLine("No internet connection for playing narration");
                    return;
                }

                Debug.WriteLine($"🎙️ Playing narration for restaurant {restaurantId}: {audioUrl}");
                OnNarrationStarted?.Invoke(this, audioUrl);

                // In MAUI app, you can use MediaElement or WebView to play audio
                // For now, just trigger the event and let the app handle it
                await Task.Delay(100);

                OnNarrationEnded?.Invoke(this, audioUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error playing narration: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle scan event từ server
        /// </summary>
        public void HandleScanEvent(ScanEventData data)
        {
            try
            {
                Debug.WriteLine($"🔔 Scan event received: Restaurant {data.RestaurantId}, Language: {data.Language}");

                OnScanReceived?.Invoke(this, new ScanEventArgs
                {
                    RestaurantId = data.RestaurantId,
                    RestaurantName = data.RestaurantName,
                    ScanTime = data.ScanTime,
                    Language = data.Language,
                    AudioUrl = data.AudioUrl
                });

                // Auto-play narration if audio URL is available
                if (!string.IsNullOrWhiteSpace(data.AudioUrl))
                {
                    _ = PlayNarration(data.RestaurantId, data.AudioUrl, data.Language);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling scan event: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Event data cho scan events
    /// </summary>
    public class ScanEventArgs : EventArgs
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public DateTime ScanTime { get; set; }
        public string Language { get; set; }
        public string AudioUrl { get; set; }
    }

    /// <summary>
    /// Data class cho scan event từ server
    /// </summary>
    public class ScanEventData
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public DateTime ScanTime { get; set; }
        public string Language { get; set; }
        public string AudioUrl { get; set; }
        public string DeviceId { get; set; }
        public string CrowdStatus { get; set; }
    }
}
