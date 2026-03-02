using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using System.Diagnostics;
using System.Text.Json;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly LocationService _locationService = new();

    private List<Poi> _poiList = new();
    private int _currentPoiId = -1;
    private CancellationTokenSource? _speechCts;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 🔥 TỰ ĐỘNG UPDATE NẾU CÓ MẠNG
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            await AutoUpdateAsync();
        }

        await LoadDataAsync();
        LoadMapPins();

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
            return;
        }

        Dispatcher.StartTimer(TimeSpan.FromSeconds(3), () =>
        {
            _ = CheckLocationAsync();
            return true;
        });
    }

    // 🔥 AUTO UPDATE
    private async Task AutoUpdateAsync()
    {
        try
        {
            var apiService = new ApiService();
            var pois = await apiService.GetPoisAsync();

            if (pois == null || pois.Count == 0)
                return;

            string json = JsonSerializer.Serialize(pois);

            string filePath = Path.Combine(FileSystem.AppDataDirectory, "pois.json");

            await File.WriteAllTextAsync(filePath, json);

            Debug.WriteLine("Auto update thành công");
        }
        catch
        {
            Debug.WriteLine("Không update được, dùng dữ liệu cũ");
        }
    }

    // 🔥 LOAD DATA (Ưu tiên file local)
    private async Task LoadDataAsync()
    {
        string filePath = Path.Combine(FileSystem.AppDataDirectory, "pois.json");

        if (File.Exists(filePath))
        {
            string json = await File.ReadAllTextAsync(filePath);

            _poiList = JsonSerializer.Deserialize<List<Poi>>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Poi>();
        }
        else
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("poi.json");
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();

            _poiList = JsonSerializer.Deserialize<List<Poi>>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Poi>();
        }

        PoiList.ItemsSource = _poiList;
    }

    // 🔥 LOAD PIN LÊN MAP
    private void LoadMapPins()
    {
        MyMap.Pins.Clear();

        foreach (var poi in _poiList)
        {
            var pin = new Pin
            {
                Label = poi.Name ?? "",
                Address = poi.Description ?? "",
                Location = new Location(poi.Latitude, poi.Longitude)
            };

            MyMap.Pins.Add(pin);
        }
    }

    // 🔥 CHECK LOCATION
    private async Task CheckLocationAsync()
    {
        var location = await _locationService.GetCurrentLocationAsync();
        if (location == null)
            return;

        LatLabel.Text = $"Latitude: {location.Latitude:F6}";

        foreach (var poi in _poiList)
        {
            poi.DistanceKm = DistanceHelper.CalculateDistanceKm(
                location.Latitude,
                location.Longitude,
                poi.Latitude,
                poi.Longitude);
        }

        _poiList = _poiList
            .OrderBy(p => p.DistanceKm)
            .ToList();

        PoiList.ItemsSource = null;
        PoiList.ItemsSource = _poiList;

        var nearestPoi = _poiList.FirstOrDefault();
        if (nearestPoi == null)
            return;

        double radiusKm = nearestPoi.Radius / 1000.0;

        if (nearestPoi.DistanceKm <= radiusKm)
        {
            if (_currentPoiId != nearestPoi.Id)
            {
                _currentPoiId = nearestPoi.Id;

                _speechCts?.Cancel();
                _speechCts = new CancellationTokenSource();

                if (!string.IsNullOrEmpty(nearestPoi.Description))
                {
                    try
                    {
                        await TextToSpeech.SpeakAsync(
                            $"Bạn đang ở gần {nearestPoi.Name}. {nearestPoi.Description}",
                            cancelToken: _speechCts.Token
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Speech bị ngắt");
                    }
                }
            }
        }
        else
        {
            _currentPoiId = -1;
            _speechCts?.Cancel();
        }
    }

}