using System;

namespace SGrowing.Api.Modules.Aerospace.ObliqueDetonationWaveEngine;

public sealed class ObliqueDetonationWaveEngine : IObliqueDetonationWaveEngine
{
    public ObliqueDetonationAnalysisResult AnalyzeDetonationWaveState(ObliqueDetonationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var mach = Math.Max(1.5, telemetry.FlightMachNumber);
        var phi = Math.Max(0.1, telemetry.HydrogenFuelEquivalenceRatio);

        // Chapman-Jouguet detonation Mach number for H2-air mixture
        var mCj = Math.Round(4.2 + 0.85 * Math.Min(1.6, phi), 2);

        // Oblique detonation wave angle beta (degrees)
        var machAngleRad = Math.Asin(1.0 / mach);
        var thetaRad = telemetry.RampDeflectionAngleDegrees * (Math.PI / 180.0);
        var waveAngleRad = machAngleRad + (thetaRad * 0.90);
        var waveAngleDeg = Math.Round(Math.Min(90.0, waveAngleRad * (180.0 / Math.PI)), 2);

        // Combustor static pressure ratio across oblique detonation wave (Rayleigh-Hugoniot)
        const double gamma = 1.28;
        var normalMachSq = Math.Pow(mach * Math.Sin(waveAngleRad), 2);
        var pressureRatio = Math.Round(Math.Max(1.0, 1.0 + ((2.0 * gamma) / (gamma + 1.0)) * (normalMachSq - 1.0)), 2);

        // Heat release efficiency based on equivalence ratio
        var heatReleaseEff = Math.Round(Math.Clamp(1.0 - (0.30 * Math.Abs(phi - 1.0)), 0.20, 1.0), 3);

        var combustorLength = Math.Max(50.0, telemetry.CombustorLengthMillimeters);
        var inductionRatio = telemetry.InductionZoneLengthMillimeters / combustorLength;

        string regime;
        string command;
        bool isStable;

        if (telemetry.RampDeflectionAngleDegrees > 29.5 || waveAngleDeg > 70.0)
        {
            regime = "DETACHED_BOW_SHOCK_UNSTART_DANGER";
            command = "Ramp angle exceeds shock detachment limit. Retract variable cowl ramp by -4.5 deg and throttle fuel injection.";
            isStable = false;
        }
        else if (inductionRatio > 0.65)
        {
            regime = "INDUCTION_BLOWOUT_UNSTABLE";
            command = "Induction delay too long for combustor length. Advance upstream plasma igniters and increase pre-compression.";
            isStable = false;
        }
        else if (phi < 0.45)
        {
            regime = "DEFLAGRATION_CROSSOVER_DEGRADED";
            command = "Lean blowout threshold. Enrichen H2 injection to restore Chapman-Jouguet detonation coupling.";
            isStable = false;
        }
        else
        {
            regime = "STANDING_OBLIQUE_DETONATION_OPTIMAL";
            command = "Detonation wave anchored on ramp shoulder. Pressure gain combustion verified with zero inlet unstart risk.";
            isStable = true;
        }

        return new ObliqueDetonationAnalysisResult(
            EngineId: telemetry.EngineId,
            ChapmanJouguetMachNumber: mCj,
            DetonationWaveAngleDegrees: waveAngleDeg,
            CombustorStaticPressureRatio: pressureRatio,
            HeatReleaseEfficiency: heatReleaseEff,
            CombustionRegime: regime,
            RampActuationCommand: command,
            IsPropulsionStable: isStable
        );
    }
}
