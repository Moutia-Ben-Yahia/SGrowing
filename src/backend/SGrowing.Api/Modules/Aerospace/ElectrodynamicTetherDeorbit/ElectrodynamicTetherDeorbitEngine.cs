using System;

namespace SGrowing.Api.Modules.Aerospace.ElectrodynamicTetherDeorbit;

public sealed class ElectrodynamicTetherDeorbitEngine : IElectrodynamicTetherDeorbitEngine
{
    public ElectrodynamicTetherAnalysisResult AnalyzeElectrodynamicTether(ElectrodynamicTetherTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var vRel = Math.Max(1.0, telemetry.OrbitalVelocityKmPerSec) * 1000.0;
        var bTesla = Math.Max(1.0, telemetry.GeomagneticFieldMicroTesla) * 1e-6;
        var lMeters = Math.Max(0.1, telemetry.TetherLengthKilometers) * 1000.0;

        // Induced motional electromotive force: EMF = v * B * L (Volts)
        var inducedEmf = vRel * bTesla * lMeters;

        // Lorentz deceleration drag force: F = I * L * B (Newtons)
        var current = Math.Max(0.0, telemetry.TetherInducedCurrentAmperes);
        var lorentzDrag = current * lMeters * bTesla;

        // Projected daily altitude decay (km/day) for typical 450 kg smallsat
        var altitudeLossPerDay = lorentzDrag * 18.5;

        string regime;
        string guidance;
        bool isOptimal;

        if (telemetry.TetherLibrationalSwingAngleDegrees > 24.0)
        {
            regime = "LIBRATIONAL_RESONANCE_INSTABILITY";
            guidance = "Electrodynamic torque induced excessive in-plane libration (> 24 deg). Modulate tether current duty cycle to damp gravitational pendular oscillations.";
            isOptimal = false;
        }
        else if (telemetry.IonosphericPlasmaDensityPerCubicCm < 1e4 || current < 0.20)
        {
            regime = "IONOSPHERIC_CURRENT_STARVATION";
            guidance = "Nighttime equatorial plasma depletion trough encountered (< 10^4 cm^-3). Activate auxiliary hollow cathode gas discharge to maintain circuit closure.";
            isOptimal = false;
        }
        else if (telemetry.TetherTensionNewtons < 1.0)
        {
            regime = "ZERO_TENSION_SLACK_SEVERANCE_HAZARD";
            guidance = "Tether tension depleted near zero. Risk of catastrophic tether tangling or recoil snapping. Engage end-mass micro-thruster tensioning.";
            isOptimal = false;
        }
        else
        {
            regime = "STEADY_STATE_LORENTZ_DEORBITING";
            guidance = "Electrodynamic tether operating at peak Lorentz drag efficiency. Propellantless orbital decay proceeding along nominal reentry corridor.";
            isOptimal = true;
        }

        return new ElectrodynamicTetherAnalysisResult(
            SatelliteId: telemetry.SatelliteId,
            InducedMotionalEmfVolts: inducedEmf,
            LorentzDecelerationDragNewtons: lorentzDrag,
            ProjectedAltitudeLossKmPerDay: altitudeLossPerDay,
            DeorbitRegime: regime,
            ElectrodynamicControlGuidance: guidance,
            IsDeorbitingOptimal: isOptimal
        );
    }
}
