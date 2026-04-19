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
            var currentUrl = AppendDeviceIdQuery(qrUrl, deviceId);

            for (var i = 0; i < 5; i++)
            {
                var response = await client.GetAsync(currentUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    return null;

                if (IsRedirectStatusCode(response.StatusCode))
                {
                    var location = response.Headers.Location;
                    if (location == null)
                        return null;

                    var locationText = location.ToString();
                    if (TryExtractPoiIdFromRedirectLocation(locationText, out var poiId))
                        return poiId;

                    // Follow intermediate redirect (e.g. http -> https) and try again.
                    currentUrl = location.IsAbsoluteUri
                        ? locationText
                        : new Uri(new Uri(currentUrl), location).ToString();

                    continue;
                }

                return null;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsRedirectStatusCode(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code >= 300 && code < 400;
    }

    private static string AppendDeviceIdQuery(string url, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}deviceId={Uri.EscapeDataString(deviceId)}";
    }

    private static bool TryExtractPoiIdFromRedirectLocation(string location, out int poiId)
    {
        poiId = 0;

        if (string.IsNullOrWhiteSpace(location))
            return false;

        string path = location;
        if (Uri.TryCreate(location, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.AbsolutePath;
        }
        else
        {
            var questionIndex = path.IndexOf('?');
            if (questionIndex >= 0)
            {
                path = path[..questionIndex];
            }
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("restaurant", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(segments[i + 1], out poiId))
            {
                return true;
            }
        }

        return segments.Length > 0 && int.TryParse(segments[^1], out poiId);
    }
}
