using FoodStreetGuide.Models;
using FoodStreetGuide.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.Threading;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly LocationService _locationService = new();
    private readonly DatabaseService _database = new();

    private List<Poi> _poiList = new();

    private Location? _lastLocation;

    double panelStart;

    Poi? _selectedPoi;
    CancellationTokenSource? _speechCts;

    // 🔥 NEW: AUTO SPEAK
    private HashSet<int> _spokenPoiIds = new();
    const double TRIGGER_DISTANCE_KM = 0.1; // 100m

    bool _suppressSearchUpdate;

    public MainPage()
    {
        InitializeComponent();
        MyMap.MapClicked += MyMap_MapClicked;
    }

    private void MyMap_MapClicked(object sender, MapClickedEventArgs e)
    {
        MyMap.MapElements.Clear();
        DismissKeyboard();
        double targetY = this.Height * 0.8;
        BottomPanel.TranslateTo(0, targetY, 200);
        SearchEntry.Text = string.Empty;
        SearchPanel.IsVisible = false;
        SearchResultsList.ItemsSource = null;
        RouteButton.IsVisible = true;
        PlayButton.IsVisible = true;
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

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            _ = Task.Run(async () =>
            {
                await AutoUpdateAsync();
            });
        }

        await LoadDataAsync();
        LoadMapPins();

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

    // 🔥 NEW: TEXT TO SPEECH HELPER
    async Task SpeakAsync(string text)
    {
        try
        {
            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
            {
                Volume = 1.0f
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("TTS ERROR: " + ex.Message);
        }
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

            RefreshLists(SearchEntry?.Text);

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

        RefreshLists(SearchEntry?.Text);

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

            // Bắt sự kiện khi chạm vào Pin trên bản đồ
            pin.MarkerClicked += (s, e) =>
            {
                // Ẩn bảng thông tin mặc định của bản đồ (nếu muốn tự dùng UI của mình)
                e.HideInfoWindow = true;

                // Hiển thị panel chi tiết phía dưới
                ShowPoiDetails(poi);
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

            // 🔥 NEW: AUTO SPEAK
            if (poi.DistanceKm <= TRIGGER_DISTANCE_KM && !_spokenPoiIds.Contains(poi.Id))
            {
                _spokenPoiIds.Add(poi.Id);

                string text = !string.IsNullOrWhiteSpace(poi.Description)
                    ? poi.Description
                    : poi.Name;

                _ = SpeakAsync(text);
            }

            // 🔥 NEW: reset nếu đi xa
            if (poi.DistanceKm > TRIGGER_DISTANCE_KM)
            {
                _spokenPoiIds.Remove(poi.Id);
            }
        }

        _poiList = _poiList
            .OrderBy(p => p.DistanceKm)
            .ToList();

        RefreshLists(SearchEntry?.Text);
    }

    // CLICK QUÁN → ZOOM MAP
    void PoiList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var poi = e.CurrentSelection.FirstOrDefault() as Poi;

        if (poi == null)
            return;

        ShowPoiDetails(poi);
    }

    void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var poi = e.CurrentSelection.FirstOrDefault() as Poi;

        if (poi == null)
            return;

        DismissKeyboard();
        SearchPanel.IsVisible = false;
        SearchEntry.Text = string.Empty;
        ((CollectionView)sender).SelectedItem = null;

        ShowPoiDetails(poi);
    }

    private void ShowPoiDetails(Poi poi)
    {
        _selectedPoi = poi;

        var location = new Location(poi.Latitude, poi.Longitude);

        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                location,
                Distance.FromKilometers(0.3)
            )
        );


        NearbyPoiList.IsVisible = false;
        AllPoiList.IsVisible = false;
        NearbyTitleLabel.IsVisible = false;
        AllTitleLabel.IsVisible = false;
        DetailPanel.IsVisible = true;
        TopCloseButton.IsVisible = true;

        TitleLabel.Text = poi.Name ?? "Quán gần bạn";

        DetailDescription.Text = poi.Description;
        DetailDistance.Text = $"Khoảng cách {poi.DistanceKm:0.00} km";
        RouteInfoLabel.Text = string.Empty;
        RouteInfoPanel.IsVisible = false;

        if (!string.IsNullOrEmpty(poi.ImageUrl))
            DetailImage.Source = poi.ImageUrl;

        BottomPanel.TranslateTo(0, 0, 200);
    }

    void ClearSelection_Click(object sender, EventArgs e)
    {
        if (_speechCts != null && !_speechCts.IsCancellationRequested)
        {
            _speechCts.Cancel();
        }
        double targetY = this.Height * 0.8;
        BottomPanel.TranslateTo(0, targetY, 200);
        _selectedPoi = null;
        NearbyPoiList.SelectedItem = null;
        AllPoiList.SelectedItem = null;
        DetailPanel.IsVisible = false;
        TopCloseButton.IsVisible = false;
        NearbyPoiList.IsVisible = true;
        AllPoiList.IsVisible = true;
        NearbyTitleLabel.IsVisible = true;
        AllTitleLabel.IsVisible = true;
        TitleLabel.Text = "Quán gần bạn";
        RouteInfoLabel.Text = string.Empty;
        RouteInfoPanel.IsVisible = false;
        MyMap.MapElements.Clear();
        RouteButton.IsVisible = true;
        PlayButton.IsVisible = true;
    }

    async Task<(List<Location> Points, double DurationSeconds)> GetRouteAsync(Location start, Location end)
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
                return (new List<Location>(), 0);

            var route = routes[0];
            var coordinates = route.GetProperty("geometry").GetProperty("coordinates");
            var duration = route.GetProperty("duration").GetDouble();

            var points = new List<Location>();

            foreach (var c in coordinates.EnumerateArray())
            {
                points.Add(new Location(
                    c[1].GetDouble(),
                    c[0].GetDouble()));
            }

            return (points, duration);
        }
        catch
        {
            return (new List<Location>(), 0);
        }
    }

    async Task SaveRouteAsync(Location start, Location end, List<Location> points)
    {
        var route = new RouteCache
        {
            StartLat = start.Latitude,
            StartLon = start.Longitude,
            EndLat = end.Latitude,
            EndLon = end.Longitude,
            PointsJson = JsonSerializer.Serialize(points)
        };

        await _database.SaveRouteAsync(route);
    }

    async Task<List<Location>> LoadRouteAsync(Location start, Location end)
    {
        var route = await _database.GetRouteAsync(
            start.Latitude,
            start.Longitude,
            end.Latitude,
            end.Longitude);

        if (route == null)
            return new List<Location>();

        return JsonSerializer.Deserialize<List<Location>>(route.PointsJson);
    }

    async void RouteButton_Click(object sender, EventArgs e)
    {
        if (_selectedPoi == null || _lastLocation == null)
            return;

        try
        {
            MyMap.MapElements.Clear();

            RouteButton.IsVisible = false;
            PlayButton.IsVisible = false;

            var start = _lastLocation;
            var end = new Location(_selectedPoi.Latitude, _selectedPoi.Longitude);

            List<Location> points;
            double durationSeconds = 0;

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var routeResult = await GetRouteAsync(start, end);
                points = routeResult.Points;
                durationSeconds = routeResult.DurationSeconds;

                if (points != null && points.Count > 0)
                {
                    await SaveRouteAsync(start, end, points);
                }
            }
            else
            {
                points = await LoadRouteAsync(start, end);

                if (points == null || points.Count == 0)
                {
                    await DisplayAlert("Offline", "Chưa có route lưu trước đó", "OK");
                    return;
                }
            }

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

            if (durationSeconds > 0)
            {
                var duration = TimeSpan.FromSeconds(durationSeconds);
                RouteInfoLabel.Text = $"Ô tô • {duration.Hours}h {duration.Minutes}m";
                RouteInfoPanel.IsVisible = true;
            }
            else
            {
                RouteInfoLabel.Text = "Ô tô";
                RouteInfoPanel.IsVisible = false;
            }

            double targetY = this.Height * 0.8;
            await BottomPanel.TranslateTo(0, targetY, 200);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    async void PlayButton_Click(object sender, EventArgs e)
    {
        if (_selectedPoi == null)
            return;

        // Nếu đang phát thì dừng
        if (_speechCts != null && !_speechCts.IsCancellationRequested)
        {
            _speechCts.Cancel();
            return;
        }

        string textToRead = !string.IsNullOrWhiteSpace(_selectedPoi.Description)
            ? _selectedPoi.Description
            : (!string.IsNullOrWhiteSpace(_selectedPoi.Name) ? _selectedPoi.Name : "Không có thông tin");

        // Bắt đầu phát mới
        _speechCts = new CancellationTokenSource();
        PlayButton.Text = "⏹️ Dừng";

        try
        {
            // Lấy danh sách ngôn ngữ văn bản thành giọng nói (TTS) trên máy
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            // Tìm Tiếng Việt (bạn có thể thay "vi" bằng "en" nếu muốn tiếng Anh)
            var viLocale = locales.FirstOrDefault(l => l.Language.ToLower() == "vi" || l.Language.ToLower() == "vie" || l.Country == "VN");

            await TextToSpeech.Default.SpeakAsync(textToRead, new SpeechOptions
            {
                Volume = 1.0f,
                Locale = viLocale // Truyền ngôn ngữ vào đây
            }, cancelToken: _speechCts.Token);
        }
        catch (OperationCanceledException)
        {
            // bị dừng
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể phát âm thanh: " + ex.Message, "OK");
        }
        finally
        {
            if (_speechCts != null)
            {
                _speechCts.Dispose();
                _speechCts = null;
            }

            PlayButton.Text = "📢 Phát";
        }
    }

    void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshLists(e.NewTextValue);
        UpdateSearchPanel(e.NewTextValue);
    }

    void DismissKeyboard()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (SearchEntry.IsFocused)
            {
                SearchEntry.Unfocus();
            }
        });
    }

    void MoveToUserLocation()
    {
        if (_lastLocation == null)
            return;

        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                new Location(_lastLocation.Latitude, _lastLocation.Longitude),
                Distance.FromKilometers(1)));
    }

    private void RefreshLists(string? keyword)
    {
        IEnumerable<Poi> query = _poiList;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            query = query.Where(p => (p.Name ?? "").ToLower().Contains(lowerKeyword));
        }

        var allItems = query.ToList();
        var nearbyItems = allItems.Where(p => p.DistanceKm <= 5).ToList();

        NearbyPoiList.ItemsSource = nearbyItems;
        AllPoiList.ItemsSource = allItems;
    }

    private void UpdateSearchPanel(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            DismissKeyboard();
            SearchPanel.IsVisible = false;
            SearchResultsList.ItemsSource = null;
            double closed = this.Height - 350;
            _ = BottomPanel.TranslateTo(0, closed, 200);
            return;
        }

        var matches = _poiList
            .Where(p => (p.Name ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        SearchResultsList.ItemsSource = matches;
        SearchPanel.IsVisible = matches.Count > 0;
        if (SearchPanel.IsVisible)
        {
            double targetY = this.Height * 0.8;
            _ = BottomPanel.TranslateTo(0, targetY, 200);
        }
    }

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

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}