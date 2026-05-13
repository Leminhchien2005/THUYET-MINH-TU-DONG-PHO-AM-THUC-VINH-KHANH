# 🗄️ Database Migration for Narration Logs

## Migration Added

File: `Migrations/20260510AddNarrationLogs.cs`

This migration creates the `NarrationLogs` table to track narration playback events.

## Table Schema

```sql
CREATE TABLE NarrationLogs (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    RestaurantId INT NOT NULL,
    PoiId INT NOT NULL,
    Language VARCHAR(10) NOT NULL DEFAULT 'vi',
    DeviceId LONGTEXT NOT NULL,
    ListenTime DATETIME(6) NOT NULL,
    CreatedUtc DATETIME(6) NOT NULL,

    INDEX IX_NarrationLogs_ListenTime (ListenTime),
    INDEX IX_NarrationLogs_RestaurantId_ListenTime (RestaurantId, ListenTime),

    FOREIGN KEY (PoiId) REFERENCES Pois(Id) ON DELETE CASCADE
);
```

## Applying Migration

### Option 1: Automatic (Recommended)
```bash
dotnet ef database update
```

### Option 2: Manual SQL
Copy the SQL schema above and execute in your MySQL database.

## Features Tracked

Each narration playback logs:
- Restaurant ID
- Device ID (user/app instance)
- Language (vi, en, zh)
- Timestamp in Vietnam timezone
- Timestamp in UTC

## API Endpoints

### Get Narration Statistics
```
GET /api/ScanAnalytics/narration-stats
  ?restaurantId={id}
  &fromUtc={date}
  &toUtc={date}

Response:
{
  "totalNarrations": 1234,
  "byRestaurant": [
    {
      "restaurantId": 123,
      "restaurantName": "Restaurant Name",
      "count": 456
    }
  ],
  "byLanguage": [
    { "language": "vi", "count": 800 },
    { "language": "en", "count": 300 },
    { "language": "zh", "count": 134 }
  ],
  "timeline": [
    { "date": "2026-05-10", "count": 150 }
  ],
  "hourly": [
    { "hour": 0, "count": 12 },
    ...
  ]
}
```

## Dashboard Integration

Admin Dashboard now shows:
- 🎙️ **Total Narrations** - Total playback count
- 📊 **Top Narrations by Restaurant** - Top 5 restaurants
- 🌍 **Narrations by Language** - Breakdown by vi, en, zh

All updating in real-time with SignalR events!

## Notes

- NarrationLog created automatically when QR is scanned and audio URL exists
- Only logs if valid audio file is configured
- Separate tracking for scans vs narration playback
- Data includes both web and mobile app narrations

Ready to use! ✅
