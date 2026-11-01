namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Service analyzing driver telematics kinetics, identifying harsh kinematic events,
/// quantifying idle fuel wastage, and evaluating green courier bonus eligibility.
/// </summary>
public sealed class DriverSafetyAndEcoScoringService : IDriverSafetyAndEcoScoringService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DriverEcoScore> _latestScores = [];

    // Weight coefficients for EcoScore deduction
    private const double WeightHarshAccelEco = 3.0;
    private const double WeightHarshBrakeEco = 2.0;
    private const double WeightSharpTurnEco = 2.0;
    private const double WeightIdleFuelEco = 5.0; // Per liter of wasted fuel

    // Weight coefficients for SafetyScore deduction
    private const double WeightHarshBrakeSafety = 4.0;
    private const double WeightHarshAccelSafety = 3.0;
    private const double WeightSharpTurnSafety = 4.0;

    // Thresholds
    private const double HarshAccelSpeedRateKmhPerSec = 15.0;
    private const double HarshAccelMaxRpm = 4500.0;
    private const double HarshBrakeSpeedRateKmhPerSec = -18.0;
    private const double SharpTurnMinSpeedKmh = 40.0;
    private const double SharpTurnMinLateralAccelMs2 = 3.0;
    private const double SharpTurnMinHeadingRateDegPerSec = 25.0;
    private const double ExcessiveIdlingThresholdSeconds = 300.0; // 5 consecutive minutes
    private const double NominalIdleFuelRateLitersPerHour = 1.20;

    /// <inheritdoc />
    public DriverEcoScore EvaluateDriverSession(string driverId, IEnumerable<Obd2TelemetryPacket> telemetryWindow)
    {
        ArgumentNullException.ThrowIfNull(telemetryWindow);

        if (string.IsNullOrWhiteSpace(driverId))
        {
            throw new ArgumentException("Driver ID cannot be null or whitespace.", nameof(driverId));
        }

        var packets = telemetryWindow.OrderBy(p => p.TimestampUtc).ToList();
        var now = DateTime.UtcNow;

        if (packets.Count == 0)
        {
            var emptyScore = new DriverEcoScore(
                DriverId: driverId,
                PeriodStartUtc: now,
                PeriodEndUtc: now,
                TotalDistanceKm: 0.0,
                EcoScore: 100.0,
                SafetyScore: 100.0,
                HarshAccelerationsCount: 0,
                HarshBrakingsCount: 0,
                SharpTurnsCount: 0,
                IdlingDurationMinutes: 0.0,
                FuelWastedIdlingLiters: 0.0,
                FuelBonusEligible: true);

            lock (_lock)
            {
                _latestScores[driverId] = emptyScore;
            }

            return emptyScore;
        }

        var periodStart = packets[0].TimestampUtc;
        var periodEnd = packets[^1].TimestampUtc;

        var harshAccelerations = 0;
        var harshBrakings = 0;
        var sharpTurns = 0;
        var totalDistanceKm = 0.0;

        // Kinematic transitions analysis
        double? previousBearing = null;
        bool inHarshAccel = false;
        bool inHarshBrake = false;

        for (var i = 0; i < packets.Count - 1; i++)
        {
            var p1 = packets[i];
            var p2 = packets[i + 1];

            var dtSeconds = (p2.TimestampUtc - p1.TimestampUtc).TotalSeconds;
            if (dtSeconds <= 0)
            {
                continue;
            }

            // Distance accumulation via trapezoidal integration of speed
            var avgSpeedKmh = (p1.SpeedKmh + p2.SpeedKmh) / 2.0;
            totalDistanceKm += avgSpeedKmh * (dtSeconds / 3600.0);

            // Kinematic acceleration rate in km/h per second
            var speedDelta = p2.SpeedKmh - p1.SpeedKmh;
            var accelRate = speedDelta / dtSeconds;

            // 1. Harsh Acceleration: Speed delta > +15 km/h per second or RPM > 4500
            if (accelRate > HarshAccelSpeedRateKmhPerSec || p2.EngineRpm > HarshAccelMaxRpm)
            {
                if (!inHarshAccel)
                {
                    harshAccelerations++;
                    inHarshAccel = true;
                }
            }
            else
            {
                inHarshAccel = false;
            }

            // 2. Harsh Braking: Speed delta < -18 km/h per second
            if (accelRate < HarshBrakeSpeedRateKmhPerSec)
            {
                if (!inHarshBrake)
                {
                    harshBrakings++;
                    inHarshBrake = true;
                }
            }
            else
            {
                inHarshBrake = false;
            }

            // 3. Sharp Turn: Lateral acceleration / sudden heading shift at speed > 40 km/h
            if (avgSpeedKmh > SharpTurnMinSpeedKmh)
            {
                // Check if GPS coordinates are available and changing
                var hasGps = Math.Abs(p1.Latitude) > 0.0001 && Math.Abs(p2.Latitude) > 0.0001;
                if (hasGps)
                {
                    var distMeters = CalculateDistanceMeters(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);
                    if (distMeters >= 1.0)
                    {
                        var currentBearing = CalculateBearing(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);
                        if (previousBearing.HasValue)
                        {
                            var headingChange = Math.Abs(NormalizeAngleDifference(currentBearing, previousBearing.Value));
                            var turnRateDegPerSec = headingChange / dtSeconds;
                            var speedMs = avgSpeedKmh / 3.6;
                            var lateralAccelMs2 = speedMs * (turnRateDegPerSec * (Math.PI / 180.0));

                            if (lateralAccelMs2 >= SharpTurnMinLateralAccelMs2 || turnRateDegPerSec >= SharpTurnMinHeadingRateDegPerSec)
                            {
                                sharpTurns++;
                            }
                        }

                        previousBearing = currentBearing;
                    }
                }
            }
        }

        // 4. Excessive Idling Analysis (> 5 consecutive minutes with speed == 0 and engine running)
        var totalExcessiveIdleSeconds = 0.0;
        var totalExcessiveIdleFuelWastedLiters = 0.0;

        var currentIdleStreakSeconds = 0.0;
        var currentIdleStreakFuelLiters = 0.0;

        for (var i = 0; i < packets.Count; i++)
        {
            var p = packets[i];
            var isEngineRunning = p.EngineRpm > 300.0 || p.InstantaneousFuelRateLitersPerHour > 0.1;
            var isStopped = p.SpeedKmh < 1.0;

            if (i > 0)
            {
                var prev = packets[i - 1];
                var prevEngineRunning = prev.EngineRpm > 300.0 || prev.InstantaneousFuelRateLitersPerHour > 0.1;
                var prevStopped = prev.SpeedKmh < 1.0;

                var dtSeconds = (p.TimestampUtc - prev.TimestampUtc).TotalSeconds;
                if (dtSeconds > 0 && dtSeconds <= 1800) // Within 30 minutes continuous gap
                {
                    if (isEngineRunning && isStopped && prevEngineRunning && prevStopped)
                    {
                        currentIdleStreakSeconds += dtSeconds;
                        var fuelRate = p.InstantaneousFuelRateLitersPerHour > 0.0
                            ? p.InstantaneousFuelRateLitersPerHour
                            : NominalIdleFuelRateLitersPerHour;
                        currentIdleStreakFuelLiters += fuelRate * (dtSeconds / 3600.0);
                    }
                    else
                    {
                        // Streak ended, evaluate if it exceeded 5 minutes (300 seconds)
                        if (currentIdleStreakSeconds > ExcessiveIdlingThresholdSeconds)
                        {
                            totalExcessiveIdleSeconds += currentIdleStreakSeconds;
                            totalExcessiveIdleFuelWastedLiters += currentIdleStreakFuelLiters;
                        }

                        currentIdleStreakSeconds = 0.0;
                        currentIdleStreakFuelLiters = 0.0;
                    }
                }
            }
        }

        // Flush any trailing idle streak
        if (currentIdleStreakSeconds > ExcessiveIdlingThresholdSeconds)
        {
            totalExcessiveIdleSeconds += currentIdleStreakSeconds;
            totalExcessiveIdleFuelWastedLiters += currentIdleStreakFuelLiters;
        }

        var idlingDurationMinutes = Math.Round(totalExcessiveIdleSeconds / 60.0, 2);
        var fuelWastedLiters = Math.Round(totalExcessiveIdleFuelWastedLiters, 3);
        totalDistanceKm = Math.Round(totalDistanceKm, 2);

        // EcoScore calculation
        // EcoScore = 100 - (w_accel * HardAccels) - (w_brake * HarshBrakes) - (w_turn * SharpTurns) - (w_idle * IdleFuelWaste)
        var rawEcoScore = 100.0
            - (WeightHarshAccelEco * harshAccelerations)
            - (WeightHarshBrakeEco * harshBrakings)
            - (WeightSharpTurnEco * sharpTurns)
            - (WeightIdleFuelEco * fuelWastedLiters);

        var finalEcoScore = Math.Round(Math.Clamp(rawEcoScore, 0.0, 100.0), 1);

        // SafetyScore calculation
        // SafetyScore = 100 - (s_brake * HarshBrakes) - (s_accel * HardAccels) - (s_turn * SharpTurns)
        var rawSafetyScore = 100.0
            - (WeightHarshBrakeSafety * harshBrakings)
            - (WeightHarshAccelSafety * harshAccelerations)
            - (WeightSharpTurnSafety * sharpTurns);

        var finalSafetyScore = Math.Round(Math.Clamp(rawSafetyScore, 0.0, 100.0), 1);

        // Green courier bonus eligibility gating: EcoScore >= 85 and SafetyScore >= 90
        var fuelBonusEligible = finalEcoScore >= 85.0 && finalSafetyScore >= 90.0;

        var result = new DriverEcoScore(
            DriverId: driverId,
            PeriodStartUtc: periodStart,
            PeriodEndUtc: periodEnd,
            TotalDistanceKm: totalDistanceKm,
            EcoScore: finalEcoScore,
            SafetyScore: finalSafetyScore,
            HarshAccelerationsCount: harshAccelerations,
            HarshBrakingsCount: harshBrakings,
            SharpTurnsCount: sharpTurns,
            IdlingDurationMinutes: idlingDurationMinutes,
            FuelWastedIdlingLiters: fuelWastedLiters,
            FuelBonusEligible: fuelBonusEligible);

        lock (_lock)
        {
            _latestScores[driverId] = result;
        }

        return result;
    }

    /// <inheritdoc />
    public DriverEcoScore? GetDriverScore(string driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return null;
        }

        lock (_lock)
        {
            return _latestScores.GetValueOrDefault(driverId);
        }
    }

    private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var rLat1 = lat1 * Math.PI / 180.0;
        var rLat2 = lat2 * Math.PI / 180.0;

        var y = Math.Sin(dLon) * Math.Cos(rLat2);
        var x = Math.Cos(rLat1) * Math.Sin(rLat2) -
                Math.Sin(rLat1) * Math.Cos(rLat2) * Math.Cos(dLon);

        var brng = Math.Atan2(y, x) * 180.0 / Math.PI;
        return (brng + 360.0) % 360.0;
    }

    private static double NormalizeAngleDifference(double angle1, double angle2)
    {
        var diff = (angle1 - angle2 + 540.0) % 360.0 - 180.0;
        return diff;
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadius * c;
    }
}
