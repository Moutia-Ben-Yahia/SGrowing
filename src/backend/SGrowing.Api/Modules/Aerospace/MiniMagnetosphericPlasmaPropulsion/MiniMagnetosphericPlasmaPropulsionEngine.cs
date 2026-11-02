using System;

namespace SGrowing.Api.Modules.Aerospace.MiniMagnetosphericPlasmaPropulsion;

public sealed class MiniMagnetosphericPlasmaPropulsionEngine : IMiniMagnetosphericPlasmaPropulsionEngine
{
    public M2p2AnalysisResult AnalyzePlasmaMagnetosphere(M2p2Telemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double protonMassKg = 1.6726219e-27;

        var vSw = Math.Max(50.0, telemetry.SolarWindSpeedKmPerSec) * 1000.0;
        var nProtonsM3 = Math.Max(0.1, telemetry.SolarWindProtonDensityPerCubicCm) * 1e6;

        // Dynamic pressure P_dyn = 0.5 * rho * v^2 [Pa]
        var pDynPascals = 0.5 * (nProtonsM3 * protonMassKg) * Math.Pow(vSw, 2);
        var pDynNanoPascals = pDynPascals * 1e9;

        // Cross sectional area of inflated magnetic bubble [m2]
        var rMagKm = Math.Max(0.5, telemetry.MagnetosphereRadiusKilometers);
        var rMagMeters = rMagKm * 1000.0;
        var magnetosphereArea = Math.PI * Math.Pow(rMagMeters, 2);

        // Net momentum transfer thrust (N)
        var netThrust = 2.0 * pDynPascals * magnetosphereArea;

        // Radiation dose attenuation percent
        var fieldRatio = Math.Max(0.1, telemetry.SuperconductingDipoleFieldTesla / 2.0);
        var attenuation = Math.Clamp(40.0 + 3.0 * rMagKm * fieldRatio, 10.0, 99.5);

        string regime;
        string action;
        bool isOperational;

        if (telemetry.SuperconductingCoilTemperatureKelvin > 85.0)
        {
            regime = "SUPERCONDUCTING_MAGNET_QUENCH_HAZARD";
            action = "High-temperature superconducting (HTS) dipole coil approaching thermal runaway quench boundary (> 85 K). Ramp down coil current and maximize cryocooler loop flow.";
            isOperational = false;
        }
        else if (telemetry.HeliconPlasmaFeedRateMilligramsPerSec < 0.5 || rMagKm < 5.0)
        {
            regime = "PLASMA_DEFLATION_UNDERPRESSURIZED";
            action = "Plasma injection rate insufficient to sustain magnetospheric inflation. Increase helicon RF power and gas feed valve duty cycle.";
            isOperational = false;
        }
        else if (pDynNanoPascals > 6.0)
        {
            regime = "SOLAR_STORM_COMPRESSION_DEFLECTION_SURGE";
            action = "Coronal Mass Ejection (CME) shock compression encountered. Artificial magnetosphere compressed; thrust and SEP particle deflection maximized.";
            isOperational = true;
        }
        else
        {
            regime = "NOMINAL_MAGNETOSPHERIC_PROPULSION_AND_SHIELDING";
            action = "Mini-magnetosphere stably inflated. Continuous solar wind momentum transfer and deep-space crew radiation shielding nominal.";
            isOperational = true;
        }

        return new M2p2AnalysisResult(
            SpacecraftId: telemetry.SpacecraftId,
            SolarWindDynamicPressureNanoPascals: pDynNanoPascals,
            NetThrustNewtons: netThrust,
            RadiationDoseAttenuationPercent: attenuation,
            PropulsionAndShieldingRegime: regime,
            MagnetosphereControlAction: action,
            IsSystemOperational: isOperational
        );
    }
}
