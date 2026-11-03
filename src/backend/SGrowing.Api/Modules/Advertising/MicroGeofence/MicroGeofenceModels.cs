namespace SGrowing.Api.Modules.Advertising.MicroGeofence;

public record GeofencePolygon(
    string PolygonId,
    string VenueName,
    string VenueType, // CONVENTION_CENTER, COMPETITOR_HEADQUARTERS, TRANSIT_HUB, FLAGSHIP_RETAIL
    string GeoCoordinatesJson, // e.g. "[[37.784,-122.401],[37.785,-122.399],...]"
    double RadiusMeters,
    decimal BidMultiplier,
    bool IsActive
);

public record FootfallAttributionEvent(
    string EventId,
    string DeviceAnonymousId,
    string PolygonId,
    string VenueName,
    int DwellTimeMinutes,
    bool AttributedStoreVisit,
    decimal IncrementalValueGeneratedUsd,
    DateTime Timestamp
);

public record MicroGeofenceOverview(
    int ActivePolygonsCount,
    int DailyAudienceDwellDetections,
    double FootfallConversionLiftPct, // e.g. +48.2%
    decimal TotalAttributedRevenueUsd,
    List<GeofencePolygon> Polygons,
    List<FootfallAttributionEvent> RecentEvents
);

public record DefinePolygonRequest(
    string VenueName,
    string VenueType,
    string GeoCoordinatesJson,
    double RadiusMeters,
    decimal BidMultiplier
);

public record RecordFootfallRequest(
    string DeviceAnonymousId,
    string PolygonId,
    int DwellTimeMinutes,
    bool AttributedStoreVisit,
    decimal IncrementalValueGeneratedUsd
);
