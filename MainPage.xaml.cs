using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using System.Diagnostics;
using System.Linq;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly LocationService _locationService = new();

    private List<Poi> _poiList = new();

    private int _currentPoiId = -1;
    private CancellationTokenSource? _speechCts;

    public MainPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _db.SeedDataAsync();

        // 🔥 Load dữ liệu 1 lần duy nhất
        _poiList = await _db.GetAllPoiAsync();
        PoiList.ItemsSource = _poiList;

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
            return;
        }

        // 🔥 Cứ 3 giây kiểm tra vị trí
        Dispatcher.StartTimer(TimeSpan.FromSeconds(3), () =>
        {
            _ = CheckLocationAsync();
            return true;
        });
    }

    private async Task CheckLocationAsync()
    {
        var location = await _locationService.GetCurrentLocationAsync();
        if (location == null)
            return;

        LatLabel.Text = $"Latitude: {location.Latitude}";
        LngLabel.Text = $"Longitude: {location.Longitude}";

        // 🔥 Tính khoảng cách cho từng POI
        foreach (var poi in _poiList)
        {
            poi.DistanceKm = DistanceHelper.CalculateDistanceKm(
                location.Latitude,
                location.Longitude,
                poi.Latitude,
                poi.Longitude);

            Debug.WriteLine($"{poi.Name} - {poi.DistanceKm} km");
        }

        // 🔥 Sắp xếp gần → xa
        _poiList = _poiList
            .OrderBy(p => p.DistanceKm)
            .ToList();

        // 🔥 Refresh lại UI
        PoiList.ItemsSource = null;
        PoiList.ItemsSource = _poiList;

        // 🔥 Lấy quán gần nhất
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
                        Debug.WriteLine("Speech bị ngắt do chuyển vị trí");
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