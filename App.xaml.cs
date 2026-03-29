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

        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);

            // Kiểm tra xem deep link có đúng định dạng: foodstreet://restaurant/{id} hay không
            if (uri.Scheme.Equals("foodstreet", StringComparison.OrdinalIgnoreCase) && 
                uri.Host.Equals("restaurant", StringComparison.OrdinalIgnoreCase))
            {
                // Trích xuất ID từ đường dẫn (VD: url "/123" -> lấy "123")
                var id = uri.AbsolutePath.Trim('/');

                if (!string.IsNullOrEmpty(id))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        // Open the restaurant in the default browser
                        await Browser.Default.OpenAsync($"https://yourwebsite.com/Restaurant/Detail/{id}", BrowserLaunchMode.SystemPreferred);
                    });
                }
            }
        }
    }
}