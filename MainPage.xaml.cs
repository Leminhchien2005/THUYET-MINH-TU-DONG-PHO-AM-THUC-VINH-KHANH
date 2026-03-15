using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using System.Diagnostics;
using System.Xml;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly LocationService _locationService = new();
    private readonly DatabaseService _database = new();

    private List<Poi> _poiList = new();

    private Location? _lastLocation;

    double panelStart;

    Poi? _selectedPoi;

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
            _ = Task.Run(async () =>
            {
                await AutoUpdateAsync();
            });
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

        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
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

            if (pois == null || pois.Count == 0)
            {
                Debug.WriteLine("API EMPTY");
                return;
            }

            Debug.WriteLine("API COUNT: " + pois.Count);

            await _database.ReplaceAllDataAsync(pois);

            var sqliteList = await _database.GetAllPoiAsync();

            Debug.WriteLine("SQLITE COUNT: " + sqliteList.Count);

            _poiList = sqliteList;

            PoiList.ItemsSource = _poiList;

            LoadMapPins();
        }
        catch (HttpRequestException)
        {
            Debug.WriteLine("API SERVER KHÔNG CHẠY");
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("API TIMEOUT");
        }
        catch (Exception ex)
        {
            Debug.WriteLine("ERROR: " + ex.Message);
        }
    }

    // LOAD SQLITE → LIST
    private async Task LoadDataAsync()
    {
        _poiList = await _database.GetAllPoiAsync();

        if (_poiList == null)
            _poiList = new List<Poi>();

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
            var moveDistance = Location.CalculateDistance(
                _lastLocation,
                location,
                DistanceUnits.Kilometers
            ) * 1000;

            // chỉ cập nhật khi di chuyển > 2m
            if (moveDistance < 2)
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

        PoiList.ItemsSource = _poiList;
    }

    // CLICK QUÁN → ZOOM MAP
    void PoiList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var poi = e.CurrentSelection.FirstOrDefault() as Poi;

        if (poi == null)
            return;

        _selectedPoi = poi;

        var location = new Location(poi.Latitude, poi.Longitude);

        // zoom tới quán
        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                location,
                Distance.FromKilometers(0.3)
            )
        );

        // hiện detail
        DetailPanel.IsVisible = true;

        DetailName.Text = poi.Name;
        DetailDescription.Text = poi.Description;
        DetailDistance.Text = $"Khoảng cách {poi.DistanceKm:0.00} km";

        if (!string.IsNullOrEmpty(poi.ImageUrl))
            DetailImage.Source = poi.ImageUrl;

        // mở panel
        BottomPanel.TranslateTo(0, 0, 200);
    }

    async void RouteButton_Click(object sender, EventArgs e)
    {
        if (_selectedPoi == null)
            return;

        var url =
            $"https://www.google.com/maps/dir/?api=1&destination={_selectedPoi.Latitude},{_selectedPoi.Longitude}";

        await Launcher.Default.OpenAsync(url);
    }

    void DetailButton_Click(object sender, EventArgs e)
    {
        if (_selectedPoi == null)
            return;

        var poi = _selectedPoi;

        FullName.Text = poi.Name;
        FullDescription.Text = poi.Description;
        FullDistance.Text = $"Khoảng cách {poi.DistanceKm:0.00} km";

        if (!string.IsNullOrEmpty(poi.ImageUrl))
            FullImage.Source = poi.ImageUrl;

        DetailFullPanel.IsVisible = true;
    }

    async void CloseDetail_Click(object sender, EventArgs e)
    {
        DetailFullPanel.IsVisible = false;

        await BottomPanel.TranslateTo(0, 280, 200);
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