namespace SGrowing.Api.Modules.Telecom.Terahertz6gBeamSquint;

public interface ITerahertz6gBeamSquintEngine
{
    BeamSquintMitigationPlan EvaluateBeamSquint(BeamSquintEvaluationRequest request);
}

public class Terahertz6gBeamSquintEngine : ITerahertz6gBeamSquintEngine
{
    public BeamSquintMitigationPlan EvaluateBeamSquint(BeamSquintEvaluationRequest request)
    {
        var cfg = request.ArrayConfig;
        var lim = request.Limits;

        // 1. Calculate uncompensated beam squint drift:
        // sin(theta_m) = (f_c / f_m) * sin(theta_0)
        double targetAngleRad = cfg.TargetSteeringAngleDegrees * Math.PI / 180.0;
        double sinTarget = Math.Sin(targetAngleRad);
        double frequencyRatio = cfg.CarrierFrequencyGigaHertz / Math.Max(1.0, cfg.SubcarrierFrequencyGigaHertz);
        double sinSquinted = Math.Clamp(frequencyRatio * sinTarget, -0.999, 0.999);
        double squintedAngleRad = Math.Asin(sinSquinted);
        double uncompensatedDriftDeg = Math.Abs((squintedAngleRad - targetAngleRad) * 180.0 / Math.PI);

        // 2. Uncompensated array gain loss in dB:
        // Proportional to element count N and squared angular drift
        double normalizedArraySize = cfg.TotalAntennaElements / 256.0;
        double gainLossDb = Math.Clamp(Math.Pow(uncompensatedDriftDeg, 2.0) * normalizedArraySize * 0.85, 0.0, 35.0);

        // 3. Compensation via True-Time-Delay (TTD) units:
        int ttdUnits = Math.Max(1, cfg.TrueTimeDelayUnitsCount);
        double compensatedDriftDeg = uncompensatedDriftDeg / ttdUnits;
        double compensatedGainLossDb = gainLossDb / Math.Pow(ttdUnits, 2.0);

        bool toleranceExceeded = uncompensatedDriftDeg > lim.MaxAllowableBeamDriftDegrees ||
                                 gainLossDb > lim.MaxAllowableArrayGainLossDb;

        int optimalSubarrays = (int)Math.Ceiling(Math.Sqrt(gainLossDb / lim.MaxAllowableArrayGainLossDb));
        optimalSubarrays = Math.Clamp(optimalSubarrays, 2, 64);

        string state;
        string directive;

        if (toleranceExceeded)
        {
            state = "WIDEBAND_BEAM_SQUINT_DEGRADATION";
            directive = $"ACTIVATE_TTD_DELAY_LINES: Uncompensated beam squint ({uncompensatedDriftDeg:F2} deg drift, {gainLossDb:F1} dB gain loss) exceeds 6G link budget. Engage {optimalSubarrays} True-Time-Delay hybrid delay lines to restore subcarrier phase coherence.";
        }
        else
        {
            state = "SPATIAL_COHERENCE_MAINTAINED";
            directive = $"NOMINAL_PRECODING: Spatial beam alignment within {lim.MaxAllowableArrayGainLossDb:F1} dB contour across wideband channel. Phase-only analog precoding optimal.";
        }

        return new BeamSquintMitigationPlan(
            ArrayId: cfg.ArrayId,
            UncompensatedBeamDriftDegrees: Math.Round(uncompensatedDriftDeg, 2),
            EdgeSubcarrierArrayGainLossDb: Math.Round(gainLossDb, 2),
            CompensatedBeamDriftDegrees: Math.Round(compensatedDriftDeg, 3),
            BeamSquintExceedsTolerance: toleranceExceeded,
            OptimalTtdSubarraysCount: optimalSubarrays,
            PrecodingArchitectureState: state,
            RecommendedPrecodingDirective: directive
        );
    }
}
