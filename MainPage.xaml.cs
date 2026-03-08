using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using System.Diagnostics;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly LocationService _locationService = new();
    private readonly DatabaseService _database = new();

    private List<Poi> _poiList = new();

    private Location? _lastLocation;

    double panelStart;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        BottomPanel.TranslationY = 280;

        await _database.Init();

        // Nếu có internet → cập nhật dữ liệu từ API
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            await AutoUpdateAsync();
        }

        // Sau đó load dữ liệu từ SQLite
        await LoadDataAsync();

        // Hiện pin lên map
        LoadMapPins();

        // Xin quyền vị trí
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
            return;
        }

        var location = await _locationService.GetCurrentLocationAsync();

        if (location != null)
        {
            _lastLocation = location;

            MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    new Location(location.Latitude, location.Longitude),
                    Distance.FromKilometers(1)
                )
            );
        }

        Dispatcher.StartTimer(TimeSpan.FromSeconds(3), () =>
        {
            _ = CheckLocationAsync();
            return true;
        });
    }

    // LẤY DỮ LIỆU API → SQLITE
    private async Task AutoUpdateAsync()
    {
        try
        {
            var apiService = new ApiService();

            var pois = await apiService.GetPoisAsync();

            Debug.WriteLine("========== API TEST ==========");

            if (pois == null)
            {
                Debug.WriteLine("API RETURN NULL");
                await DisplayAlert("API TEST", "API NULL", "OK");
                return;
            }

            Debug.WriteLine("API COUNT: " + pois.Count);

            await DisplayAlertAsync("API TEST", "API Count: " + pois.Count, "OK");

            foreach (var p in pois)
            {
                Debug.WriteLine("API POI: " + p.Name);
            }

            await _database.ReplaceAllDataAsync(pois);

            Debug.WriteLine("========== SQLITE TEST ==========");

            var sqliteList = await _database.GetAllPoiAsync();

            Debug.WriteLine("SQLITE COUNT: " + sqliteList.Count);

            foreach (var p in sqliteList)
            {
                Debug.WriteLine("SQLITE POI: " + p.Name);
            }

            await DisplayAlertAsync("SQLITE TEST", "SQLite Count: " + sqliteList.Count, "OK");


            _poiList = sqliteList;

            PoiList.ItemsSource = _poiList;

            Debug.WriteLine("========== UI TEST ==========");
            Debug.WriteLine("UI COUNT: " + _poiList.Count);

            LoadMapPins();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("ERROR: " + ex.Message);
            await DisplayAlert("ERROR", ex.Message, "OK");
        }
    }

    // LOAD SQLITE → LIST
    private async Task LoadDataAsync()
    {
        _poiList = await _database.GetAllPoiAsync();

        if (_poiList == null)
            _poiList = new List<Poi>();

        if (_poiList.Count == 0)
        {
            var poi = new Poi
            {
                Name = "Bún bò Huế",
                Description = "Quán nổi tiếng",
                Latitude = 10.762622,
                Longitude = 106.660172
            };

            await _database.AddPoiAsync(poi);

            _poiList = await _database.GetAllPoiAsync();
        }

        PoiList.ItemsSource = null;
        PoiList.ItemsSource = _poiList;

        Debug.WriteLine("POI COUNT: " + _poiList.Count);
    }

    // HIỆN PIN TRÊN MAP
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

    // CẬP NHẬT VỊ TRÍ
    async Task CheckLocationAsync()
    {
        var location = await _locationService.GetCurrentLocationAsync();

        if (location == null)
            return;

        if (_lastLocation != null)
        {
            var distance = Location.CalculateDistance(
                _lastLocation,
                location,
                DistanceUnits.Kilometers
            ) * 1000;

            if (distance < 10)
                return;
        }

        _lastLocation = location;

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

        LoadMapPins();
    }

    // CLICK QUÁN → ZOOM MAP
    void PoiList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var poi = e.CurrentSelection.FirstOrDefault() as Poi;

        if (poi == null)
            return;

        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                new Location(poi.Latitude, poi.Longitude),
                Distance.FromKilometers(0.5)
            )
        );
    }

    // SEARCH
    void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        string keyword = e.NewTextValue?.ToLower() ?? "";

        var result = _poiList
            .Where(p => (p.Name ?? "").ToLower().Contains(keyword))
            .ToList();

        PoiList.ItemsSource = null;
        PoiList.ItemsSource = result;
    }

    // DRAG PANEL
    async void OnPanelPan(object sender, PanUpdatedEventArgs e)
    {
        double full = 0;
        double closed = this.Height - 350;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                panelStart = BottomPanel.TranslationY;
                break;

            case GestureStatus.Running:

                double newY = panelStart + e.TotalY;

                if (newY < full)
                    newY = full;

                if (newY > closed)
                    newY = closed;

                BottomPanel.TranslationY = newY;

                break;

            case GestureStatus.Completed:

                if (BottomPanel.TranslationY < closed / 2)
                    await BottomPanel.TranslateToAsync(0, full, 200);
                else
                    await BottomPanel.TranslateToAsync(0, closed, 200);

                break;
        }
    }
}