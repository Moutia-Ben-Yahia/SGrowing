using System;

namespace SGrowing.Api.Modules.Transportation.MaglevInductrackNullFlux;

public sealed class MaglevInductrackNullFluxEngine : IMaglevInductrackNullFluxEngine
{
    public MaglevInductrackAnalysisResult AnalyzeLevitationState(MaglevInductrackTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double halbachWavelengthMeters = 0.50;
        var waveNumberK = (2.0 * Math.PI) / halbachWavelengthMeters; // ~12.566 m^-1

        var speedMetersPerSec = Math.Max(0.1, telemetry.VehicleSpeedKph / 3.6);
        var frequencyOmega = waveNumberK * speedMetersPerSec;

        var inductanceHenries = Math.Max(1e-8, telemetry.TrackCoilInductanceMicroHenries * 1e-6);
        var resistanceOhms = Math.Max(1e-6, telemetry.TrackCoilResistanceOhms);

        // Transition velocity where omega * L = R (takeoff transition)
        var transitionSpeedMps = resistanceOhms / (waveNumberK * inductanceHenries);
        var transitionSpeedKph = Math.Round(transitionSpeedMps * 3.6, 1);

        var omegaLOverR = (frequencyOmega * inductanceHenries) / resistanceOhms;

        // Peak asymptotic lift force modulated by airgap exponential attenuation exp(-2*k*airgap)
        var airgapMeters = Math.Max(0.001, telemetry.AirgapMillimeters / 1000.0);
        var airgapAttenuation = Math.Exp(-2.0 * waveNumberK * Math.Max(0.0, airgapMeters - 0.015));
        var b0Factor = Math.Pow(Math.Max(0.1, telemetry.HalbachPeakMagneticFieldTesla) / 1.40, 2);
        var maxLiftForceKn = 480.0 * b0Factor * airgapAttenuation;

        // Lift force: F_L = F_max * ((wL/R)^2 / (1 + (wL/R)^2))
        var liftForceKn = Math.Round(maxLiftForceKn * (Math.Pow(omegaLOverR, 2) / (1.0 + Math.Pow(omegaLOverR, 2))), 2);

        // Magnetic drag force: F_D = F_max * ((wL/R) / (1 + (wL/R)^2))
        var dragForceKn = Math.Round(maxLiftForceKn * (omegaLOverR / (1.0 + Math.Pow(omegaLOverR, 2))), 2);

        var liftToDragRatio = Math.Round(liftForceKn / Math.Max(0.1, dragForceKn), 2);

        string regime;
        string command;
        bool isStable;

        if (telemetry.AirgapMillimeters < 8.0)
        {
            regime = "AIRGAP_COLLAPSE_TOUCHDOWN_HAZARD";
            command = "Airgap collapsed below 8 mm safety margin. Deploy secondary mechanical landing wheels and engage emergency electrodynamic brake.";
            isStable = false;
        }
        else if (telemetry.TrackTemperatureCelsius > 75.0)
        {
            regime = "TRACK_EDDY_OVERHEATING_WARNING";
            command = "Track coil Joule heating exceeds 75C thermal limits. Escalate guideway forced-air cooling and space vehicle dispatch headway.";
            isStable = false;
        }
        else if (telemetry.VehicleSpeedKph <= transitionSpeedKph * 1.35)
        {
            regime = "TRANSITION_SPEED_MAGNETIC_DRAG_PEAK";
            command = "Vehicle traversing magnetic drag peak. Command full linear synchronous motor (LSM) acceleration to punch through transition barrier.";
            isStable = true;
        }
        else
        {
            regime = "HIGH_SPEED_SUPERCONDUCTING_LEVITATION_OPTIMAL";
            command = "Stable Inductrack EDS levitation verified. High L/D ratio achieves low specific energy consumption at cruise.";
            isStable = true;
        }

        return new MaglevInductrackAnalysisResult(
            BogieId: telemetry.BogieId,
            ElectrodynamicLiftForceKilonewtons: liftForceKn,
            MagneticDragForceKilonewtons: dragForceKn,
            LiftToDragRatio: liftToDragRatio,
            TransitionSpeedKph: transitionSpeedKph,
            SuspensionRegime: regime,
            LinearMotorPropulsionCommand: command,
            IsLevitationStable: isStable
        );
    }
}
