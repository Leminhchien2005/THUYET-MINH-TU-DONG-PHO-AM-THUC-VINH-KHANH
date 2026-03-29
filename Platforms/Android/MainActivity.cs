using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace FoodStreetGuide;

[Activity(Theme = "@style/Maui.SplashTheme",
          MainLauncher = true,
          LaunchMode = LaunchMode.SingleTop,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode,
          Exported = true)]
[IntentFilter(new[] { Android.Content.Intent.ActionView },
              Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
              DataScheme = "foodstreet",
              DataHost = "restaurant")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    void HandleIntent(Intent intent)
    {
        var data = intent?.Data;

        if (data != null)
        {
            var uri = new Uri(data.ToString());

            var segments = uri.Segments;

            if (segments.Length >= 2)
            {
                var id = segments.Last().Replace("/", "");

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Microsoft.Maui.Controls.Application.Current?.MainPage is Shell shell)
                    {
                        await shell.GoToAsync($"//MainPage?poiId={id}");
                    }
                    else if (Microsoft.Maui.Controls.Application.Current != null)
                    {
                        Microsoft.Maui.Controls.Application.Current.MainPage = new AppShell();
                        await Shell.Current.GoToAsync($"//MainPage?poiId={id}");
                    }
                });
            }
        }
    }
}   