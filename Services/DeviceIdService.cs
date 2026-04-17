namespace FoodStreetGuide.Services;

public static class DeviceIdService
{
    private const string DeviceIdKey = "device_id";

    // Tạo deviceId ở lần chạy đầu tiên, các lần sau đọc lại từ Preferences.
    public static string GetOrCreateDeviceId()
    {
        var deviceId = Preferences.Get(DeviceIdKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(deviceId))
            return deviceId;

        deviceId = Guid.NewGuid().ToString("N");
        Preferences.Set(DeviceIdKey, deviceId);

        return deviceId;
    }
}
