namespace SGrowing.Api.Modules.Energy.SupercriticalWellboreJouleThomson;

public class SupercriticalWellboreEngine
{
    private const double Gravity = 9.81; // m/s^2
    private const double JouleThomsonCoefficientSco2 = 1.8; // deg C / MPa

    public SupercriticalWellboreAnalysisResult AnalyzeWellbore(SupercriticalWellboreTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.WellboreId))
        {
            throw new ArgumentException("Wellbore ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.WellboreDepthMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Wellbore depth must be positive.");
        }

        if (telemetry.MassFlowRateKgPerSec <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Mass flow rate must be positive.");
        }

        // Approximate density profile for supercritical fluid:
        // Annulus (cold down-flow, high density ~650-800 kg/m^3 for sCO2)
        // Tubing (hot up-flow, low density ~150-250 kg/m^3 for sCO2)
        double rhoAnnulus = Math.Clamp(750.0 - (telemetry.AnnulusInletTemperatureCelsius * 1.5), 450.0, 900.0);
        double rhoTubing = Math.Clamp(220.0 - (telemetry.TubingOutletTemperatureCelsius * 0.15), 100.0, 400.0);

        // Buoyancy driving pressure: Delta_P = (rho_a - rho_t) * g * H [Pa -> MPa]
        double thermosiphonPressureMpa = (rhoAnnulus - rhoTubing) * Gravity * telemetry.WellboreDepthMeters * 1e-6;

        // Frictional pressure loss in wellbore loop [MPa]
        double frictionLossMpa = 1.5 + (0.04 * telemetry.MassFlowRateKgPerSec) + (telemetry.WellboreDepthMeters * 0.0003);

        bool isSelfDriven = thermosiphonPressureMpa > frictionLossMpa;

        // Joule-Thomson expansion temperature shift across surface choke valve:
        // dT_JT = -mu_JT * Delta_P_choke
        double jouleThomsonShift = -JouleThomsonCoefficientSco2 * telemetry.SurfaceChokePressureDropMpa;

        // Thermal power output: P_th = m_dot * Cp * Delta_T [MW]
        double cp = string.Equals(telemetry.WorkingFluid, "Supercritical-Water", StringComparison.OrdinalIgnoreCase) ? 4.2 : 1.8;
        double deltaTemp = Math.Max(0.0, telemetry.TubingOutletTemperatureCelsius - telemetry.AnnulusInletTemperatureCelsius);
        double thermalPowerMw = telemetry.MassFlowRateKgPerSec * cp * deltaTemp * 1e-3;

        string state;
        string action;
        string summary;

        if (jouleThomsonShift <= -6.0)
        {
            state = "JOULE_THOMSON_FREEZING_HAZARD";
            action = "RESTRICT_CHOKE_PRESSURE_DROP";
            summary = $"JOULE-THOMSON CHATTERING: High choke drop ({telemetry.SurfaceChokePressureDropMpa:F1} MPa) induced {jouleThomsonShift:F1}°C cooling, threatening phase transition boundary. Restrict choke valve opening.";
        }
        else if (isSelfDriven)
        {
            state = "SELF_DRIVEN_THERMOSIPHON_OPTIMAL";
            action = "MAINTAIN_CHOKE_OPENING";
            summary = $"OPTIMAL THERMOSIPHON: Natural circulation sustained with {thermosiphonPressureMpa:F1} MPa driving pressure vs {frictionLossMpa:F1} MPa friction. Generating {thermalPowerMw:F1} MWth cleanly without auxiliary pumps.";
        }
        else
        {
            state = "PARASITIC_PUMPING_REQUIRED";
            action = "MODULATE_CHOKE_INCREASE_FLOW";
            summary = $"INSUFFICIENT BUOYANCY: Thermosiphon pressure ({thermosiphonPressureMpa:F1} MPa) is below loop friction ({frictionLossMpa:F1} MPa). Auxiliary surface booster pump engaged.";
        }

        return new SupercriticalWellboreAnalysisResult(
            telemetry.WellboreId,
            Math.Round(thermosiphonPressureMpa, 2),
            Math.Round(frictionLossMpa, 2),
            isSelfDriven,
            Math.Round(jouleThomsonShift, 1),
            Math.Round(thermalPowerMw, 2),
            state,
            action,
            summary);
    }
}
