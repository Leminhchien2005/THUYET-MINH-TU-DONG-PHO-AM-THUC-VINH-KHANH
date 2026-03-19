using FoodStreetGuide.Models;
using System.Text.Json;

namespace FoodStreetGuide.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient = new HttpClient
        {
            //BaseAddress = new Uri("http://10.0.2.2:5057"),// may ao
            BaseAddress = new Uri("http://192.168.31.164:5057"),// may that  
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