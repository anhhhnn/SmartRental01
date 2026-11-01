namespace SmartRental.Data;

public static class GeoCoordinateGenerator
{
    private const int RandomSeed = 20261027;
    private const double EarthRadiusKm = 6371d;

    public static GeoPoint? GenerateForArea(string? areaName, int deterministicKey)
    {
        var area = ThaiNguyenAreas.Normalize(areaName);
        if (area is null
            || !GeoAreaCenters.ByArea.TryGetValue(area, out var center)
            || center is null)
        {
            return null;
        }

        var random = new Random(unchecked(RandomSeed + deterministicKey * 7919));
        var distanceRoll = random.NextDouble();
        var distanceKm = distanceRoll switch
        {
            < 0.75 => 0.15 + random.NextDouble() * 0.55,
            < 0.97 => 0.70 + random.NextDouble() * 0.60,
            _ => 1.30 + random.NextDouble() * 0.50
        };
        var bearing = random.NextDouble() * 2 * Math.PI;
        var angularDistance = distanceKm / EarthRadiusKm;
        var latitude1 = DegreesToRadians(center.Latitude);
        var longitude1 = DegreesToRadians(center.Longitude);
        var latitude2 = Math.Asin(
            Math.Sin(latitude1) * Math.Cos(angularDistance)
            + Math.Cos(latitude1) * Math.Sin(angularDistance) * Math.Cos(bearing));
        var longitude2 = longitude1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(latitude1),
            Math.Cos(angularDistance) - Math.Sin(latitude1) * Math.Sin(latitude2));

        return new GeoPoint(RadiansToDegrees(latitude2), RadiansToDegrees(longitude2));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;
}