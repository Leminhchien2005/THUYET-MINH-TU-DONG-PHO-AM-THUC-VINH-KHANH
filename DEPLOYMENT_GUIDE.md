# 🚀 DEPLOYMENT & TESTING GUIDE

## ✅ Pre-Deployment Checklist

- [x] Build successful
- [x] No API changes
- [x] SignalR hub registered
- [x] AdminDashboard updated
- [x] QRController updated
- [ ] Audio files exist on CDN
- [ ] Audio URLs in database
- [ ] Server URL configured (MAUI)
- [ ] Test all features

## 📋 Deployment Steps

### Step 1: Deploy Backend
```bash
# Publish FoodStreetWeb
dotnet publish -c Release

# Deploy to server
# - Ensure /scanhub endpoint accessible
# - Check CORS allows SignalR
```

### Step 2: Test Admin Dashboard
```
1. Navigate to Admin > Dashboard
2. Open DevTools > Console
3. Look for:
   🔌 "SignalR connected"
   ✅ "Subscribed to all restaurants"
4. Quét QR code từ bất kỳ đâu
5. Verify:
   ✅ Heatmap updates
   ✅ totalScans increases
   ✅ Audio plays (if configured)
```

### Step 3: Add Web Component (Optional)
```html
<!-- In Restaurant/TDetail.cshtml or Landing.cshtml -->
<!-- Add at end of file before closing </body> -->

<script src="https://cdn.jsdelivr.net/npm/@@microsoft/signalr@latest/signalr.min.js"></script>
<script>
    let narrationHubConnection = null;
    let currentRestaurantId = @Model.Id;

    // Copy entire script block from NarrationListenerComponent.html
</script>
```

### Step 4: Integrate MAUI App (Manual)
```csharp
// In MainPage.xaml.cs:

// 1. Add fields
private ScanHubClient? _scanHubClient;
private ScanNarrationHub? _narrationHub;

// 2. Copy methods from MAUI_QUICK_START.cs

// 3. Call in OnAppearing
protected override async void OnAppearing()
{
    base.OnAppearing();
    await InitializeScanHubAsync();
    // ... other code ...
}

// 4. Call in OnDisappearing
protected override async void OnDisappearing()
{
    base.OnDisappearing();
    await CleanupScanHubAsync();
    // ... other code ...
}

// 5. Set server URL
Preferences.Set("server_url", "https://your-server.com");
```

## 🧪 Testing Scenarios

### Scenario 1: Admin Dashboard Real-Time Updates
```
Prerequisites:
  - Admin logged in
  - Dashboard open in browser
  - DevTools console visible

Steps:
  1. Scan QR code from MAUI app or web
  2. Observe:
     📊 Heatmap updates within 1-2 seconds
     📈 totalScans increases
     🎙️ Audio plays (if available)
     ✅ Console shows "OnScanReceived"

Expected:
  ✅ All updates happen in real-time
  ✅ No page refresh needed
  ✅ Multiple scans aggregate correctly
```

### Scenario 2: Restaurant Detail Page Narration
```
Prerequisites:
  - Restaurant detail page loaded
  - Audio component integrated

Steps:
  1. Scan QR code that targets this restaurant
  2. Observe:
     🎙️ Narration audio plays automatically
     📢 Notification appears
     ✅ Audio volume is 50%

Expected:
  ✅ Audio starts within 1 second
  ✅ Notification shows restaurant name
  ✅ No errors in console
```

### Scenario 3: MAUI App Integration
```
Prerequisites:
  - MainPage.xaml.cs updated
  - Server URL configured
  - App running

Steps:
  1. Launch app
  2. Navigate to restaurant detail
  3. Observe console:
     ✅ "ScanHub initialized successfully"
     🔌 "ScanHub status: Connected"
     ✅ "Subscribed to restaurant: {id}"
  4. Scan QR code
  5. Observe:
     📢 "Scan received: {restaurantId}"
     🎙️ "Playing narration..."

Expected:
  ✅ Connection established automatically
  ✅ Auto-reconnect if disconnected
  ✅ Events received within 1 second
```

### Scenario 4: Offline Handling
```
Steps:
  1. Disable internet (offline mode)
  2. Try to scan QR
  3. Observe:
     ✅ Error handled gracefully
     ✅ No app crash
  4. Re-enable internet
  5. Wait 5 seconds
  6. Observe:
     🔌 "SignalR reconnected"
     ✅ Connection restored

Expected:
  ✅ Graceful degradation
  ✅ Auto-reconnect works
  ✅ No data loss
```

### Scenario 5: Filter by Restaurant
```
Admin Dashboard:
  1. Open Dashboard
  2. Select specific restaurant
  3. Observe:
     ✅ "Subscribed to restaurant: {id}"
     📊 Only data for that restaurant shows
  4. Scan QR for that restaurant
  5. Observe:
     ✅ Heatmap updates
     ✅ totalScans increases
  6. Scan QR for different restaurant
  7. Observe:
     ✅ No heatmap change
     ✅ Data not updated

Expected:
  ✅ Filtering works correctly
  ✅ Only subscribed events trigger updates
```

## 🔍 Debugging

### Enable Debug Logging
```javascript
// In browser console
localStorage.setItem('DEBUG', '*');
location.reload();

// Or in code
function enableDebug() {
    window.debugSignalR = true;
}
```

### Check Connection
```javascript
// In browser console
console.log('Hub connected:', scanHubConnection.state);
console.log('Groups:', scanHubConnection.groups);
```

### MAUI App Logging
```csharp
// Console output will show:
// - 🎙️ Narration events
// - 📢 Scan events
// - 🔌 Connection status
// - ❌ Errors
```

## 🚨 Troubleshooting

### "ScanHub connection failed"
```
✓ Check /scanhub endpoint exists
✓ Check CORS configuration
✓ Check firewall allows WebSocket
✓ Check server URL is correct
✓ Check internet connection
```

### "Audio not playing"
```
✓ Check browser/device audio enabled
✓ Check audio file URL valid
✓ Check CORS for audio CDN
✓ Check audio format (MP3 recommended)
✓ Check browser console for errors
```

### "Heatmap not updating"
```
✓ Check DevTools console for errors
✓ Check /api/ScanAnalytics/heatmap returns data
✓ Check OnScanReceived event fires
✓ Check subscription group correct
✓ Check database has scan logs
```

### "MAUI App not receiving events"
```
✓ Check server URL configured
✓ Check /scanhub endpoint accessible from app
✓ Check certificate (if HTTPS)
✓ Check firewall/VPN blocking WebSocket
✓ Check internet connection
✓ Check console logs for errors
```

## 📊 Performance Metrics

### Expected Performance
```
Connection latency: < 100ms
Event delivery: < 1 second
Heatmap update: 1-2 seconds
Audio start: < 1 second
Reconnect time: < 5 seconds
```

### Monitoring
```
- Check browser DevTools Network tab
- Monitor server memory (SignalR connections)
- Check CPU usage during broadcasts
- Monitor bandwidth for audio delivery
```

## ✅ Final Checklist Before Production

- [ ] All tests passed
- [ ] Admin Dashboard works
- [ ] Audio files exist
- [ ] Server URL configured
- [ ] CORS configured
- [ ] SSL certificate valid
- [ ] Firewall allows WebSocket
- [ ] Database backed up
- [ ] Load testing done
- [ ] Monitoring configured

## 📞 Support

If issues occur:

1. **Check Logs**
   - Browser DevTools Console
   - MAUI App Debug Output
   - Server Application Logs

2. **Check Connectivity**
   - Internet connection
   - Server endpoint accessible
   - DNS resolution working

3. **Verify Configuration**
   - Server URL correct
   - Audio URLs valid
   - Database contains data
   - Permissions correct

4. **Fallback**
   - App works without narration
   - Dashboard updates via polling
   - Existing features work

## 🎯 Success Criteria

- ✅ Admin Dashboard updates real-time
- ✅ Audio plays automatically
- ✅ MAUI app receives events
- ✅ Web pages show notifications
- ✅ Filtering works correctly
- ✅ Auto-reconnect works
- ✅ No console errors
- ✅ Good performance metrics

---

**Ready for deployment!** 🚀

Once all tests pass, you're good to go live.
