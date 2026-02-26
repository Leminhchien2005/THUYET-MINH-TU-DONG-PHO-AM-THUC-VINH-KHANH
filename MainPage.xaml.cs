using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;
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
        _poiList = await _db.GetAllPoiAsync();
        PoiList.ItemsSource = _poiList;

        // Load pin lên map
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

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
            return;
        }

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

        LatLabel.Text = $"Latitude: {location.Latitude:F6}";

        // Tính khoảng cách
        foreach (var poi in _poiList)
        {
            poi.DistanceKm = DistanceHelper.CalculateDistanceKm(
                location.Latitude,
                location.Longitude,
                poi.Latitude,
                poi.Longitude);
        }

        // Sắp xếp gần → xa
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