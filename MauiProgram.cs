using Camera.MAUI;
using System.Linq;
using Microsoft.Extensions.Logging;
using FoodStreetGuide.Services;
using Microsoft.Maui.Controls.Maps;
using Plugin.Maui.Audio;

namespace FoodStreetGuide
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .UseMauiCameraView()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Database
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<DevicePresenceService>();
            builder.Services.AddSingleton<MainPage>();

            // AUDIO
            builder.Services.AddSingleton(AudioManager.Current);

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}