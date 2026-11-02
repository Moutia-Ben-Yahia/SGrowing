namespace SGrowing.Api.Modules.Logistics.MaritimeEcoRouting;

/// <summary>
/// Autonomous maritime AIS vessel collision avoidance and bunker fuel eco-routing engine.
/// Evaluates IMO 2023 Carbon Intensity Indicator (CII) and COLREGS CPA/TCPA collision vectors.
/// </summary>
public sealed class MaritimeEcoRoutingEngine
{
    private const double NauticalMileToKm = 1.852;
    private const double EarthRadiusKm = 6371.0;
    private const double VlsfoCo2EmissionFactor = 3.114; // grams CO2 per gram VLSFO fuel
    private const double CriticalCpaDistanceNm = 1.5;     // 1.5 Nautical Miles safety bubble

    public EcoVoyagePlan ComputeEcoRouteAndCollisionRisks(EcoRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var own = request.OwnVessel;
        var dest = request.Destination;

        // 1. Calculate Great-Circle Distance
        var distanceNm = CalculateGreatCircleDistanceNm(own.Position, dest);

        // 2. Slow-Steaming Eco Speed Calculation (Cubic Fuel Law)
        var baseSpeed = own.SpeedKnots > 5.0 ? own.SpeedKnots : 18.0;
        var ecoSpeed = Math.Max(12.0, baseSpeed * 0.88); // 12% speed reduction saves ~27% fuel

        // Admiralty Formula: Daily Fuel (metric tons) = k * DWT^(2/3) * V^3
        var dwt = Math.Max(5000.0, own.DeadweightTons);
        var baseDailyTons = 0.00000085 * Math.Pow(dwt, 2.0 / 3.0) * Math.Pow(ecoSpeed, 3.0);

        // Weather resistance penalty (waves + currents)
        var wavePenalty = 1.0 + (0.04 * request.Weather.SignificantWaveHeightMeters);
        var effectiveDailyFuelTons = baseDailyTons * wavePenalty;

        var voyageHours = distanceNm / ecoSpeed;
        var voyageDays = voyageHours / 24.0;
        var totalFuelTons = voyageDays * effectiveDailyFuelTons;
        var fuelCostUsd = (decimal)totalFuelTons * request.VlsfoFuelPricePerTonUsd;

        // IMO Carbon Intensity Indicator (CII)
        var totalCo2Grams = totalFuelTons * 1_000_000.0 * VlsfoCo2EmissionFactor;
        var transportWork = dwt * distanceNm;
        var ciiScore = transportWork > 0 ? totalCo2Grams / transportWork : 0.0;
        var ciiGrade = DetermineCiiGrade(ciiScore);

        // 3. AIS Closest Point of Approach (CPA / TCPA) Collision Evaluation
        var collisionAssessments = new List<CollisionAvoidanceAssessment>();

        foreach (var target in request.NearbyAisTargets)
        {
            var assessment = EvaluateCollisionRisk(own, target);
            collisionAssessments.Add(assessment);
        }

        return new EcoVoyagePlan(
            PlanId: $"VOY-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            OwnVesselMmsi: own.Mmsi,
            RecommendedSpeedKnots: Math.Round(ecoSpeed, 1),
            DistanceNauticalMiles: Math.Round(distanceNm, 1),
            EstimatedFuelBurnMetricTons: Math.Round(totalFuelTons, 1),
            EstimatedFuelCostUsd: fuelCostUsd,
            CiiScoreGramsCo2PerTonNm: Math.Round(ciiScore, 2),
            CiiRatingGrade: ciiGrade,
            CollisionRisks: collisionAssessments,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    private static CollisionAvoidanceAssessment EvaluateCollisionRisk(AisVesselTelemetry own, AisVesselTelemetry target)
    {
        // Own vessel velocity vector (knots)
        var ownHeadingRad = own.CourseOverGroundDegrees * Math.PI / 180.0;
        var vxOwn = own.SpeedKnots * Math.Sin(ownHeadingRad);
        var vyOwn = own.SpeedKnots * Math.Cos(ownHeadingRad);

        // Target vessel velocity vector (knots)
        var targetHeadingRad = target.CourseOverGroundDegrees * Math.PI / 180.0;
        var vxTarget = target.SpeedKnots * Math.Sin(targetHeadingRad);
        var vyTarget = target.SpeedKnots * Math.Cos(targetHeadingRad);

        // Relative velocity
        var vrx = vxTarget - vxOwn;
        var vry = vyTarget - vyOwn;
        var vRel = Math.Sqrt((vrx * vrx) + (vry * vry));

        // Relative distance in nautical miles
        var midLat = (own.Position.Latitude + target.Position.Latitude) / 2.0;
        var dxNm = (target.Position.Longitude - own.Position.Longitude) * 60.0 * Math.Cos(midLat * Math.PI / 180.0);
        var dyNm = (target.Position.Latitude - own.Position.Latitude) * 60.0;

        double cpaNm;
        double tcpaMinutes;

        if (vRel < 0.1)
        {
            cpaNm = Math.Sqrt((dxNm * dxNm) + (dyNm * dyNm));
            tcpaMinutes = 0.0;
        }
        else
        {
            tcpaMinutes = -((dxNm * vrx) + (dyNm * vry)) / (vRel * vRel) * 60.0;
            cpaNm = Math.Abs((dxNm * vry) - (dyNm * vrx)) / vRel;
        }

        var collisionRisk = tcpaMinutes > 0 && tcpaMinutes <= 45.0 && cpaNm <= CriticalCpaDistanceNm;
        var courseAdjustment = collisionRisk ? 25.0 : 0.0; // COLREGS: alter course to starboard

        var advisory = collisionRisk
            ? $"COLREGS RULE 16: Immediate give-way action required. TCPA: {tcpaMinutes:F1}m, CPA: {cpaNm:F2}NM. Alter course +{courseAdjustment}° Starboard."
            : $"CLEAR AIRSPACE/WATERWAY: CPA {cpaNm:F2}NM exceeds safety margin.";

        return new CollisionAvoidanceAssessment(
            TargetMmsi: target.Mmsi,
            TargetName: target.VesselName,
            DistanceCpaNauticalMiles: Math.Round(cpaNm, 2),
            TimeCpaMinutes: Math.Round(tcpaMinutes, 1),
            CollisionRiskDetected: collisionRisk,
            RecommendedCourseAdjustmentDegrees: courseAdjustment,
            ColregsAdvisory: advisory
        );
    }

    private static double CalculateGreatCircleDistanceNm(GeoCoordinate a, GeoCoordinate b)
    {
        var dLat = (b.Latitude - a.Latitude) * Math.PI / 180.0;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180.0;
        var lat1 = a.Latitude * Math.PI / 180.0;
        var lat2 = b.Latitude * Math.PI / 180.0;

        var h = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1.0 - h));
        var distanceKm = EarthRadiusKm * c;

        return distanceKm / NauticalMileToKm;
    }

    private static string DetermineCiiGrade(double score)
    {
        return score switch
        {
            < 4.5 => "A",
            < 6.0 => "B",
            < 8.0 => "C",
            < 10.5 => "D",
            _ => "E"
        };
    }
}
