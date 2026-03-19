using FoodStreetGuide.Services;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace FoodStreetGuide
{
    public partial class App : Application
    {
        // Database dùng chung
        public static DatabaseService Database { get; private set; }

        public App()
        {
            InitializeComponent();

            // Khởi tạo database
            Database = new DatabaseService();

            // 🔥 Load ngôn ngữ đã lưu
            var lang = Preferences.Get("lang", "vi");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}