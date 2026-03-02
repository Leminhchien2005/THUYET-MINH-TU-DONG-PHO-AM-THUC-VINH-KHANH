using System.IO;
using Microsoft.Maui.Storage;
using SQLite;
using FoodStreetGuide.Models;
using System.Text.Json;

namespace FoodStreetGuide.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;

        public async Task Init()
        {
            if (_database != null)
                return;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodstreet.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            // Tạo bảng theo model mới (có ImageUrl)
            await _database.CreateTableAsync<Poi>();
        }

        public async Task<List<Poi>> GetAllPoiAsync()
        {
            await Init();
            return await _database!.Table<Poi>().ToListAsync();
        }

        public async Task SeedDataAsync()
        {
            await Init();

            // 🔥 Xoá dữ liệu cũ
            await _database!.DeleteAllAsync<Poi>();

            using var stream = await FileSystem.OpenAppPackageFileAsync("poi.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var poiList = JsonSerializer.Deserialize<List<Poi>>(json, options);

            if (poiList != null && poiList.Count > 0)
            {
                await _database.InsertAllAsync(poiList);
            }
        }
    }
}