using SQLite;
using FoodStreetGuide.Models;
using Microsoft.Maui.Storage;

namespace FoodStreetGuide.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;

        // 🔥 Khởi tạo database
        public async Task Init()
        {
            if (_database != null)
                return;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodstreet.db3");

            _database = new SQLiteAsyncConnection(dbPath);

            // 🔥 Tạo bảng Poi nếu chưa có
            await _database.CreateTableAsync<Poi>();

            await _database.CreateTableAsync<RouteCache>();
        }

        // 🔥 Lấy toàn bộ POI
        public async Task<List<Poi>> GetAllPoiAsync()
        {
            await Init();
            return await _database!.Table<Poi>().ToListAsync();
        }

        // 🔥 Thêm 1 POI
        public async Task AddPoiAsync(Poi poi)
        {
            await Init();
            await _database!.InsertAsync(poi);
        }

        // 🔥 Thêm nhiều POI
        public async Task AddPoisAsync(List<Poi> pois)
        {
            await Init();
            await _database!.InsertAllAsync(pois);
        }

        // 🔥 Xóa toàn bộ POI
        public async Task DeleteAllPoiAsync()
        {
            await Init();
            await _database!.DeleteAllAsync<Poi>();
        }

        // 🔥 Update 1 POI
        public async Task UpdatePoiAsync(Poi poi)
        {
            await Init();
            await _database!.UpdateAsync(poi);
        }

        // 🔥 Thay toàn bộ dữ liệu (dùng khi update từ API)
        public async Task ReplaceAllDataAsync(List<Poi> pois)
        {
            await Init();

            // Xóa dữ liệu cũ
            await _database!.DeleteAllAsync<Poi>();

            // Thêm dữ liệu mới
            if (pois != null && pois.Count > 0)
            {
                await _database.InsertAllAsync(pois);
            }
        }
        public async Task SaveRouteAsync(RouteCache route)
        {
            await _database.InsertAsync(route);
        }

        public async Task<RouteCache> GetRouteAsync(double slat, double slon, double elat, double elon)
        {
            return await _database.Table<RouteCache>()
                .FirstOrDefaultAsync(r =>
                    Math.Abs(r.StartLat - slat) < 0.001 &&
                    Math.Abs(r.StartLon - slon) < 0.001 &&
                    Math.Abs(r.EndLat - elat) < 0.001 &&
                    Math.Abs(r.EndLon - elon) < 0.001);
        }
    }
}