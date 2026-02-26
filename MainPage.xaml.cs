using FoodStreetGuide.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace FoodStreetGuide;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;

    public MainPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _db.SeedDataAsync();
        var list = await _db.GetAllPoiAsync();


        PoiList.ItemsSource = list;

        MyMap.Pins.Clear();

        foreach (var poi in list)
        {
            var pin = new Pin
            {
                Label = poi.Name ?? "",
                Address = poi.Description ?? "",
                Location = new Location(poi.Latitude, poi.Longitude)
            };

            MyMap.Pins.Add(pin);
        }
    }
}