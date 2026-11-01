namespace SGrowing.Api.Modules.Logistics.Routing;

/// <summary>
/// Supported freight transport modalities across Algeria and North Africa.
/// </summary>
public enum TransportMode
{
    RoadFreight = 1,
    RailFreight = 2,
    CoastalMaritime = 3,
    AirCargo = 4
}

/// <summary>
/// Optimization objective for multi-modal path routing.
/// </summary>
public enum OptimizationGoal
{
    FastestTime = 1,
    LowestCost = 2,
    LowestCarbon = 3,
    Balanced = 4
}

/// <summary>
/// Specific regional weather hazard classifications.
/// </summary>
public enum WeatherHazardType
{
    SiroccoSandstorm = 1,     // Desert sandstorm severely reducing visibility and road traction
    FlashFlood = 2,           // Oued flash flood overflowing bridges / bypasses
    ExtremeHeatwave = 3,      // Extreme ambient temps (> 45°C) inducing tire blowout and battery degradation
    HighAtlasSnow = 4,        // Mountain pass snow and black ice (e.g., Chiffa, Djurdjura)
    HeavyCoastalFog = 5       // Dense marine fog impeding port crane / coastal navigation
}

/// <summary>
/// Operational severity of weather hazard.
/// </summary>
public enum HazardSeverity
{
    Advisory = 1,
    Warning = 2,
    CriticalBlocked = 3
}

/// <summary>
/// Geographic coordinate with optional elevation.
/// </summary>
public sealed record GeoCoordinate(double Latitude, double Longitude, double ElevationMeters = 0.0)
{
    /// <summary>
    /// Calculates the great-circle Haversine distance in kilometers.
    /// </summary>
    public double DistanceToKm(GeoCoordinate target)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(target.Latitude - Latitude);
        var dLon = DegreesToRadians(target.Longitude - Longitude);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(DegreesToRadians(Latitude)) * Math.Cos(DegreesToRadians(target.Latitude)) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Node in the multi-modal logistics transport graph.
/// </summary>
public sealed record RoutingNode(
    string NodeId,
    string Name,
    GeoCoordinate Location,
    bool IsTransshipmentHub,
    IReadOnlyList<TransportMode> SupportedModes
);

/// <summary>
/// Edge connecting two nodes with modality, physical distance, speed, gradient, and cost metrics.
/// </summary>
public sealed record RoutingEdge(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    TransportMode Mode,
    double DistanceKm,
    double BaseSpeedKmh,
    decimal CostPerKmDzd,
    double GradientPercent,
    double CarbonGramsPerTonKm
);

/// <summary>
/// Active meteorological or environmental hazard alert affecting transit corridors.
/// </summary>
public sealed record WeatherHazardAlert(
    string HazardId,
    WeatherHazardType HazardType,
    HazardSeverity Severity,
    GeoCoordinate AffectedCenter,
    double RadiusKm,
    double SpeedReductionFactor, // 0.0 = completely blocked, 0.5 = 50% speed limit
    DateTime ReportedAtUtc,
    DateTime ExpiresAtUtc,
    string CorridorName,
    TransportMode? ApplicableMode = null
)
{
    public bool IsExpired(DateTime nowUtc) => nowUtc > ExpiresAtUtc;

    public bool AffectsLocation(GeoCoordinate point) => AffectedCenter.DistanceToKm(point) <= RadiusKm;

    public bool AffectsMode(TransportMode mode) => ApplicableMode == null || ApplicableMode == mode;
}

/// <summary>
/// Physical parameters and energy state of an electric freight vehicle (EV).
/// </summary>
public sealed record EvFleetEnergyProfile(
    string VehicleId,
    double BatteryCapacityKwh,
    double CurrentSoCKwh,
    double EmptyWeightKg,
    double PayloadWeightKg,
    double RegenerativeEfficiencyRatio = 0.65,
    double AuxiliaryPowerKw = 2.5,
    double AmbientTemperatureCelsius = 25.0
)
{
    public double TotalMassKg => EmptyWeightKg + PayloadWeightKg;
    public double SoCPercent => BatteryCapacityKwh > 0 ? (CurrentSoCKwh / BatteryCapacityKwh) * 100.0 : 0.0;
}

/// <summary>
/// Discrete leg traversed along an optimized route.
/// </summary>
public sealed record RouteLeg(
    string FromNodeId,
    string ToNodeId,
    TransportMode Mode,
    double DistanceKm,
    double DurationHours,
    decimal CostDzd,
    double CarbonKg,
    bool IsWeatherImpaired,
    string? AppliedHazardId
);

/// <summary>
/// Modal transfer at an inter-modal transshipment terminal.
/// </summary>
public sealed record TransshipmentTransfer(
    string HubNodeId,
    TransportMode FromMode,
    TransportMode ToMode,
    double DwellTimeHours,
    decimal HandlingCostDzd
);

/// <summary>
/// Complete optimized multi-modal routing plan.
/// </summary>
public sealed record MultiModalRoutePlan(
    string RouteId,
    string OriginNodeId,
    string DestinationNodeId,
    OptimizationGoal Goal,
    double TotalDistanceKm,
    double TotalDurationHours,
    decimal TotalCostDzd,
    double TotalCarbonKg,
    IReadOnlyList<RouteLeg> Legs,
    IReadOnlyList<TransshipmentTransfer> Transfers,
    bool WeatherReroutingApplied
);

/// <summary>
/// Fast-charging stop scheduled along an EV freight route.
/// </summary>
public sealed record EvChargingStop(
    string HubNodeId,
    double ArrivalSoCPercent,
    double TargetSoCPercent,
    double EnergyChargedKwh,
    double ChargingTimeMinutes,
    decimal ChargingCostDzd
);

/// <summary>
/// Full battery energy dispatch and consumption profile for EV freight corridor.
/// </summary>
public sealed record EvEnergyRoutePlan(
    string RouteId,
    string VehicleId,
    double StartingSoCPercent,
    double EndingSoCPercent,
    double TotalEnergyConsumedKwh,
    double TotalEnergyRegeneratedKwh,
    double NetEnergyKwh,
    IReadOnlyList<EvChargingStop> RequiredChargingStops,
    bool RangeDepletedRisk
);

/// <summary>
/// Route calculation request payload.
/// </summary>
public sealed record CalculateRouteRequest(
    string OriginNodeId,
    string DestinationNodeId,
    OptimizationGoal Goal = OptimizationGoal.Balanced,
    double CargoWeightTons = 10.0,
    bool AvoidWeatherHazards = true
);

/// <summary>
/// EV fleet energy routing request payload.
/// </summary>
public sealed record CalculateEvEnergyRouteRequest(
    MultiModalRoutePlan BaseRoute,
    EvFleetEnergyProfile VehicleProfile,
    double MinimumReserveSoCPercent = 15.0
);
