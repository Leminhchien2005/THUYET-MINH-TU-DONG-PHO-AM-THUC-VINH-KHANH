using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using System.Net.Http;

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
        MyMap.MapClicked += MyMap_MapClicked;
    }

    private void MyMap_MapClicked(object sender, MapClickedEventArgs e)
    {
        MyMap.MapElements.Clear();
        double targetY = this.Height * 0.8;
        BottomPanel.TranslateTo(0, targetY, 200);
    }


    private void ZoomIn_Clicked(object sender, EventArgs e)
    {
        if (MyMap.VisibleRegion != null)
        {
            var center = MyMap.VisibleRegion.Center;
            var radius = MyMap.VisibleRegion.Radius;
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radius.Kilometers * 0.5)));
        }
    }

    private void ZoomOut_Clicked(object sender, EventArgs e)
    {
        if (MyMap.VisibleRegion != null)
        {
            var center = MyMap.VisibleRegion.Center;
            var radius = MyMap.VisibleRegion.Radius;
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radius.Kilometers * 2.0)));
        }
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
        PoiList.IsVisible = false;
        DetailPanel.IsVisible = true;

        DetailName.Text = poi.Name;
        DetailDescription.Text = poi.Description;
        DetailDistance.Text = $"Khoảng cách {poi.DistanceKm:0.00} km";

        if (!string.IsNullOrEmpty(poi.ImageUrl))
            DetailImage.Source = poi.ImageUrl;

        // mở panel
        BottomPanel.TranslateTo(0, 0, 200);
    }

    void ClearSelection_Click(object sender, EventArgs e)
    {
        _selectedPoi = null;
        PoiList.SelectedItem = null;
        DetailPanel.IsVisible = false;
        PoiList.IsVisible = true;
        MyMap.MapElements.Clear();
    }

    async Task<List<Location>> GetRouteAsync(Location start, Location end)
    {
        try
        {
            var url =
            $"https://router.project-osrm.org/route/v1/driving/{start.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{start.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)};{end.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{end.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}?overview=full&geometries=geojson";

            var http = new HttpClient();

            var json = await http.GetStringAsync(url);

            var doc = JsonDocument.Parse(json);

            var routes = doc.RootElement.GetProperty("routes");

            if (routes.GetArrayLength() == 0)
                return new List<Location>();

            var coordinates =
            routes[0].GetProperty("geometry").GetProperty("coordinates");

            var points = new List<Location>();

            foreach (var c in coordinates.EnumerateArray())
            {
                points.Add(new Location(
                    c[1].GetDouble(),
                    c[0].GetDouble()));
            }

            return points;
        }
        catch
        {
            return new List<Location>();
        }
    }
    async void RouteButton_Click(object sender, EventArgs e)
    {
        if (_selectedPoi == null || _lastLocation == null)
            return;

        try
        {
            MyMap.MapElements.Clear();

            var start = _lastLocation;
            var end = new Location(_selectedPoi.Latitude, _selectedPoi.Longitude);

            var points = await GetRouteAsync(start, end);

            if (points == null || points.Count == 0)
            {
                await DisplayAlert("Lỗi", "Không lấy được đường đi", "OK");
                return;
            }

            var polyline = new Polyline
            {
                StrokeColor = Colors.Blue,
                StrokeWidth = 6
            };

            foreach (var p in points)
                polyline.Geopath.Add(p);

            MyMap.MapElements.Add(polyline);

            if (points.Count > 0)
            {
                var minLat = points.Min(p => p.Latitude);
                var maxLat = points.Max(p => p.Latitude);
                var minLon = points.Min(p => p.Longitude);
                var maxLon = points.Max(p => p.Longitude);

                var centerLat = (minLat + maxLat) / 2;
                var centerLon = (minLon + maxLon) / 2;

                var latDegrees = (maxLat - minLat) * 1.5;
                var lonDegrees = (maxLon - minLon) * 1.5;

                if (latDegrees == 0) latDegrees = 0.01;
                if (lonDegrees == 0) lonDegrees = 0.01;

                MyMap.MoveToRegion(new MapSpan(new Location(centerLat, centerLon), latDegrees, lonDegrees));
            }

            double targetY = this.Height * 0.8;
            await BottomPanel.TranslateTo(0, targetY, 200);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    void FavoriteButton_Click(object sender, EventArgs e)
    {
        if (_selectedPoi == null)
            return;

        // Xử lý lưu yêu thích ở đây
        DisplayAlert("Thông báo", $"Đã lưu '{_selectedPoi.Name}' vào danh sách yêu thích.", "OK");
    }

    void CloseDetail_Click(object sender, EventArgs e)
    {
        DetailFullPanel.IsVisible = false;
        TitleLabel.IsVisible = true;
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
                BottomPanel.CancelAnimations();
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
            case GestureStatus.Canceled:

                if (BottomPanel.TranslationY < closed / 2)
                    await BottomPanel.TranslateTo(0, full, 250, Easing.CubicOut);
                else
                    await BottomPanel.TranslateTo(0, closed, 250, Easing.CubicOut);

                break;
        }
    }

}