using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Logistics.DroneCorridors;

/// <summary>
/// Autonomous 3D airspace geofencing manager maintaining restricted zones
/// (airports, military bases, power lines) and executing polygon collision geometry.
/// </summary>
public sealed class GeofenceAirspaceManager
{
    private readonly ConcurrentDictionary<string, GeofencePolygon> _zones = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterZone(GeofencePolygon zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        _zones[zone.ZoneId] = zone;
    }

    public bool IsSegmentIntersectingRestrictedAirspace(GeoPoint3D start, GeoPoint3D end, out string? conflictingZoneName)
    {
        conflictingZoneName = null;

        foreach (var zone in _zones.Values)
        {
            if (zone.ZoneType == GeofenceZoneType.ApprovedUrbanCorridor) continue;

            // Check altitude overlap
            var segMinAlt = Math.Min(start.AltitudeMetersAgl, end.AltitudeMetersAgl);
            var segMaxAlt = Math.Max(start.AltitudeMetersAgl, end.AltitudeMetersAgl);

            if (segMaxAlt < zone.MinAltitudeMeters || segMinAlt > zone.MaxAltitudeMeters)
            {
                continue; // Clear vertically
            }

            // Check 2D intersection with polygon
            if (IsPointInsidePolygon(start.Latitude, start.Longitude, zone.Vertices) ||
                IsPointInsidePolygon(end.Latitude, end.Longitude, zone.Vertices))
            {
                conflictingZoneName = zone.Name;
                return true;
            }

            var n = zone.Vertices.Count;
            for (var i = 0; i < n; i++)
            {
                var v1 = zone.Vertices[i];
                var v2 = zone.Vertices[(i + 1) % n];

                if (LineSegmentsIntersect(start.Latitude, start.Longitude, end.Latitude, end.Longitude,
                                           v1.Latitude, v1.Longitude, v2.Latitude, v2.Longitude))
                {
                    conflictingZoneName = zone.Name;
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsPointInsidePolygon(double lat, double lon, IReadOnlyList<GeoPoint3D> polygon)
    {
        var inside = false;
        var n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((polygon[i].Longitude > lon) != (polygon[j].Longitude > lon)) &&
                (lat < (polygon[j].Latitude - polygon[i].Latitude) * (lon - polygon[i].Longitude) /
                 (polygon[j].Longitude - polygon[i].Longitude) + polygon[i].Latitude))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static bool LineSegmentsIntersect(double p0x, double p0y, double p1x, double p1y,
                                              double p2x, double p2y, double p3x, double p3y)
    {
        var s1x = p1x - p0x;
        var s1y = p1y - p0y;
        var s2x = p3x - p2x;
        var s2y = p3y - p2y;

        var s = (-s1y * (p0x - p2x) + s1x * (p0y - p2y)) / (-s2x * s1y + s1x * s2y);
        var t = (s2x * (p0y - p2y) - s2y * (p0x - p2x)) / (-s2x * s1y + s1x * s2y);

        return s >= 0 && s <= 1 && t >= 0 && t <= 1;
    }

    public IReadOnlyList<GeofencePolygon> GetAllZones() => _zones.Values.ToList();
}
