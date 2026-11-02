using System;

namespace SGrowing.Api.Modules.Aerospace.RotatingDetonationRocketCombustor;

public sealed class RotatingDetonationRocketCombustorEngine : IRotatingDetonationRocketCombustorEngine
{
    private const double BaselineIsobaricIsp = 310.0; // s for LOX/LCH4 deflagration

    public RotatingDetonationAnalysisResult AnalyzeDetonationCombustor(RotatingDetonationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.EngineId))
        {
            throw new ArgumentException("Engine ID must be provided.", nameof(telemetry));
        }

        if (telemetry.CombustorOuterDiameterMillimeters <= telemetry.AnnularGapWidthMillimeters ||
            telemetry.AnnularGapWidthMillimeters <= 0)
        {
            throw new ArgumentException("Outer diameter must strictly exceed annular gap width.", nameof(telemetry));
        }

        if (telemetry.TheoreticalCjVelocityMetersPerSecond <= 0 || telemetry.DetonationWaveVelocityMetersPerSecond < 0)
        {
            throw new ArgumentException("Velocities must be positive.", nameof(telemetry));
        }

        var meanDiameterM = (telemetry.CombustorOuterDiameterMillimeters - telemetry.AnnularGapWidthMillimeters) / 1000.0;
        var meanCircumferenceM = Math.PI * meanDiameterM;

        var waveCount = Math.Max(0, telemetry.ObservedWaveCount);
        var frequencyKhz = meanCircumferenceM > 0 && waveCount > 0
            ? (waveCount * telemetry.DetonationWaveVelocityMetersPerSecond) / (meanCircumferenceM * 1000.0)
            : 0.0;

        var deficitPercent = Math.Max(0.0,
            ((telemetry.TheoreticalCjVelocityMetersPerSecond - telemetry.DetonationWaveVelocityMetersPerSecond) /
             telemetry.TheoreticalCjVelocityMetersPerSecond) * 100.0);

        var crClamped = Math.Clamp(telemetry.CounterRotatingWaveIntensityRatio, 0.0, 1.0);
        var deficitFactor = Math.Max(0.0, 1.0 - (deficitPercent / 100.0));
        var counterFactor = Math.Max(0.0, 1.0 - crClamped);

        // Pressure Gain Combustion ratio: ~1.05 - 1.15x
        var pressureGainRatio = 1.0 + (0.12 * deficitFactor * counterFactor);

        // Theoretical Isp boost from Humphrey/Fickett-Jacobs pressure gain cycle
        var estimatedIsp = BaselineIsobaricIsp * Math.Pow(pressureGainRatio, 0.45);

        string regime;
        string guidance;
        bool isStable = crClamped < 0.20 && deficitPercent < 12.0 && waveCount >= 1;

        if (waveCount == 0 || telemetry.DetonationWaveVelocityMetersPerSecond < 500.0)
        {
            regime = "DETONATION_QUENCHED_FLAME_EXTINCTION";
            guidance = "No coherent detonation wave observed. Inflow velocity or stoichiometry is outside detonation cell limits; trigger spark plug re-ignition.";
            isStable = false;
            pressureGainRatio = 1.0;
            estimatedIsp = BaselineIsobaricIsp;
        }
        else if (crClamped > 0.40)
        {
            regime = "CHAOTIC_COUNTER_ROTATING_MODE_COLLISION_HAZARD";
            guidance = $"Elevated counter-rotating wave intensity ({crClamped * 100:F1}%). Head-on wave collisions induce severe acoustic pressure spikes. Bias injector injection momentum angle.";
            isStable = false;
        }
        else if (deficitPercent > 20.0)
        {
            regime = "SEVERE_DETONATION_DEFICIT_DEFLAGRATION_TRANSITION";
            guidance = $"Detonation velocity deficit ({deficitPercent:F1}%) indicates incomplete mixing or excessive wall thermal quenching. Annulus width or mixture ratio adjustment required.";
            isStable = false;
        }
        else if (crClamped < 0.15 && deficitPercent < 8.0)
        {
            regime = "STABLE_SUPERSONIC_CONTINUOUS_DETONATION_REGIME";
            guidance = $"Highly stable detonation front operating at {frequencyKhz:F2} kHz ({waveCount} waves). Estimated Isp improved to {estimatedIsp:F1} s ({pressureGainRatio:F3}x pressure gain).";
        }
        else
        {
            regime = "MARGINALLY_STABLE_MULTI_WAVE_REGIME";
            guidance = $"Continuous detonation sustained with {waveCount} waves, but slight wave jitter detected. Monitor plenum feed pressure stability.";
        }

        return new RotatingDetonationAnalysisResult(
            telemetry.EngineId,
            Math.Round(frequencyKhz, 2),
            Math.Round(deficitPercent, 2),
            Math.Round(pressureGainRatio, 4),
            Math.Round(estimatedIsp, 1),
            regime,
            guidance,
            isStable
        );
    }
}
