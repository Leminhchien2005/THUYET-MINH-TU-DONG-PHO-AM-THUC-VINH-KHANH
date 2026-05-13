# 📊 Hướng dẫn Thêm Bảng NarrationLogs

## ✅ Phương pháp 1: Dùng SQL Script Trực Tiếp (Recommend)

### Bước 1: Mở MySQL Client
```bash
# Sử dụng MySQL Workbench hoặc command line
mysql -h maglev.proxy.rlwy.net -u root -pLXSpLFmhVEXTJWohkcvWfvUOTtFHMYsy -P 55832 railway
```

### Bước 2: Chạy Script
```sql
-- Copy & paste toàn bộ nội dung từ setup_narration_logs.sql
-- Hoặc chạy:
SOURCE /path/to/setup_narration_logs.sql;
```

### Bước 3: Xác Nhận
```sql
-- Kiểm tra bảng được tạo
SHOW TABLES LIKE 'NarrationLogs';

-- Kiểm tra cấu trúc
DESCRIBE NarrationLogs;

-- Kiểm tra indexes
SHOW INDEXES FROM NarrationLogs;
```

---

## ✅ Phương pháp 2: Dùng .NET EF (Alternative)

### Bước 1: Mark Migrations Đã Apply
```bash
cd ..\FoodStreetWeb

# Chạy SQL script để mark migrations as applied
# (Xem phần trên)
```

### Bước 2: Apply Migration Mới
```bash
dotnet ef database update
```

---

## 🗄️ SQL Commands Nhanh

### Thêm bảng NarrationLogs
```sql
CREATE TABLE IF NOT EXISTS `NarrationLogs` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `RestaurantId` int NOT NULL,
  `PoiId` int NOT NULL,
  `Language` varchar(10) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'vi',
  `DeviceId` longtext CHARACTER SET utf8mb4 NOT NULL,
  `ListenTime` datetime(6) NOT NULL,
  `CreatedUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_NarrationLogs_ListenTime` (`ListenTime`),
  KEY `IX_NarrationLogs_RestaurantId_ListenTime` (`RestaurantId`, `ListenTime`),
  CONSTRAINT `FK_NarrationLogs_Pois_PoiId` FOREIGN KEY (`PoiId`) REFERENCES `Pois` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### Xem bảng NarrationLogs
```sql
SELECT * FROM NarrationLogs LIMIT 10;
```

### Xóa bảng NarrationLogs (nếu cần)
```sql
DROP TABLE IF EXISTS NarrationLogs;
```

### Kiểm tra migrations applied
```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
```

---

## 📝 Ghi chú

- ✅ NarrationLog model đã được định nghĩa trong code
- ✅ DbSet<NarrationLog> đã được thêm vào AppDbContext
- ✅ Migration file đã được tạo
- ✅ Chỉ cần thêm bảng NarrationLogs vào database

---

## 🚀 Tiếp Theo

1. Chạy một trong hai phương pháp trên
2. Xác nhận bảng được tạo thành công
3. Test dashboard - tất cả metrics sẽ hoạt động
4. Dữ liệu narration sẽ được lưu vào bảng này

---

**Status:** ✅ Ready to Deploy
