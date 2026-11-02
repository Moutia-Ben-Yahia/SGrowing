namespace SGrowing.Api.Modules.Finance.SviVolatilitySurfaceCalibration;

public interface ISviVolatilityEngine
{
    SviVolatilityEvaluation AnalyzeSurface(SviVolatilityTelemetry telemetry);
}

public class SviVolatilityEngine : ISviVolatilityEngine
{
    public SviVolatilityEvaluation AnalyzeSurface(SviVolatilityTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Asymptotic wing slopes: b * (1 - rho) for put wing, b * (1 + rho) for call wing
        double b = telemetry.SviParameterB;
        double rho = telemetry.SviParameterRho;
        double putWingSlope = b * (1.0 - rho);
        double callWingSlope = b * (1.0 + rho);

        // Parameter admissibility
        bool isParamAdmissible = b >= 0.0 &&
                                 Math.Abs(rho) < 1.0 &&
                                 telemetry.SviParameterSigma > 0.0 &&
                                 (telemetry.SviParameterA + b * telemetry.SviParameterSigma * Math.Sqrt(1.0 - rho * rho)) >= 0.0;

        bool isButterflyFree = telemetry.MinimumButterflyDensityG >= 0.0;
        bool isCalendarFree = telemetry.IsCalendarSpreadMonotonic;
        bool isLeeMomentSatisfied = putWingSlope < 2.0 && callWingSlope < 2.0;

        if (!isButterflyFree)
        {
            regimes.Add("SVI_BUTTERFLY_ARBITRAGE_DENSITY_NEGATIVE");
            mitigations.Add("Re-fit SVI parameters using quasi-explicit surface parametrization to strictly enforce non-negative risk-neutral probability density g(k) >= 0.");
        }

        if (!isCalendarFree)
        {
            regimes.Add("CALENDAR_SPREAD_VOLATILITY_INVERSION");
            mitigations.Add("Re-slice calendar total variance curves with forward variance interpolation constraints (w(k, t2) >= w(k, t1)).");
        }

        if (!isLeeMomentSatisfied)
        {
            regimes.Add("SVI_WINGS_ASYMPTOTIC_SLOPE_BREACH");
            mitigations.Add("Bound asymptotic wing parameter b <= 2 / (1 + |rho|) to prevent violation of Roger Lee's asymptotic moment formula.");
        }

        bool isCalibrated = isParamAdmissible && isButterflyFree && isCalendarFree && isLeeMomentSatisfied &&
                            telemetry.CalibrationRmseBasisPoints <= 20.0;

        return new SviVolatilityEvaluation
        {
            OptionSurfaceUid = telemetry.OptionSurfaceUid,
            AsymptoticPutWingSlope = Math.Round(putWingSlope, 4),
            AsymptoticCallWingSlope = Math.Round(callWingSlope, 4),
            IsSviParametersAdmissible = isParamAdmissible,
            IsButterflyArbitrageFree = isButterflyFree,
            IsCalendarSpreadArbitrageFree = isCalendarFree,
            IsVolatilitySurfaceCalibrated = isCalibrated,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
