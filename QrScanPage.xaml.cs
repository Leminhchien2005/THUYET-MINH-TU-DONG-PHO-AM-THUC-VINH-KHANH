using Camera.MAUI;
using Camera.MAUI.ZXingHelper;
using FoodStreetGuide.Services;
using Microsoft.Maui.ApplicationModel;
using System.Linq;
using Camera.MAUI.ZXing;

namespace FoodStreetGuide;

public partial class QrScanPage : ContentPage
{
    bool isScanning = false;
    bool _isCameraStarted;
    bool _isCameraInitializing;
    bool _isAnimating;

    public QrScanPage()
    {
        InitializeComponent();

        cameraView.BarCodeDecoder = new ZXingBarcodeDecoder();
        cameraView.BarCodeDetectionFrameRate = 10;
        cameraView.BarCodeDetectionMaxThreads = 5;
        cameraView.ControlBarcodeResultDuplicate = false;

        cameraView.CamerasLoaded += CameraView_CamerasLoaded;
        cameraView.BarcodeDetected += CameraView_BarcodeDetected;

        // ✅ Bật scan
        cameraView.BarCodeDetectionEnabled = true;
    }

    private async void CameraView_CamerasLoaded(object sender, EventArgs e)
    {
        await EnsureCameraStartedAsync();
    }

    private async void CameraView_BarcodeDetected(object sender, BarcodeEventArgs e)
    {
        if (isScanning) return;
        isScanning = true;

        var result = e.Result.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(result))
        {
            isScanning = false;
            return;
        }

        if (MainThread.IsMainThread)
        {
            await HandleBarcodeAsync(result);
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() => HandleBarcodeAsync(result));
        }
    }

    private async Task HandleBarcodeAsync(string result)
    {
        var shouldRestartCamera = true;

        try
        {
            await cameraView.StopCameraAsync();

            var api = new ApiService();
            var poiId = TryExtractPoiId(result) ?? await api.RedeemQrAsync(result);

            if (poiId != null)
            {
                shouldRestartCamera = false;
                await Shell.Current.GoToAsync($"..?poiId={poiId}");
                return;
            }

            await DisplayAlert("QR", "QR không hợp lệ / đã dùng / hết hạn", "OK");
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể xử lý QR. Vui lòng thử lại.", "OK");
        }
        finally
        {
            isScanning = false;

            if (shouldRestartCamera)
            {
                await Task.Delay(300);
                await cameraView.StartCameraAsync();
                _isCameraStarted = true;
            }
            else
            {
                _isCameraStarted = false;
            }
        }
    }

    private static int? TryExtractPoiId(string rawQrText)
    {
        if (string.IsNullOrWhiteSpace(rawQrText))
            return null;

        var input = rawQrText.Trim();

        if (int.TryParse(input, out var directId))
            return directId;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("restaurant", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(segments[i + 1], out var id))
            {
                return id;
            }
        }

        if (uri.Host.Equals("restaurant", StringComparison.OrdinalIgnoreCase)
            && segments.Length > 0
            && int.TryParse(segments[^1], out var deepLinkId))
        {
            return deepLinkId;
        }

        return null;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        isScanning = false;

        _ = EnsureCameraStartedAsync();

        _isAnimating = true;
        StartAnimation();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        _isAnimating = false;

        if (_isCameraStarted)
        {
            await cameraView.StopCameraAsync();
            _isCameraStarted = false;
        }
    }

    private async void StartAnimation()
    {
        while (_isAnimating)
        {
            await ScanLine.TranslateTo(0, 120, 1000);
            await ScanLine.TranslateTo(0, -120, 1000);
        }
    }

    private async Task EnsureCameraStartedAsync()
    {
        if (_isCameraInitializing || _isCameraStarted)
            return;

        _isCameraInitializing = true;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Lỗi", "Không có quyền camera", "OK");
                return;
            }

            int retry = 0;
            while (cameraView.Cameras.Count == 0 && retry < 20)
            {
                retry++;
                await Task.Delay(100);
            }

            var camera = cameraView.Cameras
                .FirstOrDefault(c => c.Position == CameraPosition.Back)
                ?? cameraView.Cameras.FirstOrDefault();

            if (camera == null)
            {
                await DisplayAlert("Lỗi", "Không tìm thấy camera trên thiết bị", "OK");
                return;
            }

            cameraView.Camera = camera;
            cameraView.BarCodeDetectionEnabled = true;

            await cameraView.StartCameraAsync();
            _isCameraStarted = true;
        }
        finally
        {
            _isCameraInitializing = false;
        }
    }
}