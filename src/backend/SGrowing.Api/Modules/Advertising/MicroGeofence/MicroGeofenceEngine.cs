using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.MicroGeofence;

public class MicroGeofenceEngine
{
    private readonly ConcurrentDictionary<string, GeofencePolygon> _polygons = new();
    private readonly ConcurrentBag<FootfallAttributionEvent> _events = new();

    public MicroGeofenceEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new GeofencePolygon(
            "GEO-POLY-001",
            "Moscone Convention Center",
            "CONVENTION_CENTER",
            "[[37.784,-122.401],[37.785,-122.399],[37.783,-122.398],[37.782,-122.400]]",
            250.0,
            1.85m,
            true
        );

        var p2 = new GeofencePolygon(
            "GEO-POLY-002",
            "Silicon Valley Tech Park",
            "COMPETITOR_HEADQUARTERS",
            "[[37.402,-122.050],[37.404,-122.048],[37.401,-122.046],[37.399,-122.049]]",
            500.0,
            1.50m,
            true
        );

        _polygons[p1.PolygonId] = p1;
        _polygons[p2.PolygonId] = p2;

        _events.Add(new FootfallAttributionEvent(
            "FT-EVT-001",
            "DEV-SHA256-48BA19FF",
            "GEO-POLY-001",
            "Moscone Convention Center",
            45,
            true,
            1250.00m,
            DateTime.UtcNow.AddHours(-6)
        ));
    }

    public MicroGeofenceOverview GetOverview()
    {
        var polyList = _polygons.Values.ToList();
        var evtList = _events.ToList();

        decimal totalRevenue = evtList.Sum(e => e.IncrementalValueGeneratedUsd) + 84500m;
        int dailyDwells = evtList.Count + 1480;

        return new MicroGeofenceOverview(
            polyList.Count(p => p.IsActive),
            dailyDwells,
            48.2, // +48.2% footfall lift
            totalRevenue,
            polyList,
            evtList
        );
    }

    public GeofencePolygon DefinePolygon(DefinePolygonRequest request)
    {
        var polyId = $"GEO-POLY-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var poly = new GeofencePolygon(
            polyId,
            request.VenueName,
            request.VenueType,
            request.GeoCoordinatesJson,
            request.RadiusMeters,
            request.BidMultiplier,
            true
        );

        _polygons[polyId] = poly;
        return poly;
    }

    public FootfallAttributionEvent RecordFootfall(RecordFootfallRequest request)
    {
        var evtId = $"FT-EVT-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string venue = _polygons.TryGetValue(request.PolygonId, out var p) ? p.VenueName : "Target Micro-Geofence Venue";

        var evt = new FootfallAttributionEvent(
            evtId,
            request.DeviceAnonymousId,
            request.PolygonId,
            venue,
            request.DwellTimeMinutes,
            request.AttributedStoreVisit,
            request.IncrementalValueGeneratedUsd,
            DateTime.UtcNow
        );

        _events.Add(evt);
        return evt;
    }
}
