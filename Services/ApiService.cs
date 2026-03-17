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
            // Dùng cho máy ảo (Android Emulator)
             BaseAddress = new Uri("http://10.0.2.2:5057"),

            // Dùng cho máy thật (đổi IP đúng máy)
            //BaseAddress = new Uri("http://192.168.1.9:5057/"),

            Timeout = TimeSpan.FromSeconds(3)
        };
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
}