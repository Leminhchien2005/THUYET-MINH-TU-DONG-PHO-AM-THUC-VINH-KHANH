# 🎙️ Real-time Narration & Scan Events System

## Tổng Quan
Hệ thống này cho phép app MAUI và web app nghe sự kiện quét QR theo nhà hàng và tự động phát thuyết minh âm thanh, cộng với cập nhật real-time heatmap trên admin dashboard.

## Kiến Trúc

### 1. Backend - Server (Không thay đổi API logic)
- **ScanHub.cs** - SignalR Hub để broadcast scan & narration events
- **QRController.cs** - Gửi scan events với audio URL qua SignalR
- **Program.cs** - Đã đăng ký ScanHub (`app.MapHub<ScanHub>("/scanhub")`)

### 2. Frontend - Web (Razor Pages)
- **AdminDashboard.cshtml** - Admin dashboard nghe events real-time để cập nhật heatmap
- **NarrationListenerComponent.html** - Component cho Restaurant detail page

### 3. Frontend - Mobile App (MAUI)
- **ScanHubClient.cs** - SignalR client để kết nối và nhận events
- **ScanNarrationHub.cs** - Quản lý phát thuyết minh
- **ScanHubIntegration.cs** - Integration mixin cho MainPage

## Luồng Hoạt Động

### Khi QR được quét:
```
1. App/Web gọi /api/qr/redeem/{code}
2. QRController lưu vào database
3. QRController broadcast OnScanReceived event qua SignalR:
   - Nhà hàng ID
   - Tên nhà hàng
   - Thời gian quét
   - Audio URL của thuyết minh
   - Ngôn ngữ
4. Tất cả clients nghe event (admin dashboard, restaurant page, app)
5. Mỗi client xử lý event theo cách của nó:
   - Admin Dashboard: Cập nhật heatmap + tổng lượt quét
   - Web Restaurant Page: Phát thuyết minh âm thanh
   - MAUI App: Phát thuyết minh + cập nhật UI
```

## Cài Đặt

### 1. Backend

Đã hoàn tất:
- ✅ ScanHub đã tạo và cấu hình
- ✅ QRController đã cập nhật để broadcast events
- ✅ Program.cs đã đăng ký hub

### 2. Web Admin Dashboard

Đã hoàn tất:
- ✅ AdminDashboard.cshtml có SignalR client
- ✅ Listen OnScanReceived event
- ✅ Auto-refresh heatmap khi có scan mới
- ✅ Phát thuyết minh nếu có audio

### 3. Web Restaurant Detail Page

Thêm vào Restaurant/TDetail.cshtml hoặc Restaurant/Landing.cshtml:
```html
<!-- Thêm component này vào page -->
@Html.Raw(System.IO.File.ReadAllText("~../NarrationListenerComponent.html"))

<!-- Hoặc copy nội dung từ NarrationListenerComponent.html vào <script> section -->
```

### 4. MAUI App

#### Step 1: Thêm ScanHubClient vào MauiProgram.cs
```csharp
builder.Services.AddSingleton<ScanHubClient>();
builder.Services.AddSingleton<ScanNarrationHub>();
```

#### Step 2: Inject vào MainPage
```csharp
private ScanHubClient _scanHubClient;

public MainPage()
{
    InitializeComponent();
    _scanHubClient = ServiceProvider.GetService<ScanHubClient>();
}
```

#### Step 3: Gọi trong OnAppearing
```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();
    await InitializeScanHubAsync();
}
```

#### Step 4: Cleanup trong OnDisappearing
```csharp
protected override async void OnDisappearing()
{
    base.OnDisappearing();
    await CleanupScanHubAsync();
}
```

## API Events

### OnScanReceived
Được gửi qua SignalR khi có QR được quét.

**Data Structure:**
```json
{
  "restaurantId": 123,
  "restaurantName": "Quán Cơm Tấm",
  "scanTime": "2026-05-10T15:30:00Z",
  "deviceId": "device-abc-123",
  "language": "vi",
  "audioUrl": "https://cdn.example.com/audio/123-vi.mp3",
  "crowdStatus": "updated"
}
```

**Listeners:**
- Admin Dashboard: Cập nhật heatmap
- Web Restaurant Page: Phát audio
- MAUI App: Phát audio + update UI

## Subscription Groups

### All Scans
- Group: `all-scans`
- Nhận tất cả sự kiện scan từ mọi nhà hàng
- Sử dụng khi: Admin dashboard, app overview

### Restaurant-Specific
- Group: `restaurant-{restaurantId}`
- Nhận sự kiện scan chỉ từ một nhà hàng cụ thể
- Sử dụng khi: Chi tiết nhà hàng, restaurant detail page

## Configuration

### Server URL (MAUI App)
```csharp
// Đặt trong Settings/Preferences
Preferences.Set("server_url", "https://foodstreet.example.com");

// Hoặc set từ environment
var serverUrl = Preferences.Get("server_url", "http://localhost:5000");
```

### Language Support
Thuyết minh hỗ trợ nhiều ngôn ngữ:
- `vi` - Tiếng Việt
- `en` - English
- `zh` - 中文

Chọn ngôn ngữ trong QR URL query param:
```
/api/qr/redeem/{code}?language=vi
```

## Testing

### Test Admin Dashboard
1. Mở http://localhost:5000/Admin/AdminDashboard
2. Quét QR từ app/web
3. Dashboard sẽ tự động cập nhật heatmap trong 1-2 giây

### Test Web Restaurant Page
1. Mở restaurant detail page
2. Quét QR hoặc trigger scan event
3. Phải nghe thuyết minh âm thanh

### Test MAUI App
1. Chạy app
2. Navigsate đến restaurant detail
3. Quét QR hoặc trigger event
4. App phải hiện notification và phát audio

## Troubleshooting

### ScanHub không connect
- Kiểm tra `/scanhub` endpoint có hoạt động
- Kiểm tra CORS policy cho SignalR
- Kiểm tra kết nối internet

### Audio không phát
- Kiểm tra audio URL có hợp lệ
- Kiểm tra browser permissions cho audio playback
- Kiểm tra audio file format (MP3 recommended)

### Heatmap không update
- Kiểm tra AdminDashboard console logs
- Verify `OnScanReceived` event được trigger
- Kiểm tra API `/api/ScanAnalytics/heatmap` có data

### MAUI App không nhận event
- Kiểm tra ScanHub connection state
- Verify subscription group đúng
- Kiểm tra server URL config

## Performance Notes

- SignalR reconnect timeout: 5 giây
- Audio playback volume: 30-50%
- Heatmap auto-refresh: mỗi scan hoặc 2 giây
- Polling fallback: 2 giây cho toàn bộ data

## Future Enhancements

1. Push notifications khi có scan
2. Batch narration (phát liên tiếp)
3. Custom narration template per restaurant
4. Narration analytics (tracking)
5. Voice recognition feedback

## Support

Mọi vấn đề liên quan đến real-time events, liên hệ team về:
- SignalR connection issues
- Audio playback problems
- Event broadcasting failures
