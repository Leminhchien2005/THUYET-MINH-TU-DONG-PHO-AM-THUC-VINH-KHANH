using Microsoft.Maui.Devices.Sensors;

namespace FoodStreetGuide.Services
{
    public class LocationService
    {
        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                var location = await Geolocation.Default.GetLastKnownLocationAsync();

                if (location == null)
                {
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.Medium,
                        TimeSpan.FromSeconds(10));

                    location = await Geolocation.Default.GetLocationAsync(request);
                }

                return location;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}