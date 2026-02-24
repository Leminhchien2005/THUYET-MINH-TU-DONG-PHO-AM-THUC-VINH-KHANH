using FoodStreetGuide.Services;
using Microsoft.Maui.ApplicationModel;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly LocationService _locationService = new();

    public MainPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _db.SeedDataAsync();   // đảm bảo có dữ liệu
        var list = await _db.GetAllPoiAsync();
        PoiList.ItemsSource = list;
    }

    private async void OnGetLocationClicked(object sender, EventArgs e)
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
            return;
        }

        var location = await _locationService.GetCurrentLocationAsync();

        if (location != null)
        {
            LatLabel.Text = $"Latitude: {location.Latitude}";
            LngLabel.Text = $"Longitude: {location.Longitude}";
        }
        else
        {
            await DisplayAlertAsync("Lỗi", "Không lấy được vị trí", "OK");
        }
    }
}