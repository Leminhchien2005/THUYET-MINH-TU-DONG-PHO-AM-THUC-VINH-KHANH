using Camera.MAUI;
using Camera.MAUI.ZXingHelper;
using Microsoft.Maui.ApplicationModel;
using System.Linq;
using Xamarin.Google.ErrorProne.Annotations;

namespace FoodStreetGuide;

public partial class QrScanPage : ContentPage
{
    bool isScanning = false;

    public QrScanPage()
    {
        InitializeComponent();

        cameraView.CamerasLoaded += CameraView_CamerasLoaded;
        cameraView.BarcodeDetected += CameraView_BarcodeDetected;

        // ✅ Bật scan
        cameraView.BarCodeDetectionEnabled = true;
    }

    private void CameraView_CamerasLoaded(object sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 0)
        {
            var camera = cameraView.Cameras
                .Where(c => c.Position == CameraPosition.Back)
                .OrderByDescending(c => c.Name) 
                .FirstOrDefault();

            cameraView.Camera = camera;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (status == PermissionStatus.Granted)
                {
                    await cameraView.StartCameraAsync();
                    await Task.Delay(500);
                }
                else
                {
                    await DisplayAlert("Lỗi", "Không có quyền camera", "OK");
                }
            });
        }
    }

    private void CameraView_BarcodeDetected(object sender, BarcodeEventArgs e)
    {
        if (isScanning) return;
        isScanning = true;

        var result = e.Result.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(result))
        {
            isScanning = false;
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await cameraView.StopCameraAsync();

            await DisplayAlert("QR Code", result, "OK");

            // 👉 nếu là link thì mở
            if (Uri.TryCreate(result, UriKind.Absolute, out var uri))
            {
                await Launcher.OpenAsync(uri);
            }

            isScanning = false;

            // bật lại camera
            await Task.Delay(1500);
            await cameraView.StartCameraAsync();
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartAnimation();
    }

    private async void StartAnimation()
    {
        while (true)
        {
            await ScanLine.TranslateTo(0, 120, 1000);
            await ScanLine.TranslateTo(0, -120, 1000);
        }
    }
}