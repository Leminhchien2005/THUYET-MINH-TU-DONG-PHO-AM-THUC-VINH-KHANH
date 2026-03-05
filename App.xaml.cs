using FoodStreetGuide.Services;

namespace FoodStreetGuide
{
    public partial class App : Application
    {
        // 🔥 Database dùng chung toàn app
        public static DatabaseService Database { get; private set; }

        public App()
        {
            InitializeComponent();

            // Khởi tạo database service
            Database = new DatabaseService();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}