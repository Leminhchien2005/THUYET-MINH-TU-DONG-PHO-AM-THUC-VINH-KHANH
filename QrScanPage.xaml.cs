using Camera.MAUI;
using System.Linq;
using Microsoft.Maui.ApplicationModel;

namespace FoodStreetGuide;

public partial class QrScanPage : ContentPage
{
    bool isScanning = false;

    public QrScanPage()
    {
        InitializeComponent();

        RequestCamera();

        cameraView.BarcodeDetected += CameraView_BarcodeDetected;
    }

    private void CameraView_BarcodeDetected(object sender, Camera.MAUI.ZXingHelper.BarcodeEventArgs e)
    {
        if (isScanning) return;
        isScanning = true;

        var result = e.Result.FirstOrDefault()?.Text;

        if (result == null)
        {
            isScanning = false;
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await cameraView.StopCameraAsync(); // Stop the camera

            await DisplayAlert("QR", result, "OK");

            // 👉 chuyển trang nếu muốn
            // await Navigation.PushAsync(new DetailPage(result));

            isScanning = false;
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        while (true)
        {
            await ScanLine.TranslateTo(0, 100, 1000);
            await ScanLine.TranslateTo(0, -100, 1000);
        }
    }

    async void RequestCamera()
    {
        var status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status == PermissionStatus.Granted)
        {
            await cameraView.StopCameraAsync();
        }
        else
        {
            await DisplayAlert("Lỗi", "Không có quyền camera", "OK");
        }
    }
}