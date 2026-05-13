# 📋 NARRATION TRACKING - CONFIGURATION & DEPLOYMENT GUIDE

## 🚀 Quick Start

### 1. Apply Database Migration
```bash
cd FoodStreetWeb
dotnet ef database update
```

If you get connection errors, update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=foodstreet;User=root;Password=your_password"
  }
}
```

### 2. Verify Database
```sql
-- Check if NarrationLogs table exists
SHOW TABLES LIKE 'NarrationLogs';

-- Check table structure
DESCRIBE NarrationLogs;

-- Verify indexes
SHOW INDEXES FROM NarrationLogs;
```

### 3. Test the Feature
1. Open Admin Dashboard
2. Scan QR code from any device
3. Check if "Tổng lượt nghe thuyết minh" increases
4. Check "Top narrations by restaurant" shows data
5. Check "Narrations by language" shows breakdown

---

## 🔧 Configuration

### Audio Files
Ensure each restaurant has audio translations:

```sql
-- Check if audio files are configured
SELECT * FROM AudioTranslations 
WHERE PoiId IN (
  SELECT Id FROM Pois
)
ORDER BY LanguageCode;
```

If missing, add via Admin Panel or SQL:
```sql
INSERT INTO AudioTranslations (PoiId, LanguageCode, AudioUrl)
VALUES 
  (1, 'vi', 'https://your-cdn.com/audio/1-vi.mp3'),
  (1, 'en', 'https://your-cdn.com/audio/1-en.mp3'),
  (1, 'zh', 'https://your-cdn.com/audio/1-zh.mp3');
```

### Language Support
Current languages:
- `vi` - Tiếng Việt (Vietnamese)
- `en` - English
- `zh` - 中文 (Chinese)

To add new language:
1. Add audio to `AudioTranslations` table
2. Update `NarrationLog.Language` column max length if needed
3. Update dashboard language map (optional)

---

## 📊 API Endpoints

### Get Narration Statistics
```
GET /api/ScanAnalytics/narration-stats
```

**Query Parameters:**
- `restaurantId` (int, optional) - Filter by restaurant
- `fromUtc` (DateTime, optional) - Start date in UTC
- `toUtc` (DateTime, optional) - End date in UTC

**Example:**
```bash
curl "http://localhost:5000/api/ScanAnalytics/narration-stats?restaurantId=1&fromUtc=2026-05-01T00:00:00Z&toUtc=2026-05-10T23:59:59Z"
```

**Response:**
```json
{
  "totalNarrations": 1234,
  "restaurantId": 1,
  "fromUtc": "2026-05-01T00:00:00Z",
  "toUtc": "2026-05-10T23:59:59Z",
  "byRestaurant": [
    {
      "restaurantId": 1,
      "restaurantName": "Quán Cơm Tấm",
      "count": 456
    },
    {
      "restaurantId": 2,
      "restaurantName": "Phở Hà Nội",
      "count": 400
    }
  ],
  "byLanguage": [
    {
      "language": "vi",
      "count": 800
    },
    {
      "language": "en",
      "count": 300
    },
    {
      "language": "zh",
      "count": 134
    }
  ],
  "timeline": [
    {
      "date": "2026-05-01",
      "count": 150
    },
    {
      "date": "2026-05-02",
      "count": 180
    }
  ],
  "hourly": [
    {
      "hour": 0,
      "count": 12
    },
    {
      "hour": 1,
      "count": 15
    },
    ...
    {
      "hour": 23,
      "count": 25
    }
  ]
}
```

---

## 🧪 Testing Checklist

### Unit Testing
- [ ] NarrationLog model created successfully
- [ ] Database migration applies without errors
- [ ] NarrationLogs table has correct schema
- [ ] Indexes created correctly

### Integration Testing
- [ ] QR scan creates ScanLog entry ✅ (existing)
- [ ] QR scan creates NarrationLog entry (if audio exists)
- [ ] NarrationLog has correct timestamps
- [ ] NarrationLog has correct language code

### End-to-End Testing
- [ ] Admin Dashboard loads successfully
- [ ] "Tổng lượt nghe thuyết minh" counter displays
- [ ] "Top narrations by restaurant" shows data
- [ ] "Narrations by language" shows breakdown
- [ ] Counters update when new QR is scanned
- [ ] Filters work correctly

### Data Validation
- [ ] No duplicate entries
- [ ] Timestamps are accurate
- [ ] Device IDs logged correctly
- [ ] Language codes are valid

---

## 🐛 Troubleshooting

### Issue: Migration Fails to Apply
```
Error: Couldn't connect to server
```

**Solution:**
1. Verify database is running: `mysql -u root -p`
2. Check connection string in `appsettings.json`
3. Ensure database exists: `CREATE DATABASE foodstreet;`
4. Try again: `dotnet ef database update`

### Issue: NarrationLogs Not Created on QR Scan
```
Check if:
1. AudioTranslation exists for that restaurant
2. AudioUrl is not null
3. Language parameter in QR URL is correct
```

**Debug:**
```csharp
// Add to QRController RedeemQR() to see what's happening
System.Diagnostics.Debug.WriteLine($"Audio found: {audio != null}");
System.Diagnostics.Debug.WriteLine($"AudioUrl: {audio?.AudioUrl}");
```

### Issue: Dashboard Shows 0 Narrations
```
Check if:
1. Database migration applied successfully
2. NarrationLogs table exists and has data
3. API endpoint returns data: http://localhost:5000/api/ScanAnalytics/narration-stats
4. Console shows errors
```

### Issue: Wrong Counts
```
Verify:
1. Date range is correct in query
2. Restaurant ID filter is correct
3. No duplicate records in database
4. Timestamps are consistent
```

---

## 📈 Performance Optimization

### Database Indexes
Created for optimal query performance:
- `IX_NarrationLogs_ListenTime` - For timeline queries
- `IX_NarrationLogs_RestaurantId_ListenTime` - For restaurant+date queries

### Query Optimization
- GroupBy on database side (not memory)
- Use AsNoTracking() for read-only queries
- Efficient date range filtering

### Caching (Optional)
For high-traffic dashboards, consider:
```csharp
// Cache narration stats for 5 minutes
services.AddMemoryCache();
cache.Set("narration-stats", data, TimeSpan.FromMinutes(5));
```

---

## 🔒 Data Privacy

### No Personal Information
- DeviceId is anonymous (not linked to user)
- No tracking of individual users
- Only aggregated statistics

### GDPR Compliance
- Users can request data deletion
- Add endpoint to delete old records:

```csharp
[HttpDelete("narration-logs/{days}")]
public async Task<IActionResult> DeleteOldNarrationLogs(int days = 90)
{
    var cutoff = DateTime.Now.AddDays(-days);
    var deletedCount = await _context.NarrationLogs
        .Where(x => x.CreatedUtc < cutoff)
        .ExecuteDeleteAsync();
    return Ok(new { deletedCount });
}
```

---

## 📊 Monitoring & Alerts

### Monitor These Metrics
- Total narration playbacks per day
- Trend analysis (increasing/decreasing)
- Language distribution changes
- Outliers (sudden spikes/drops)

### Suggested Alerts
```sql
-- Alert if narrations drop below 10 per day
SELECT DATE(ListenTime) as day, COUNT(*) as count
FROM NarrationLogs
GROUP BY DATE(ListenTime)
HAVING count < 10
ORDER BY day DESC;

-- Alert if one language dominates (>90%)
SELECT Language, COUNT(*) as count
FROM NarrationLogs
WHERE DATE(ListenTime) = CURDATE()
GROUP BY Language
ORDER BY count DESC;
```

---

## 🚀 Production Deployment

### Pre-Deployment Checklist
- [ ] Migration tested on staging DB
- [ ] Dashboard UI verified
- [ ] API endpoints tested
- [ ] Performance acceptable
- [ ] Error handling in place
- [ ] Monitoring configured
- [ ] Backup taken

### Deployment Steps
1. **Backup Database**
   ```bash
   mysqldump -u root -p foodstreet > foodstreet_backup.sql
   ```

2. **Apply Migration**
   ```bash
   dotnet ef database update --configuration Release
   ```

3. **Verify**
   ```sql
   SELECT COUNT(*) FROM NarrationLogs;
   ```

4. **Monitor**
   - Check Admin Dashboard
   - Monitor logs for errors
   - Verify real-time updates work

### Rollback Plan
```bash
# If something goes wrong:
1. Restore from backup
2. Remove migration
3. Redeploy previous version
```

---

## 📞 Support & References

### Documentation
- `NARRATION_TRACKING_COMPLETE.md` - Feature overview
- `MIGRATION_NARRATION.md` - Database schema
- `NARRATION_SYSTEM.md` - Full technical docs

### Useful SQL Queries
```sql
-- Recent narrations
SELECT * FROM NarrationLogs 
ORDER BY ListenTime DESC 
LIMIT 10;

-- By restaurant
SELECT RestaurantId, COUNT(*) as count 
FROM NarrationLogs 
GROUP BY RestaurantId 
ORDER BY count DESC;

-- By language
SELECT Language, COUNT(*) as count 
FROM NarrationLogs 
GROUP BY Language 
ORDER BY count DESC;

-- Hourly distribution today
SELECT HOUR(ListenTime) as hour, COUNT(*) as count 
FROM NarrationLogs 
WHERE DATE(ListenTime) = CURDATE() 
GROUP BY HOUR(ListenTime) 
ORDER BY hour;
```

---

**Status:** ✅ Ready for Production

Start with `dotnet ef database update` and you're good to go! 🚀
