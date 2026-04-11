using Microsoft.Maui.Devices.Sensors;

public static class DistanceHelper
{
    public static double CalculateDistanceKm(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        return Location.CalculateDistance(
            new Location(lat1, lon1),
            new Location(lat2, lon2),
            DistanceUnits.Kilometers
        );
    }
}