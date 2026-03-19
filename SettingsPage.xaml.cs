using Microsoft.Maui.Storage;
using System.Globalization;

namespace FoodStreetGuide
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();

            // Load dữ liệu đã lưu
            volumeSlider.Value = Preferences.Get("volume", 1.0);

            var lang = Preferences.Get("lang", "vi");
            languagePicker.SelectedItem = lang;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Lưu volume
            Preferences.Set("volume", volumeSlider.Value);

            if (languagePicker.SelectedItem != null)
            {
                string lang = languagePicker.SelectedItem.ToString();

                // Lưu ngôn ngữ
                Preferences.Set("lang", lang);

                // Đổi ngôn ngữ ngay
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);

                // Reload UI
                App.Current.MainPage = new AppShell();
            }

            await DisplayAlert("Thông báo", "Đã lưu cài đặt", "OK");
        }
    }
}