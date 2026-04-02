using FoodStreetGuide.Resources.Strings;
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

                // 🔥 FIX: set culture đầy đủ
                var culture = new CultureInfo(lang);

                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                // 🔥 Reload UI đúng cách
                Application.Current.MainPage = new AppShell();
            }

            // 🔥 dùng resx (không hard-code)
            await DisplayAlert(
                AppResources.Settings,
                AppResources.SavedMessage,
                "OK"
            );
        }
    }
}