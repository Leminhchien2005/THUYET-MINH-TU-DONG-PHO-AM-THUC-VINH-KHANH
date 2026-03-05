using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using System.Diagnostics;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly LocationService _locationService = new();
    private readonly DatabaseService _database = new();

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

        // 🔥 Khởi tạo SQLite
        await _database.Init();

        // 🔥 Nếu có mạng thì update từ API
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            await AutoUpdateAsync();
        }

        // 🔥 Load dữ liệu từ SQLite
        await LoadDataAsync();

        // 🔥 Hiển thị pin map
        LoadMapPins();

        // 🔥 Xin quyền GPS
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
            return;
        }

        // 🔥 Check vị trí mỗi 3 giây
        Dispatcher.StartTimer(TimeSpan.FromSeconds(3), () =>
        {
            _ = CheckLocationAsync();
            return true;
        });
    }

    // 🔥 Update dữ liệu từ Web API
    private async Task AutoUpdateAsync()
    {
        try
        {
            var apiService = new ApiService();

            var pois = await apiService.GetPoisAsync();

            if (pois == null || pois.Count == 0)
                return;

            await _database.ReplaceAllDataAsync(pois);

            Debug.WriteLine("Update dữ liệu từ API thành công");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi update API: {ex.Message}");
        }
    }

    // 🔥 Load dữ liệu từ SQLite
    private async Task LoadDataAsync()
    {
        _poiList = await _database.GetAllPoiAsync();

        PoiList.ItemsSource = _poiList;
    }

    // 🔥 Load pin lên map
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

    // 🔥 Kiểm tra khoảng cách
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