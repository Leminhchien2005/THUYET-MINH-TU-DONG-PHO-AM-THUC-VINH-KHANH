using FoodStreetGuide.Services;

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

        await _db.SeedDataAsync();   // đảm bảo có dữ liệu
        var list = await _db.GetAllPoiAsync();
        PoiList.ItemsSource = list;
    }
}