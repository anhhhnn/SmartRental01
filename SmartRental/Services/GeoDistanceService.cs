namespace SmartRental.Services;

public class GeoDistanceService
{
    private const double EarthRadiusKm = 6371d;

    public double CalculateDistanceKm(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        var latitudeDelta = DegreesToRadians(lat2 - lat1);
        var longitudeDelta = DegreesToRadians(lon2 - lon1);
        var latitude1 = DegreesToRadians(lat1);
        var latitude2 = DegreesToRadians(lat2);

        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(latitude1) * Math.Cos(latitude2)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        // Floating-point rounding can make the value microscopically greater than 1
        // for antipodal points, which would otherwise make Asin return NaN.
        var clampedHaversine = Math.Clamp(haversine, 0d, 1d);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(clampedHaversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
