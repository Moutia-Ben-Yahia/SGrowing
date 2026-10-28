namespace SGrowing.Core.Spatial;

public record GeoBoundingBox(
    double MinLatitude,
    double MaxLatitude,
    double MinLongitude,
    double MaxLongitude,
    bool CrossesAntimeridian
);

public static class PostGisSpatialHelper
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>
    /// Computes an accurate bounding box envelope for spatial indexing (GiST ST_MakeEnvelope).
    /// Properly handles polar clamp and 180-degree antimeridian dateline wrap-around.
    /// </summary>
    public static GeoBoundingBox CreateBoundingBoxEnvelope(double centerLat, double centerLng, double radiusKm)
    {
        if (radiusKm <= 0) radiusKm = 1.0;

        // Angular distance in radians
        var radDist = radiusKm / EarthRadiusKm;

        var radLat = centerLat * Math.PI / 180.0;
        var radLon = centerLng * Math.PI / 180.0;

        var minLatRad = radLat - radDist;
        var maxLatRad = radLat + radDist;

        double minLat = minLatRad * 180.0 / Math.PI;
        double maxLat = maxLatRad * 180.0 / Math.PI;

        // Clamp to poles
        minLat = Math.Max(-90.0, minLat);
        maxLat = Math.Min(90.0, maxLat);

        double minLon;
        double maxLon;
        bool crossesAntimeridian = false;

        if (minLat > -90.0 && maxLat < 90.0)
        {
            var deltaLon = Math.Asin(Math.Sin(radDist) / Math.Cos(radLat));
            var minLonRad = radLon - deltaLon;
            var maxLonRad = radLon + deltaLon;

            minLon = minLonRad * 180.0 / Math.PI;
            maxLon = maxLonRad * 180.0 / Math.PI;

            if (minLon < -180.0 || maxLon > 180.0)
            {
                crossesAntimeridian = true;
                if (minLon < -180.0) minLon += 360.0;
                if (maxLon > 180.0) maxLon -= 360.0;
            }
        }
        else
        {
            // Center is too close to pole, covers all longitudes
            minLon = -180.0;
            maxLon = 180.0;
        }

        return new GeoBoundingBox(
            MinLatitude: Math.Round(minLat, 6),
            MaxLatitude: Math.Round(maxLat, 6),
            MinLongitude: Math.Round(minLon, 6),
            MaxLongitude: Math.Round(maxLon, 6),
            CrossesAntimeridian: crossesAntimeridian
        );
    }

    /// <summary>
    /// Fast in-memory bounding-box pre-filtering before exact spherical distance calculation.
    /// </summary>
    public static bool IsPointWithinBox(double lat, double lng, GeoBoundingBox box)
    {
        if (lat < box.MinLatitude || lat > box.MaxLatitude)
            return false;

        if (box.CrossesAntimeridian)
        {
            // Point is within if longitude >= MinLongitude OR longitude <= MaxLongitude
            return lng >= box.MinLongitude || lng <= box.MaxLongitude;
        }

        return lng >= box.MinLongitude && lng <= box.MaxLongitude;
    }

    /// <summary>
    /// Accurate Haversine great-circle distance between two coordinates in kilometers.
    /// </summary>
    public static double CalculateHaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(EarthRadiusKm * c, 3);
    }

    /// <summary>
    /// Generates raw SQL expression for PostGIS ST_DWithin query using GiST geography indexing.
    /// </summary>
    public static string BuildPostGisDWithinSql(string geomCol, double lat, double lng, double radiusMeters)
    {
        return $"ST_DWithin({geomCol}, ST_SetSRID(ST_MakePoint({lng:F6}, {lat:F6}), 4326)::geography, {radiusMeters:F2})";
    }

    /// <summary>
    /// Generates raw SQL expression for PostGIS ST_Distance query.
    /// </summary>
    public static string BuildPostGisDistanceSql(string geomCol, double lat, double lng)
    {
        return $"ST_Distance({geomCol}, ST_SetSRID(ST_MakePoint({lng:F6}, {lat:F6}), 4326)::geography)";
    }
}
