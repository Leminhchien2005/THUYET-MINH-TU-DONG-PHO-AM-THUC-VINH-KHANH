namespace FoodStreetGuide
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Đăng ký route cho SettingsPage
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

            Routing.RegisterRoute(nameof(QrScanPage), typeof(QrScanPage));
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        private async void QrTab_Tapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(QrScanPage));
        }
    }
}