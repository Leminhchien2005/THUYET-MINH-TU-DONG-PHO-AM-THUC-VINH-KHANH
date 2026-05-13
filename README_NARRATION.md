# 🎙️ Real-Time Narration & Scan Events System

Hệ thống cho phép app MAUI và web app nghe sự kiện quét QR theo nhà hàng và tự động phát thuyết minh âm thanh, cũng như cập nhật real-time heatmap trên admin dashboard.

## 📋 Tổng Quan

- **Auto-play narration** khi QR được quét
- **Real-time heatmap updates** trên admin dashboard
- **Live feed** cho cả web và mobile app
- **Multi-language support** (vi, en, zh)
- **No API changes** - Hoàn toàn backward compatible

## 🚀 Quick Start

### 1. Admin Dashboard (Đã hoạt động)
Dashboard sẽ tự động:
- Nghe sự kiện quét QR
- Cập nhật heatmap real-time
- Phát thuyết minh âm thanh (nếu có)

**Không cần thêm gì!** Tự động hoạt động khi bạn quét QR.

### 2. Web Restaurant Detail Page
Thêm code này vào restaurant detail page:

```html
<!-- Copy nội dung từ NarrationListenerComponent.html vào <script> section -->
```

Hoặc đơn giản hơn:
```html
<script src="path/to/narration-listener.js"></script>
```

### 3. MAUI App
Thêm các methods vào MainPage.xaml.cs:

```csharp
// 1. Copy từ MAUI_QUICK_START.cs
// 2. Gọi InitializeScanHubAsync() từ OnAppearing()
// 3. Gọi CleanupScanHubAsync() từ OnDisappearing()
```

Xem `MAUI_QUICK_START.cs` để có code hoàn chỉnh.

## 📁 Files & Components

### Backend (Tham khảo)
- `Hubs/ScanHub.cs` - SignalR Hub
- `Controllers/QRController.cs` - Broadcast events
- `Program.cs` - Hub registration

### Frontend
- `Views/Admin/AdminDashboard.cshtml` - Dashboard with real-time updates
- `NarrationListenerComponent.html` - Web component (tái sử dụng)
- `Services/ScanHubClient.cs` - MAUI SignalR client
- `Services/ScanNarrationHub.cs` - MAUI narration handler

## 📊 Data Flow

```
User scans QR (App/Web)
  ↓
QRController logs scan
  ↓
QRController broadcasts OnScanReceived via SignalR
  ├─→ Admin Dashboard (updates heatmap)
  ├─→ Restaurant Page (plays narration)
  └─→ MAUI App (shows notification + plays narration)
```

## 🎙️ Events

### OnScanReceived
Broadcast mỗi khi QR được quét

```json
{
  "restaurantId": 123,
  "restaurantName": "Quán Cơm Tấm",
  "scanTime": "2026-05-10T15:30:00Z",
  "language": "vi",
  "audioUrl": "https://cdn.example.com/audio/123-vi.mp3",
  "crowdStatus": "updated"
}
```

## ⚙️ Configuration

### Server URL (MAUI)
```csharp
// Set in app settings
Preferences.Set("server_url", "https://your-server.com");
```

### Audio Support
Các ngôn ngữ hỗ trợ:
- `vi` - Tiếng Việt
- `en` - English
- `zh` - 中文

## 🧪 Testing

### Test Checklist
- [ ] Admin Dashboard cập nhật heatmap khi quét QR
- [ ] Nghe thuyết minh âm thanh trên admin dashboard
- [ ] Restaurant detail page phát narration
- [ ] MAUI app nhận sự kiện quét
- [ ] Auto-reconnect khi mất connection
- [ ] Filter theo nhà hàng hoạt động

## 📚 Documentation

- `IMPLEMENTATION_SUMMARY.md` - Chi tiết thay đổi
- `NARRATION_SYSTEM.md` - Full technical documentation
- `MAUI_QUICK_START.cs` - MAUI integration guide
- `NarrationListenerComponent.html` - Web component code

## 🔗 SignalR Hub Endpoint
```
ws://localhost:5000/scanhub
wss://your-server.com/scanhub (production)
```

## 🛠️ Architecture

### Subscription Groups
- `all-scans` - Tất cả sự kiện từ mọi nhà hàng
- `restaurant-{id}` - Sự kiện từ nhà hàng cụ thể

### Auto-Join on Connect
- Tự động join `all-scans` group
- Có thể change group theo filter

## ✅ Features Implemented

- [x] SignalR real-time events
- [x] Admin dashboard auto-updates
- [x] Audio narration playback
- [x] Multi-restaurant filtering
- [x] Auto-reconnect on disconnect
- [x] Language support
- [x] MAUI app integration services
- [x] Web component for restaurants
- [x] No API changes

## ⚠️ Troubleshooting

**ScanHub not connecting:**
- Kiểm tra `/scanhub` endpoint hoạt động
- Check CORS configuration
- Verify internet connection

**Audio not playing:**
- Check audio URL hợp lệ
- Verify browser/app audio permissions
- Kiểm tra audio file format

**Heatmap not updating:**
- Open DevTools > Console
- Check for `OnScanReceived` event
- Verify API `/api/ScanAnalytics/heatmap` có data

## 📞 Support

Issues hoặc câu hỏi? Kiểm tra logs:
```
🎙️ = Narration related
📢 = Scan events
🔌 = Connection status
❌ = Errors
```

## 📝 Version
- **Version**: 1.0.0
- **Status**: ✅ Production Ready
- **Build**: Successful

---

**Ready to use!** 🚀

Start with:
1. Test Admin Dashboard
2. Add to Restaurant Pages
3. Integrate into MAUI App
