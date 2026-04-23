using Android.Media.Midi;
using Android.Net;
using FoodStreetGuide.Models;
using FoodStreetGuide.Resources.Strings;
using FoodStreetGuide.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using Plugin.Maui.Audio;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace FoodStreetGuide;

[QueryProperty(nameof(PoiIdQuery), "poiId")]
public partial class MainPage : ContentPage
{
    private readonly LocationService _locationService = new();
    private readonly DatabaseService _database = new();
    private readonly ApiService _apiService = new();
    private readonly DevicePresenceService? _devicePresenceService;
    private Dictionary<int, Label> _poiLabels = new();
    private Dictionary<int, Pin> _poiPins = new();
    private int? _featuredPoiId;
    private int? _nearestPoiId;
    private static ConcurrentDictionary<int, SemaphoreSlim> _poiAudioLocks = new();

    private List<Poi> _poiList = new();

    private Location? _lastLocation;
    private string CurrentLang =>
    Preferences.Get("lang", "vi");

    double panelStart;

    Poi? _selectedPoi;
    CancellationTokenSource? _speechCts;
    bool _isAutoSpeakEnabled = true;

    private Location SmoothLocation(Location newLoc)
    {
        if (_lastLocation == null) return newLoc;

        double lat = (_lastLocation.Latitude * 0.7) + (newLoc.Latitude * 0.3);
        double lon = (_lastLocation.Longitude * 0.7) + (newLoc.Longitude * 0.3);

        return new Location(lat, lon);
    }

    // 🔥 NEW: AUTO SPEAK
    private HashSet<int> _spokenPoiIds = new();
    const double TRIGGER_DISTANCE_KM = 0.15;
    private List<int> _lastNearbyPoiIds = new();
    private DateTime _lastSpeakTime = DateTime.MinValue;

    const int MAX_POI_READ = 3; // đọc tối đa 3 quán
    const double RESET_DISTANCE_KM = 0.15; // ra khỏi vùng thì reset
    const int SPEAK_COOLDOWN_SECONDS = 10; // tránh spam
    private DateTime _lastLocationCheck = DateTime.MinValue;
    double _lastMinDistance = double.MaxValue;
    private readonly IAudioManager _audioManager = AudioManager.Current;
    private IAudioPlayer? _audioPlayer;

    bool _suppressSearchUpdate;
    bool _isInitialized;
    bool _isSyncing;
    string? _lastSyncError;
    bool _isLabelTrackingEnabled;

    private string _pendingPoiId;

    private string _poiIdQuery;

    private SemaphoreSlim GetPoiLock(int poiId)
    {
        return _poiAudioLocks.GetOrAdd(poiId, _ => new SemaphoreSlim(5));
        // 5 người nghe cùng lúc / 1 POI
    }
    public string PoiIdQuery
    {
        get => _poiIdQuery;
        set
        {
            _poiIdQuery = value;
            if (string.IsNullOrWhiteSpace(value))
                return;

            var idToFind = value.Trim();

            if (!_isInitialized)
            {
                _pendingPoiId = idToFind;
                return;
            }

            _ = Dispatcher.DispatchAsync(async () =>
            {
                await ShowPoiFromQrAndSpeakAsync(idToFind);
            });
        }
    }

    private void TryShowPendingPoi()
    {
        if (string.IsNullOrWhiteSpace(_pendingPoiId) || _poiList == null)
            return;

        var idToFind = _pendingPoiId;
        _pendingPoiId = null;

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await ShowPoiFromQrAndSpeakAsync(idToFind);
        });
    }

    public MainPage()
    {
        InitializeComponent();
        MyMap.MapClicked += MyMap_MapClicked;

        _devicePresenceService = Application.Current?.Handler?.MauiContext?.Services
            .GetService<DevicePresenceService>();

        if (_devicePresenceService != null)
        {
            _devicePresenceService.ConnectionStateChanged += OnDeviceConnectionStateChanged;
            DeviceStatusLabel.Text = _devicePresenceService.IsConnected ? "🟢 Online" : "⚫ Offline";
        }
    }

    public MainPage(string poiId) : this()
    {
        // Handle deep link navigation to specific POI
        Dispatcher.DispatchAsync(async () =>
        {
            await LoadDataAsync();
            var poi = _poiList.FirstOrDefault(p => p.Id.ToString() == poiId);
            if (poi != null)
            {
                ShowPoiDetails(poi);
            }
        });
    }

    private void MyMap_MapClicked(object sender, MapClickedEventArgs e)
    {
        MyMap.MapElements.Clear();
        DismissKeyboard();
        BottomPanel.TranslationY = 420;
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
            UpdateLabelPositions();
        }
    }

    private void ZoomOut_Clicked(object sender, EventArgs e)
    {
        if (MyMap.VisibleRegion != null)
        {
            var center = MyMap.VisibleRegion.Center;
            var radius = MyMap.VisibleRegion.Radius;
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radius.Kilometers * 2.0)));
            UpdateLabelPositions();

        }
    }

    private void OnMapSizeChanged(object sender, EventArgs e)
    {
        UpdateLabelPositions();
    }
    private void OnMapPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MyMap.VisibleRegion))
        {
            UpdateLabelPositions();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        StartLabelTracking();

        await EnsurePresenceConnectedAsync();

        if (_isInitialized)
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                _ = AutoUpdateAsync();
            }
            return;
        }

        BottomPanel.TranslationY = this.Height > 0 ? this.Height - 350 : 280;

        await _database.Init();

        var hasLocalData = (await _database.GetAllPoiAsync()).Count > 0;

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            if (!hasLocalData)
            {
                var synced = await AutoUpdateAsync();
                if (!synced)
                {
                    var detail = string.IsNullOrWhiteSpace(_lastSyncError)
                        ? string.Empty
                        : $"\n\nChi tiết: {_lastSyncError}";
                    await DisplayAlert("Lỗi", $"Không tải được dữ liệu POI từ server.{detail}", "OK");
                }
            }
            else
            {
                _ = AutoUpdateAsync();
            }
        }
        else if (!hasLocalData)
        {
            await DisplayAlert("Offline", "Thiết bị chưa có Internet và chưa có dữ liệu POI cục bộ.", "OK");
        }

        await LoadDataAsync();
        LoadMapPins();

        MyMap.SizeChanged += OnMapSizeChanged;
        MyMap.PropertyChanged += OnMapPropertyChanged;

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

            foreach (var poi in _poiList)
            {
                poi.DistanceKm = DistanceHelper.CalculateDistanceKm(
                    location.Latitude,
                    location.Longitude,
                    poi.Latitude,
                    poi.Longitude);
            }

            RefreshLists(SearchEntry?.Text);

            MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    new Location(location.Latitude, location.Longitude),
                    Distance.FromKilometers(1)
                )
            );
        }

        Dispatcher.StartTimer(TimeSpan.FromSeconds(5), () =>
        {
            _ = CheckLocationAsync();
            return true;
        });

        _isInitialized = true;

        TryShowPendingPoi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopLabelTracking();
        MyMap.SizeChanged -= OnMapSizeChanged;
        MyMap.PropertyChanged -= OnMapPropertyChanged;
    }

    private void StartLabelTracking()
    {
        if (_isLabelTrackingEnabled)
            return;

        _isLabelTrackingEnabled = true;

        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(120), () =>
        {
            if (!_isLabelTrackingEnabled)
                return false;

            UpdateLabelPositions();
            return true;
        });
    }

    private void StopLabelTracking()
    {
        _isLabelTrackingEnabled = false;
    }

    private void OnDeviceConnectionStateChanged(bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DeviceStatusLabel.Text = isOnline ? "🟢 Online" : "⚫ Offline";
        });
    }

    private async Task EnsurePresenceConnectedAsync()
    {
        if (_devicePresenceService == null)
            return;

        try
        {
            await _devicePresenceService.EnsureConnectedAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Presence] Connect failed: {ex.Message}");
            DeviceStatusLabel.Text = "⚫ Offline";
        }
    }

    public async void OpenRestaurantFromQr(string id)
    {
        var poi = await _apiService.GetPoiById(id);

        if (poi == null) return;

        string textToRead = !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description : poi.Name;

        DetailPanel.IsVisible = true;

        TitleLabel.Text = poi.Name;
        DetailDescription.Text = poi.Description;
        DetailImage.Source = poi.ImageUrl;

        var location = new Location(poi.Latitude, poi.Longitude);

        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.5)));
        UpdateLabelPositions();

        _ = SpeakAsync(textToRead);
    }

    // 🔥 NEW: TEXT TO SPEECH HELPER
    async Task SpeakAsync(string text)
    {
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            var lang = CurrentLang; // vi / en / zh

            // 🔥 map ngôn ngữ → locale chuẩn
            Locale locale = null;

            if (lang == "vi")
                locale = locales.FirstOrDefault(l => l.Language.StartsWith("vi"));

            else if (lang == "en")
                locale = locales.FirstOrDefault(l => l.Language.StartsWith("en"));

            else if (lang == "zh")
                locale = locales.FirstOrDefault(l =>
                    l.Language.StartsWith("zh") || l.Language.StartsWith("cmn"));

            // fallback nếu không có
            locale ??= locales.FirstOrDefault();

            // 🔥 cancel cái đang phát (nếu có)
            _speechCts?.Cancel();
            _speechCts = new CancellationTokenSource();

            // 🔥 hiện nút stop
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StopAutoSpeakButton.IsVisible = true;
            });

            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
            {
                Volume = (float)Preferences.Get("volume", 1.0),
                Locale = locale
            }, cancelToken: _speechCts.Token);

            // 🔥 đọc xong thì ẩn nút
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StopAutoSpeakButton.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("TTS ERROR: " + ex.Message);
        }
        finally
        {
            _speechCts?.Dispose();
            _speechCts = null; // 🔥 FIX CHÍNH
        }
    }

    // LẤY DỮ LIỆU API → SQLITE
    private async Task<bool> AutoUpdateAsync()
    {
        if (_isSyncing)
            return false;

        _isSyncing = true;
        _lastSyncError = null;

        try
        {
            var apiService = new ApiService();
            var pois = await apiService.GetPoisWithFoodsAsync();

            if (pois == null || pois.Count == 0)
            {
                Debug.WriteLine("API EMPTY");
                Console.WriteLine("API EMPTY");
                _lastSyncError = "API trả về rỗng";
                return false;
            }

            foreach (var poi in pois)
            {
                foreach (var food in poi.Foods)
                {
                    Debug.WriteLine($"FOOD: {food.Name} - {food.Description} - {food.ImageUrl}");
                }
            }

            Debug.WriteLine("API COUNT: " + pois.Count);
            Console.WriteLine("API COUNT: " + pois.Count);

            // =========================
            // CLEAR DATABASE
            // =========================
            var poiEntities = new List<Poi>();
            var foodEntities = new List<Food>();
            var foodTransEntities = new List<FoodTranslation>();
            var poiTransEntities = new List<PoiTranslation>();

            foreach (var poi in pois)
            {
                // ================= POI =================
                poiEntities.Add(new Poi
                {
                    Id = poi.Id,
                    Name = poi.Name,
                    Latitude = poi.Latitude,
                    Longitude = poi.Longitude,
                    Radius = poi.Radius,
                    Description = poi.Description,
                    ImageUrl = poi.ImageUrl
                });

                // ================= FOOD =================
                if (poi.Foods != null)
                {
                    foreach (var food in poi.Foods)
                    {
                        foodEntities.Add(new Food
                        {
                            Id = food.Id,
                            Name = food.Name,
                            Price = food.Price,
                            Description = food.Description,
                            ImageUrl = food.ImageUrl,
                            PoiId = food.PoiId
                        });

                        if (food.Translations != null)
                        {
                            foreach (var t in food.Translations)
                            {
                                foodTransEntities.Add(new FoodTranslation
                                {
                                    FoodId = food.Id,
                                    LanguageCode = t.LanguageCode,
                                    Name = t.Name,
                                    Description = t.Description
                                });
                            }
                        }
                    }
                }

                // ================= POI TRANSLATION =================
                if (poi.Translations != null)
                {
                    foreach (var t in poi.Translations)
                    {
                        poiTransEntities.Add(new PoiTranslation
                        {

                            PoiId = poi.Id,
                            LanguageCode = t.LanguageCode,
                            Name = t.Name,
                            Description = t.Description
                        });
                    }
                }
            }

            await _database.RunInTransactionAsync(conn =>
            {
                conn.DeleteAll<Poi>();
                conn.DeleteAll<Food>();
                conn.DeleteAll<FoodTranslation>();
                conn.DeleteAll<PoiTranslation>();

                conn.InsertAll(poiEntities);
                conn.InsertAll(foodEntities);
                conn.InsertAll(foodTransEntities);
                conn.InsertAll(poiTransEntities);
            });

            // ================= BULK INSERT =================

            var sqliteList = await _database.GetAllPoiAsync();

            Debug.WriteLine("SQLITE COUNT: " + sqliteList.Count);
            Console.WriteLine("SQLITE COUNT: " + sqliteList.Count);

            var poiTrans = await _database.GetAllPoiTranslationAsync();

            _poiList = sqliteList
                .Select(p => LocalizePoi(p, poiTrans))
                .ToList();

            _featuredPoiId = await _apiService.GetFeaturedRestaurantIdAsync();

            await DebugDatabaseAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                RefreshLists(SearchEntry?.Text);
                LoadMapPins();
                TryShowPendingPoi();
            });

            return true;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"API HTTP ERROR: {ex.Message}");
            Console.WriteLine($"API HTTP ERROR: {ex.Message}");
            _lastSyncError = ex.Message;
            return false;
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("API TIMEOUT");
            Console.WriteLine("API TIMEOUT");
            _lastSyncError = "Kết nối quá thời gian chờ (timeout)";
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine("ERROR: " + ex.Message);
            _lastSyncError = ex.Message;
            return false;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    // LOAD SQLITE → LIST
    private async Task LoadDataAsync()
    {
        var pois = await _database.GetAllPoiAsync();
        var poiTrans = await _database.GetAllPoiTranslationAsync();

        _poiList = pois
            .Select(p => LocalizePoi(p, poiTrans))
            .ToList();

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            _featuredPoiId = await _apiService.GetFeaturedRestaurantIdAsync();
        }

        RefreshLists(SearchEntry?.Text);
    }

    // HIỆN PIN TRÊN MAP
    private void LoadMapPins()
    {
        MyMap.Pins.Clear();
        LabelContainer.Children.Clear();
        _poiLabels.Clear();
        _poiPins.Clear();

        foreach (var poi in _poiList)
        {
            var pin = new Pin
            {
                Label = poi.Name ?? "",
                Address = poi.Description ?? "",
                Location = new Location(poi.Latitude, poi.Longitude)
            };
            pin.MarkerClicked += (s, e) =>
            {
                ShowPoiDetails(poi);
                e.HideInfoWindow = true;
            };
            MyMap.Pins.Add(pin);
            _poiPins[poi.Id] = pin;

            var nameLabel = new Label
            {
                Text = poi.Name,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#DDFFFFFF"),
                TextColor = Colors.Black,
                Padding = new Thickness(6, 3),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                InputTransparent = true
            };
            // KHÔNG dùng Border.CornerRadiusProperty

            _poiLabels[poi.Id] = nameLabel;
            LabelContainer.Children.Add(nameLabel);
        }

        UpdateNearestPoiHighlight();
        UpdateLabelPositions();
    }

    private void UpdateNearestPoiHighlight()
    {
        foreach (var poi in _poiList)
        {
            var displayName = string.IsNullOrWhiteSpace(poi.Name)
                ? $"POI #{poi.Id}"
                : poi.Name;

            var isNearby = poi.DistanceKm <= (poi.Radius / 1000.0);
            var isFeatured = _featuredPoiId.HasValue && poi.Id == _featuredPoiId.Value;
            var displayWithBadge = isFeatured ? $"⭐ {displayName}" : displayName;

            if (_poiPins.TryGetValue(poi.Id, out var pin))
            {
                pin.Label = displayWithBadge;
            }

            if (_poiLabels.TryGetValue(poi.Id, out var label))
            {
                label.Text = displayWithBadge;

                if (isFeatured)
                {
                    label.BackgroundColor = Color.FromArgb("#FFF4CE");
                    label.TextColor = Color.FromArgb("#B45309");
                }
                else if (isNearby)
                {
                    label.BackgroundColor = Color.FromArgb("#DBEAFE");
                    label.TextColor = Color.FromArgb("#1E40AF");
                }
                else
                {
                    label.BackgroundColor = Color.FromArgb("#DDFFFFFF");
                    label.TextColor = Colors.Black;
                }
            }
        }
    }

    private void UpdateLabelPositions()
    {
        if (MyMap.VisibleRegion == null || LabelContainer.Width <= 0 || LabelContainer.Height <= 0)
            return;

        foreach (var kvp in _poiLabels)
        {
            var poi = _poiList.FirstOrDefault(p => p.Id == kvp.Key);
            if (poi == null) continue;

            var screenPos = LocationToScreen(new Location(poi.Latitude, poi.Longitude));
            if (screenPos.HasValue)
            {
                // Đặt label ở vị trí pixel tuyệt đối
                double labelWidth = 100;
                double labelHeight = 25;
                double x = screenPos.Value.X - (labelWidth / 2); // căn giữa theo chiều ngang
                double y = screenPos.Value.Y - labelHeight - 5;  // ở phía trên pin

                AbsoluteLayout.SetLayoutBounds(kvp.Value, new Rect(x, y, labelWidth, labelHeight));
                AbsoluteLayout.SetLayoutFlags(kvp.Value, AbsoluteLayoutFlags.None); // pixel tuyệt đối
                kvp.Value.IsVisible = true;
            }
            else
            {
                kvp.Value.IsVisible = false;
            }
        }

    }

    private Point? LocationToScreen(Location location)
    {
        if (MyMap.VisibleRegion == null || MyMap.Width <= 0 || MyMap.Height <= 0)
            return null;

        var region = MyMap.VisibleRegion;
        double mapWidth = MyMap.Width;
        double mapHeight = MyMap.Height;

        // Tính offset tương đối (0..1)
        double x = (location.Longitude - region.Center.Longitude) / region.LongitudeDegrees + 0.5;
        double y = 0.5 - (location.Latitude - region.Center.Latitude) / region.LatitudeDegrees;

        return new Point(x * mapWidth, y * mapHeight);
    }

    // CẬP NHẬT VỊ TRÍ
    async Task CheckLocationAsync()
    {
        if ((DateTime.Now - _lastLocationCheck).TotalSeconds < 2)
            return;

        _lastLocationCheck = DateTime.Now;

        var location = await _locationService.GetCurrentLocationAsync();

        if (location == null)
            return;

        location = SmoothLocation(location);

        if (location == null)
            return;

        double moveDistance = 0;

        if (_lastLocation != null)
        {
            moveDistance = Location.CalculateDistance(
                _lastLocation,
                location,
                DistanceUnits.Kilometers
            ) * 1000;
        }

        if (_lastLocation != null)
        {
            var distance = Location.CalculateDistance(_lastLocation, location, DistanceUnits.Kilometers);

            if (distance > 0.2)
                return;

            moveDistance = distance * 1000;
        }

        // 🔥 nếu user di chuyển xa lại → bật lại auto speak
        if (moveDistance > 15)
        {
            _isAutoSpeakEnabled = true;
        }

        // 🔥 detect đứng yên
        bool isStandingStill = moveDistance < 2;


        _lastLocation = location;

        LatLabel.Text = $"Latitude: {location.Latitude:F6}";

        // =======================
        // TÍNH DISTANCE
        // =======================
        foreach (var poi in _poiList)
        {
            poi.DistanceKm = DistanceHelper.CalculateDistanceKm(
                location.Latitude,
                location.Longitude,
                poi.Latitude,
                poi.Longitude);
        }

        RefreshLists(SearchEntry?.Text);

        // =======================
        // LẤY POI GẦN
        // =======================
        var nearbyPois = _poiList
            .Where(p => p.DistanceKm <= (p.Radius / 1000.0))
            .OrderByDescending(p => p.Priority)              // ưu tiên
            .ThenBy(p => p.DistanceKm)                       // gần hơn
            .ThenByDescending(p => p.Radius)                 // radius lớn hơn trước
            .ThenByDescending(p => !string.IsNullOrEmpty(p.Description))
            .ThenBy(p => p.Id)
            .ToList();

        // 🔥 CHỐNG NHẢY DISTANCE (tránh spam đọc)
        if (nearbyPois.Count > 0)
        {
            var minDistance = nearbyPois.First().DistanceKm;

            if (Math.Abs(minDistance - _lastMinDistance) < 0.02)
                return; // thay đổi quá nhỏ → bỏ qua lần check này

            _lastMinDistance = minDistance;
        }

        var currentIds = nearbyPois.Select(p => p.Id).ToList();

        // ✅ CHỈ check POI MỚI (không phải reorder)
        bool hasNewPoi = currentIds.Except(_lastNearbyPoiIds).Any();

        if (nearbyPois.Count > 0 && hasNewPoi)
        {
            _ = _apiService.ReportEnterPoiZoneAsync(nearbyPois.Select(p => p.Id).ToList());
        }

        // =======================
        // CHECK COOLDOWN
        // =======================
        bool canSpeak = (DateTime.Now - _lastSpeakTime).TotalSeconds >
            (isStandingStill ? 30 : SPEAK_COOLDOWN_SECONDS);

        // =======================
        // SPEAK
        // =======================
        if (_isAutoSpeakEnabled && nearbyPois.Count > 0 && hasNewPoi && canSpeak)
        {
            // ✅ đang đọc thì bỏ qua
            if (_speechCts != null && !_speechCts.IsCancellationRequested)
            {
                _speechCts.Cancel(); // 🔥 thêm dòng này
            }

            if (nearbyPois.Count == 1)
            {
                var poi = nearbyPois.First();

                string text = BuildSpeakText(new List<Poi> { poi });

                if (!string.IsNullOrWhiteSpace(text))
                {
                    _lastNearbyPoiIds = currentIds;
                    _lastSpeakTime = DateTime.Now;

                    _ = AutoSpeakPoiAsync(poi, text);
                }
            }
            else
            {
                _lastNearbyPoiIds = currentIds;
                _lastSpeakTime = DateTime.Now;

                _ = AutoSpeakNearbyPoisAsync(nearbyPois);
            }
        }

        // =======================
        // RESET KHI RA KHỎI VÙNG
        // =======================
        if (nearbyPois.Count == 0)
        {
            _lastNearbyPoiIds.Clear();
        }

        // =======================
        // SORT + REFRESH UI
        // =======================
        if (moveDistance > 10)
        {
            _poiList = _poiList
                .OrderBy(p => p.DistanceKm)
                .ToList();

            RefreshLists(SearchEntry?.Text);
        }

        if (moveDistance > 10)
        {
            RefreshLists(SearchEntry?.Text);
        }
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

        // 🔥 CẬP NHẬT KHOẢNG CÁCH TRƯỚC KHI HIỂN THỊ
        if (_lastLocation != null)
        {
            poi.DistanceKm = DistanceHelper.CalculateDistanceKm(
                _lastLocation.Latitude,
                _lastLocation.Longitude,
                poi.Latitude,
                poi.Longitude);
        }

        var location = new Location(poi.Latitude, poi.Longitude);

        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                location,
                Distance.FromKilometers(0.3)
            )
        );
        UpdateLabelPositions();

        BottomPanel.TranslationY = 280;

        NearbyPoiList.IsVisible = false;
        AllPoiList.IsVisible = false;
        NearbyTitleLabel.IsVisible = false;
        AllTitleLabel.IsVisible = false;
        DetailPanel.IsVisible = true;
        TopCloseButton.IsVisible = true;
        BottomTabBar.IsVisible = false;

        TitleLabel.Text = poi.Name ?? AppResources.NearbyTitle;

        DetailDescription.Text = poi.Description;
        DetailDistance.Text = string.Format(AppResources.DistanceFormat, poi.DistanceKm);
        RouteInfoLabel.Text = string.Empty;
        RouteInfoPanel.IsVisible = false;

        if (!string.IsNullOrEmpty(poi.ImageUrl))
            DetailImage.Source = poi.ImageUrl;

        // 🔥 LOAD FOOD LIST
        LoadFoodList(poi.Id);
    }

    // 🔥 LOAD FOOD LIST CỦA POI
    private async void LoadFoodList(int poiId)
    {
        try
        {
            var foods = await _database.GetFoodsByPoiIdAsync(poiId);
            var foodTrans = await _database.GetAllFoodTranslationAsync();

            var localizedFoods = foods
                .Select(f => LocalizeFood(f, foodTrans))
                .ToList();

            DetailFoodList.ItemsSource = localizedFoods;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading foods: {ex.Message}");
            DetailFoodList.ItemsSource = new List<Food>();
        }
    }

    void ClearSelection_Click(object sender, EventArgs e)
    {
        if (_speechCts != null && !_speechCts.IsCancellationRequested)
        {
            _speechCts.Cancel();
        }

        BottomPanel.TranslationY = 420;

        _selectedPoi = null;
        NearbyPoiList.SelectedItem = null;
        AllPoiList.SelectedItem = null;
        DetailPanel.IsVisible = false;
        TopCloseButton.IsVisible = false;
        NearbyPoiList.IsVisible = true;
        AllPoiList.IsVisible = true;
        NearbyTitleLabel.IsVisible = true;
        AllTitleLabel.IsVisible = true;
        TitleLabel.Text = AppResources.NearbyTitle;
        RouteInfoLabel.Text = string.Empty;
        RouteInfoPanel.IsVisible = false;
        MyMap.MapElements.Clear();
        RouteButton.IsVisible = true;
        PlayButton.IsVisible = true;
        BottomTabBar.IsVisible = true;
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

            double targetY = this.Height > 0 ? this.Height - 350 : 280;
            await BottomPanel.TranslateTo(0, targetY, 250, Easing.CubicOut);
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

            _audioPlayer?.Stop();

            return;
        }

        string textToRead = !string.IsNullOrWhiteSpace(_selectedPoi.Description)
            ? _selectedPoi.Description
            : (!string.IsNullOrWhiteSpace(_selectedPoi.Name)
                ? _selectedPoi.Name
                : AppResources.NoInfo);

        _speechCts = new CancellationTokenSource();

        PlayButton.Text = "⏹️";

        try
        {
            var lang = Preferences.Get("lang", "vi");

            // =========================
            // ƯU TIÊN AUDIO ONLINE
            // =========================
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var audioUrl = await _apiService.GetAudioUrlAsync(
                    _selectedPoi.Id,
                    lang);

                if (!string.IsNullOrWhiteSpace(audioUrl))
                {
                    var tempFile = Path.Combine(
                        FileSystem.CacheDirectory,
                        $"audio_{_selectedPoi.Id}_{lang}.m4a");

                    using (var httpClient = new HttpClient())
                    {
                        var bytes = await httpClient.GetByteArrayAsync(audioUrl);

                        await File.WriteAllBytesAsync(tempFile, bytes);
                    }

                    var stream = File.OpenRead(tempFile);

                    _audioPlayer = _audioManager.CreatePlayer(stream);

                    _audioPlayer.Play();

                    // chờ phát xong
                    while (_audioPlayer.IsPlaying)
                    {
                        await Task.Delay(300);
                    }

                    _audioPlayer.Dispose();
                    _audioPlayer = null;

                    return;
                }
            }

            // =========================
            // FALLBACK → TTS
            // =========================
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            var locale = locales.FirstOrDefault(l =>
                            l.Language.StartsWith(lang))
                         ?? locales.FirstOrDefault();

            await TextToSpeech.Default.SpeakAsync(
                textToRead,
                new SpeechOptions
                {
                    Volume = (float)Preferences.Get("volume", 1.0),
                    Locale = locale
                },
                cancelToken: _speechCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                AppResources.Error,
                AppResources.TtsError + ex.Message,
                "OK");
        }
        finally
        {
            if (_speechCts != null)
            {
                _speechCts.Dispose();
                _speechCts = null;
            }

            PlayButton.Text = "📢";
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

    void HomeTab_Tapped(object sender, TappedEventArgs e)
    {
        SearchEntry.Text = string.Empty;
        SearchPanel.IsVisible = false;
        SearchResultsList.ItemsSource = null;

        if (_selectedPoi != null)
        {
            ClearSelection_Click(this, EventArgs.Empty);
        }

        MoveToUserLocation();
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
        var nearbyItems = allItems
            .Where(p => p.DistanceKm <= (p.Radius / 1000.0))
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.DistanceKm)
            .ThenByDescending(p => p.Radius)
            .ThenByDescending(p => !string.IsNullOrEmpty(p.Description))
            .ThenBy(p => p.Id)
            .ToList();

        UpdateNearestPoiHighlight();

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
        double middle = this.Height > 0 ? this.Height - 350 : 280;
        double closed = this.Height > 0 ? this.Height - 150 : 600;

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
                double currentY = BottomPanel.TranslationY;

                // Find the nearest snap point
                double distFull = Math.Abs(currentY - full);
                double distMiddle = Math.Abs(currentY - middle);
                double distClosed = Math.Abs(currentY - closed);

                double targetY = full; // Default to full

                if (distMiddle < distFull && distMiddle < distClosed)
                {
                    targetY = middle;
                }
                else if (distClosed < distFull && distClosed < distMiddle)
                {
                    targetY = closed;
                }

                await BottomPanel.TranslateTo(0, targetY, 250, Easing.CubicOut);
                break;
        }
    }

    private async void OnSettingsClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    private async void QrTab_Tapped(object sender, EventArgs e)
    {
        var view = sender as View;
        await view.ScaleTo(0.9, 100);
        await view.ScaleTo(1, 100);

        await Shell.Current.GoToAsync(nameof(QrScanPage));
    }

    private async Task DebugDatabaseAsync()
    {
        var pois = await _database.GetAllPoiAsync();
        var foods = await _database.GetAllFoodAsync();
        var poiTrans = await _database.GetAllPoiTranslationAsync();
        var foodTrans = await _database.GetAllFoodTranslationAsync();

        // ================= POI =================
        System.Diagnostics.Debug.WriteLine("===== POI =====");
        foreach (var p in pois)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[{p.Id}] {p.Name} - {p.Description}"
            );
        }

        // ================= FOOD =================
        System.Diagnostics.Debug.WriteLine("===== FOOD =====");
        foreach (var f in foods)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[{f.Id}] {f.Name} - {f.Description} (POI {f.PoiId})"
            );
        }

        // ================= POI TRANSLATION =================
        System.Diagnostics.Debug.WriteLine("===== POI TRANSLATION =====");
        foreach (var t in poiTrans)
        {
            System.Diagnostics.Debug.WriteLine(
                $"POI_ID: {t.PoiId} | LANG: {t.LanguageCode} | NAME: {t.Name} | DESC: {t.Description}"
            );
        }

        // ================= FOOD TRANSLATION =================
        System.Diagnostics.Debug.WriteLine("===== FOOD TRANSLATION =====");
        foreach (var t in foodTrans)
        {
            System.Diagnostics.Debug.WriteLine(
                $"FOOD_ID: {t.FoodId} | LANG: {t.LanguageCode} | NAME: {t.Name} | DESC: {t.Description}"
            );
        }
    }

    private Poi LocalizePoi(Poi poi, List<PoiTranslation> trans)
    {
        var t = trans.FirstOrDefault(x =>
                    x.PoiId == poi.Id && x.LanguageCode == CurrentLang)
                ?? trans.FirstOrDefault(x =>
                    x.PoiId == poi.Id && x.LanguageCode == "vi");

        if (t != null)
        {
            poi.Name = t.Name;
            poi.Description = t.Description;
        }

        if (string.IsNullOrWhiteSpace(poi.Name))
        {
            poi.Name = $"POI #{poi.Id}";
        }

        return poi;
    }

    private string BuildSpeakText(List<Poi> nearby)
    {
        if (nearby == null || nearby.Count == 0)
            return "";

        // =========================
        // CASE 1 QUÁN
        // =========================
        if (nearby.Count == 1)
        {
            var p = nearby.First();
            int meters = (int)(p.DistanceKm * 1000);

            switch (CurrentLang)
            {
                case "en":
                    return !string.IsNullOrWhiteSpace(p.Description)
                        ? $"{p.Name}, about {meters} meters away. {p.Description}"
                        : $"{p.Name}, about {meters} meters away";

                case "zh":
                    return !string.IsNullOrWhiteSpace(p.Description)
                        ? $"{p.Name}，距离您大约{meters}米。{p.Description}"
                        : $"{p.Name}，距离您大约{meters}米";

                default: // vi
                    return !string.IsNullOrWhiteSpace(p.Description)
                        ? $"{p.Name}, cách bạn khoảng {meters} mét. {p.Description}"
                        : $"{p.Name}, cách bạn khoảng {meters} mét";
            }
        }

        // =========================
        // NHIỀU QUÁN
        // =========================
        var top = nearby.Take(3).ToList();

        var parts = top.Select(p =>
        {
            int meters = (int)(p.DistanceKm * 1000);

            switch (CurrentLang)
            {
                case "en":
                    return !string.IsNullOrWhiteSpace(p.Description)
                        ? $"{p.Name}, about {meters} meters away, {p.Description}"
                        : $"{p.Name}, about {meters} meters away";

                case "zh":
                    return !string.IsNullOrWhiteSpace(p.Description)
                        ? $"{p.Name}，距离{meters}米，{p.Description}"
                        : $"{p.Name}，距离{meters}米";

                default:
                    return !string.IsNullOrWhiteSpace(p.Description)
                        ? $"{p.Name}, cách bạn khoảng {meters} mét, {p.Description}"
                        : $"{p.Name}, cách bạn khoảng {meters} mét";
            }
        });

        string text = "";

        // =========================
        // MỞ ĐẦU
        // =========================
        switch (CurrentLang)
        {
            case "en":
                text = $"There are {nearby.Count} places near you. ";
                break;

            case "zh":
                text = $"您附近有{nearby.Count}个地点。";
                break;

            default:
                text = $"Bạn đang gần {nearby.Count} địa điểm. ";
                break;
        }

        text += string.Join(". ", parts);

        // =========================
        // CÒN LẠI
        // =========================
        if (nearby.Count > 3)
        {
            switch (CurrentLang)
            {
                case "en":
                    text += $". And {nearby.Count - 3} more places nearby";
                    break;

                case "zh":
                    text += $"。还有{nearby.Count - 3}个地点在附近";
                    break;

                default:
                    text += $". Và còn {nearby.Count - 3} địa điểm khác gần bạn";
                    break;
            }
        }

        return text;
    }

    void StopAutoSpeak_Clicked(object sender, EventArgs e)
    {
        // 🔥 tắt auto speak luôn
        _isAutoSpeakEnabled = false;

        // 🔥 dừng giọng đang phát
        if (_speechCts != null && !_speechCts.IsCancellationRequested)
        {
            _speechCts.Cancel();
        }

        StopAutoSpeakButton.IsVisible = false;
    }

    void EnableAutoSpeak()
    {
        _isAutoSpeakEnabled = true;
    }

    private Food LocalizeFood(Food food, List<FoodTranslation> trans)
    {
        var t = trans.FirstOrDefault(x =>
                    x.FoodId == food.Id && x.LanguageCode == CurrentLang)
                ?? trans.FirstOrDefault(x =>
                    x.FoodId == food.Id && x.LanguageCode == "vi");

        if (t != null)
        {
            food.Name = t.Name;
            food.Description = t.Description;
        }

        return food;
    }

    private async Task ShowPoiFromQrAndSpeakAsync(string poiId)
    {
        var poi = _poiList.FirstOrDefault(p => p.Id.ToString() == poiId);
        if (poi == null)
            return;

        ShowPoiDetails(poi);

        var textToRead = !string.IsNullOrWhiteSpace(poi.Description)
            ? poi.Description
            : poi.Name;

        if (!string.IsNullOrWhiteSpace(textToRead))
        {
            await SpeakAsync(textToRead);
        }
    }

    async Task AutoSpeakPoiAsync(Poi poi, string fallbackText)
    {
        var sem = GetPoiLock(poi.Id);
        await sem.WaitAsync();

        try
        {
            // stop speech cũ
            if (_speechCts != null && !_speechCts.IsCancellationRequested)
                _speechCts.Cancel();

            _audioPlayer?.Stop();
            _audioPlayer?.Dispose();
            _audioPlayer = null;

            _speechCts = new CancellationTokenSource();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                StopAutoSpeakButton.IsVisible = true;
            });

            var lang = Preferences.Get("lang", "vi");

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    var audioUrl = await _apiService.GetAudioUrlAsync(poi.Id, lang);

                    if (!string.IsNullOrWhiteSpace(audioUrl))
                    {
                        var tempFile = Path.Combine(
                            FileSystem.CacheDirectory,
                            $"auto_audio_{poi.Id}_{lang}.m4a");

                        using (var httpClient = new HttpClient())
                        {
                            var bytes = await httpClient.GetByteArrayAsync(audioUrl);
                            await File.WriteAllBytesAsync(tempFile, bytes);
                        }

                        using var stream = File.OpenRead(tempFile);
                        _audioPlayer = _audioManager.CreatePlayer(stream);

                        _audioPlayer.Play();

                        while (_audioPlayer.IsPlaying)
                        {
                            if (_speechCts.IsCancellationRequested)
                            {
                                _audioPlayer.Stop();
                                break;
                            }

                            await Task.Delay(300);
                        }

                        _audioPlayer.Dispose();
                        _audioPlayer = null;

                        return; // OK vì có finally bảo vệ
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("AUTO AUDIO ERROR: " + ex.Message);
                }
            }

            // fallback TTS
            await SpeakAsync(fallbackText);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("AUTO SPEAK ERROR: " + ex.Message);
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StopAutoSpeakButton.IsVisible = false;
            });

            sem.Release(); // ✅ luôn chạy an toàn
        }
    }

    private async Task AutoSpeakNearbyPoisAsync(List<Poi> pois)
    {
        if (pois == null || pois.Count == 0)
            return;

        // =========================
        // ĐỌC MỞ ĐẦU
        // =========================
        string intro = CurrentLang switch
        {
            "en" => pois.Count == 1
                ? "There is 1 place near you."
                : $"There are {pois.Count} places near you.",

            "zh" => pois.Count == 1
                ? "您附近有1个地点。"
                : $"您附近有{pois.Count}个地点。",

            _ => pois.Count == 1
                ? "Bạn đang gần 1 địa điểm."
                : $"Bạn đang gần {pois.Count} địa điểm."
        };

        await SpeakAsync(intro);

        // =========================
        // CHỈ ĐỌC TỐI ĐA 3 QUÁN
        // =========================
        var topPois = pois.Take(3).ToList();

        foreach (var poi in topPois)
        {
            if (!_isAutoSpeakEnabled)
                return;

            string text = BuildSpeakText(new List<Poi> { poi });

            var sem = GetPoiLock(poi.Id);
            await sem.WaitAsync(); // 🔥 VÀO HÀNG ĐỢI

            try
            {
                var lang = Preferences.Get("lang", "vi");

                // =========================
                // ƯU TIÊN AUDIO ONLINE
                // =========================
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var audioUrl = await _apiService.GetAudioUrlAsync(poi.Id, lang);

                    if (!string.IsNullOrWhiteSpace(audioUrl))
                    {
                        var tempFile = Path.Combine(
                            FileSystem.CacheDirectory,
                            $"auto_audio_{poi.Id}_{lang}.m4a");

                        using (var httpClient = new HttpClient())
                        {
                            var bytes = await httpClient.GetByteArrayAsync(audioUrl);
                            await File.WriteAllBytesAsync(tempFile, bytes);
                        }

                        using var stream = File.OpenRead(tempFile);

                        _audioPlayer = _audioManager.CreatePlayer(stream);
                        _audioPlayer.Play();

                        while (_audioPlayer.IsPlaying)
                        {
                            if (!_isAutoSpeakEnabled)
                            {
                                _audioPlayer.Stop();
                                break;
                            }

                            await Task.Delay(300);
                        }

                        _audioPlayer.Dispose();
                        _audioPlayer = null;

                        await Task.Delay(500);
                    }
                    else
                    {
                        // fallback nếu không có audio
                        await SpeakAsync(text);
                    }
                }
                else
                {
                    // =========================
                    // OFFLINE → TTS
                    // =========================
                    await SpeakAsync(text);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AUTO SPEAK ERROR: {ex.Message}");
            }
            finally
            {
                sem.Release(); // 🔥 LUÔN GIẢI PHÓNG QUEUE
            }

            await Task.Delay(500); // nghỉ giữa các quán
        }

        // =========================
        // THÔNG BÁO CÒN QUÁN KHÁC
        // =========================
        if (pois.Count > 3)
        {
            string remain = CurrentLang switch
            {
                "en" => $"And {pois.Count - 3} more places nearby.",
                "zh" => $"还有{pois.Count - 3}个地点在附近。",
                _ => $"Và còn {pois.Count - 3} địa điểm khác gần bạn."
            };

            await SpeakAsync(remain);
        }
    }
}