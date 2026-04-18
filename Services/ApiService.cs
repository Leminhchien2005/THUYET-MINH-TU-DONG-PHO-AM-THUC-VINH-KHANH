using FoodStreetGuide.Models;
using System.Text.Json;

namespace FoodStreetGuide.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://foodstreetweb-sfecqdx26a-as.a.run.app"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<int?> GetFeaturedRestaurantIdAsync(int days = 7)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/ScanAnalytics/crowded-restaurants?days={days}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var ids = JsonSerializer.Deserialize<List<int>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<int>();

            return ids.Count > 0 ? ids[0] : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<HashSet<int>> GetCrowdedRestaurantIdsAsync(int days = 7)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/ScanAnalytics/crowded-restaurants?days={days}");
            if (!response.IsSuccessStatusCode)
            {
                return new HashSet<int>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var ids = JsonSerializer.Deserialize<List<int>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<int>();

            return ids.ToHashSet();
        }
        catch
        {
            return new HashSet<int>();
        }
    }

    public async Task<List<Poi>> GetPoisAsync()
    {
        var response = await _httpClient.GetAsync("api/PoisApi");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<Poi>>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<Poi>();
    }

    public async Task<List<ApiPoiDto>> GetPoisWithFoodsAsync()
    {
        var response = await _httpClient.GetAsync("api/PoisApi");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<ApiPoiDto>>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<ApiPoiDto>();
    }

    public async Task<Poi> GetPoiById(string id)
    {
        var response = await _httpClient.GetAsync($"api/PoisApi/{id}");

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<Poi>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<int?> RedeemQrAsync(string qrUrl)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(handler);

            var deviceId = DeviceIdService.GetOrCreateDeviceId();
            var requestUrl = AppendDeviceIdQuery(qrUrl, deviceId);

            var response = await client.GetAsync(requestUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return null;

            if ((int)response.StatusCode == 302)
            {
                var location = response.Headers.Location?.ToString();

                if (!string.IsNullOrEmpty(location))
                {
                    var parts = location.Split('/');
                    var idText = parts.Last();

                    if (int.TryParse(idText, out int poiId))
                        return poiId;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string AppendDeviceIdQuery(string url, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}deviceId={Uri.EscapeDataString(deviceId)}";
    }
}
