namespace SGrowing.Api.Modules.Energy.TokamakNtmEccdSuppression;

public interface ITokamakNtmEccdEngine
{
    TokamakNtmEccdEvaluation AnalyzeDischarge(TokamakNtmEccdTelemetry telemetry);
}

public class TokamakNtmEccdEngine : ITokamakNtmEccdEngine
{
    public TokamakNtmEccdEvaluation AnalyzeDischarge(TokamakNtmEccdTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Alignment efficiency: eta_align = exp(-(delta_r / w_eccd)^2)
        double wEccd = Math.Max(0.5, telemetry.EccdDepositionWidthCm);
        double alignRatio = telemetry.EccdDepositionRadialOffsetCm / wEccd;
        double alignEfficiency = Math.Clamp(Math.Exp(-alignRatio * alignRatio), 0.05, 1.0);
        double eccdEfficiencyPercent = alignEfficiency * 100.0;

        // Modified Rutherford Equation: dw/dt = Delta'_0 + Delta'_BS - Delta'_ECCD
        // Delta'_BS ~ beta_theta * (w / (w^2 + wd^2))
        double w = Math.Max(0.1, telemetry.MagneticIslandWidthCm);
        double wd = 0.8; // Polarization scale length cm
        double deltaBs = 4.2 * telemetry.PoloidalBetaTheta * (w / (w * w + wd * wd));

        // Classical stability parameter Delta'_0 ~ -m/r ~ -2.0 / 0.8 ~ -2.5 (stabilizing without bootstrap)
        double delta0 = -2.5;

        // ECCD stabilization term: Delta'_ECCD ~ (16 * P_ECCD / w^2) * eta_align
        double deltaEccd = (2.8 * telemetry.EccdInjectedPowerMegaWatts / (w * w)) * alignEfficiency;

        // Island growth rate dw/dt (in s^-1)
        double growthRate = delta0 + deltaBs - deltaEccd;

        // Projected suppression time (ms)
        double suppressionTimeMs = 0.0;
        if (growthRate < 0.0)
        {
            suppressionTimeMs = Math.Clamp((w / Math.Abs(growthRate)) * 100.0, 10.0, 2500.0);
        }

        bool isBeamAligned = Math.Abs(telemetry.EccdDepositionRadialOffsetCm) <= 1.2 && eccdEfficiencyPercent >= 70.0;
        bool isWidthControlled = telemetry.MagneticIslandWidthCm <= 5.0 && growthRate <= 0.5;
        bool isDisruptionAverted = telemetry.IslandRotationFrequencyKhz >= 2.0 && telemetry.MagneticIslandWidthCm < 6.5;

        if (telemetry.MagneticIslandWidthCm >= 5.5 || telemetry.IslandRotationFrequencyKhz < 2.0)
        {
            regimes.Add("NTM_ISLAND_GROWTH_APPROACHING_LOCKING");
            mitigations.Add("Ramp ECCD launcher power to maximum (20 MW) and apply resonant magnetic perturbation (RMP) torque to spin up mode.");
        }

        if (!isBeamAligned)
        {
            regimes.Add("ECCD_LAUNCHER_MIRROR_MISALIGNMENT");
            mitigations.Add("Engage real-time ECE radiometer feedback mirror steering to position microwave deposition inside island O-point (|delta_r| <= 0.8 cm).");
        }

        if (telemetry.NormalizedBetaDegradationPercent > 18.0)
        {
            regimes.Add("HIGH_BETA_PLASMA_CONFINEMENT_COLLAPSE");
            mitigations.Add("Trigger auxiliary neutral beam injection (NBI) throttle to avoid soft beta limit disruption.");
        }

        bool isStable = isBeamAligned && isWidthControlled && isDisruptionAverted && growthRate < 0.0;

        return new TokamakNtmEccdEvaluation
        {
            PulseDischargeId = telemetry.PulseDischargeId,
            CalculatedIslandGrowthRateInvSec = Math.Round(growthRate, 2),
            EccdStabilizationEfficiencyPercent = Math.Round(eccdEfficiencyPercent, 1),
            ProjectedIslandSuppressionTimeMs = Math.Round(suppressionTimeMs, 1),
            IsEccdBeamAligned = isBeamAligned,
            IsIslandWidthControlled = isWidthControlled,
            IsModeLockedDisruptionAverted = isDisruptionAverted,
            IsPlasmaDischargeStable = isStable,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
