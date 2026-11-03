namespace SGrowing.Api.Modules.IoT.PredictiveTelemetry;

/// <summary>
/// Weibull fatigue degradation modeling estimating Remaining Useful Life (RUL)
/// in running hours based on cumulative stress cycles and thermal-vibration loading.
/// </summary>
public sealed class MachineryRemainingUsefulLifeEstimator
{
    // Typical industrial bearing Weibull parameters (wear-out phase beta > 1.0)
    public const double DefaultBetaShape = 2.4;
    public const double DefaultEtaScaleHours = 10000.0; // 10,000 running hours baseline

    public MachineryRulEstimate EstimateRul(
        string machineId,
        double accumulatedOperatingHours,
        double meanVibrationRms,
        double meanBearingTempCelsius,
        double beta = DefaultBetaShape,
        double eta = DefaultEtaScaleHours)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);
        if (accumulatedOperatingHours < 0) throw new ArgumentOutOfRangeException(nameof(accumulatedOperatingHours));

        // Acceleration factor based on Arrhenius thermal model and vibration stress
        // Temperature stress: for every 10C above 70C baseline, fatigue doubles
        var thermalDelta = Math.Max(0.0, meanBearingTempCelsius - 70.0);
        var thermalAcceleration = Math.Pow(2.0, thermalDelta / 10.0);

        // Vibration stress: baseline 2.5 mm/s. Accelerated by (Vib / 2.5)^2
        var vibAcceleration = Math.Max(1.0, Math.Pow(meanVibrationRms / 2.5, 2.0));

        var combinedStressFactor = thermalAcceleration * vibAcceleration;
        var adjustedEta = eta / combinedStressFactor;

        // Effective age in hours
        var effectiveAge = accumulatedOperatingHours * combinedStressFactor;

        // Mean Remaining Useful Life: E[T | T > t]
        var remainingHours = Math.Max(0.0, (adjustedEta - effectiveAge));

        // Conditional failure probability: F(t + h | t) = 1 - exp( (t/eta)^beta - ((t+h)/eta)^beta )
        var t0 = effectiveAge;
        var h48 = 48.0 * combinedStressFactor;
        var h168 = 168.0 * combinedStressFactor; // 7 days

        var p48 = ConditionalFailureProb(t0, h48, adjustedEta, beta);
        var p168 = ConditionalFailureProb(t0, h168, adjustedEta, beta);

        var action = remainingHours switch
        {
            < 72.0 => "CRITICAL: Immediate bearing replacement before Catastrophic Seizure.",
            < 300.0 => "WARNING: Schedule maintenance within next scheduled weekend window.",
            < 1000.0 => "PLANNED: Order replacement parts and verify lubricant viscosity.",
            _ => "NOMINAL: Machinery operating within design fatigue endurance."
        };

        return new MachineryRulEstimate(
            MachineId: machineId,
            EstimatedRulHours: Math.Round(remainingHours, 1),
            CumulativeStressCycles: Math.Round(effectiveAge, 1),
            FailureProbabilityNext48Hours: Math.Round(p48, 4),
            FailureProbabilityNext168Hours: Math.Round(p168, 4),
            WeibullBetaShape: beta,
            WeibullEtaScaleHours: Math.Round(adjustedEta, 1),
            RecommendedAction: action,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    private static double ConditionalFailureProb(double t, double deltaH, double eta, double beta)
    {
        if (eta <= 0.0) return 1.0;
        var term1 = Math.Pow(t / eta, beta);
        var term2 = Math.Pow((t + deltaH) / eta, beta);
        var exponent = term1 - term2;
        var survivalGivenT = Math.Exp(exponent);
        return Math.Clamp(1.0 - survivalGivenT, 0.0, 1.0);
    }
}
