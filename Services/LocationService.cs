using Microsoft.Maui.Devices.Sensors;

namespace FoodStreetGuide.Services
{
    public class LocationService
    {
        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                var request = new GeolocationRequest(
                    GeolocationAccuracy.Best,
                    TimeSpan.FromSeconds(10));

                var location = await Geolocation.Default.GetLocationAsync(request);

                return location;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}