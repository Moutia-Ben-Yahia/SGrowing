using System;

namespace SGrowing.Api.Modules.Marine.MhdMarineSilentPropulsion;

public sealed class MhdMarineSilentPropulsionEngine : IMhdMarineSilentPropulsionEngine
{
    public MhdMarineAnalysisResult AnalyzeMhdThruster(MhdMarineTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var ductArea = Math.Max(0.1, telemetry.ThrusterDuctCrossSectionAreaM2);
        var ductLength = Math.Max(0.5, telemetry.ThrusterDuctLengthMeters);
        var ductVolumeM3 = ductArea * ductLength;

        var j = Math.Max(1.0, telemetry.TransverseCurrentDensityAmpsPerMeter2);
        var b = Math.Max(0.1, telemetry.MagneticFluxDensityTesla);

        // Lorentz thrust: F = J * B * Volume (Newtons -> kN)
        var thrustKn = Math.Round((j * b * ductVolumeM3) / 1000.0, 2);

        // Propulsive efficiency: eta = (v * B) / (v * B + J / sigma)
        var v = Math.Max(0.5, telemetry.SeawaterFlowVelocityMetersPerSecond);
        var sigma = Math.Max(0.5, telemetry.SeawaterConductivitySiemensPerMeter);
        var vb = v * b;
        var ohmicLoss = j / sigma;
        var efficiency = Math.Round(Math.Clamp(vb / (vb + ohmicLoss), 0.05, 0.95), 3);

        // Acoustic noise suppression (relative to standard 95 dB mechanical propeller)
        const double baselineScrewNoiseDb = 95.0;
        var measuredNoise = Math.Clamp(telemetry.HydroacousticCavitationNoiseDecibels, 20.0, 120.0);
        var noiseSuppressionDb = Math.Round(baselineScrewNoiseDb - measuredNoise, 1);

        string regime;
        string command;
        bool isStealth;

        if (telemetry.MagneticFluxDensityTesla > 14.5 || !telemetry.SuperconductingCryostatQuenchProtectionArmed)
        {
            regime = "MAGNETIC_CRITICAL_CURRENT_QUENCH_HAZARD";
            command = "REBCO magnet near upper critical field or quench protection disarmed. Dump coil current into dump resistor.";
            isStealth = false;
        }
        else if (telemetry.TransverseCurrentDensityAmpsPerMeter2 > 3000.0)
        {
            regime = "ELECTRODE_THERMAL_SATURATION_WARNING";
            command = "Electrode current density exceeds 3000 A/m2. Throttle power converters to prevent thermal boundary boiling.";
            isStealth = false;
        }
        else if (telemetry.HydroacousticCavitationNoiseDecibels > 65.0)
        {
            regime = "CHLORINE_OVERPOTENTIAL_BUBBLE_CAVITATION";
            command = "Electrochemical microbubble coalescence producing acoustic signature. Shift electrode bias to suppress chlorine evolution.";
            isStealth = false;
        }
        else
        {
            regime = "SILENT_SUPERCONDUCTING_LORENTZ_CRUISE";
            command = "Duct Lorentz force steady. Acoustic emissions <50 dB; submarine operating in ultra-silent hydrodynamic stealth.";
            isStealth = true;
        }

        return new MhdMarineAnalysisResult(
            VesselDuctId: telemetry.VesselDuctId,
            ElectromagneticLorentzThrustKilonewtons: thrustKn,
            DuctPropulsiveEfficiency: efficiency,
            AcousticNoiseSuppressionDecibels: noiseSuppressionDb,
            ThrusterOperationalRegime: regime,
            ElectrohydrodynamicCommand: command,
            IsPropulsionStealthMaintained: isStealth
        );
    }
}
