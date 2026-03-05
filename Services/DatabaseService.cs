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
    }
}