using System;

namespace SGrowing.Api.Modules.Energy.AdiabaticCaesThermalStorage;

public sealed class AdiabaticCaesThermalStorageEngine : IAdiabaticCaesThermalStorageEngine
{
    public AdiabaticCaesAnalysisResult AnalyzeAdiabaticCaes(AdiabaticCaesTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var pCavern = Math.Max(1.0, telemetry.CavernPressureBar);
        var tTes = Math.Max(20.0, telemetry.ThermalStorageTemperatureCelsius);
        var leak = Math.Max(0.0, telemetry.CavernDailyAirLeakagePercent);

        // Round-trip exergy efficiency calculation (%)
        var efficiency = Math.Clamp(54.0 + 0.045 * tTes + 0.085 * pCavern - 2.5 * leak, 35.0, 78.0);

        // Stored exergy calculation in MegaWatt-Hours (MWh)
        var storedExergyMwh = (pCavern * 1.35 * (tTes / 100.0));

        // Geomechanical safety factor based on lithostatic margin and cushion floor
        var upperMargin = Math.Max(0.0, 0.83 - telemetry.CavernPressureLithostaticRatio);
        var lowerMargin = Math.Max(0.0, (pCavern - telemetry.MinimumPermissibleCavernPressureBar) / 25.0);
        var safetyFactor = Math.Max(0.2, 1.0 + 2.2 * Math.Min(upperMargin, lowerMargin));

        string regime;
        string action;
        bool isOperational;

        if (pCavern > telemetry.MaximumPermissibleCavernPressureBar || telemetry.CavernPressureLithostaticRatio > 0.83)
        {
            regime = "CAVERN_LITHOSTATIC_FRACTURE_HAZARD";
            action = "Salt cavern internal pressure exceeds geomechanical lithostatic fracture limit (> 83% lithostatic). Halt compressor injection and vent pressure to relief manifold.";
            isOperational = false;
        }
        else if (pCavern < telemetry.MinimumPermissibleCavernPressureBar)
        {
            regime = "CAVERN_CREEP_CONVERGENCE_COLLAPSE_RISK";
            action = "Cavern pressure below minimum cushion pressure. Excessive deviatoric shear stress threatens rapid salt rock creep convergence. Halt discharge expansion.";
            isOperational = false;
        }
        else if (tTes < 180.0)
        {
            regime = "TES_THERMAL_EXERGY_DEFICIT";
            action = "Thermal energy storage (TES) bed temperature below expansion preheat threshold (< 180 C). Pre-charge thermal regenerator before turbine spin-up.";
            isOperational = false;
        }
        else
        {
            regime = "STEADY_STATE_ADIABATIC_CAES_CYCLING";
            action = "Adiabatic CAES operating within optimal thermodynamic and geomechanical envelope. High exergy round-trip efficiency maintained without fossil emissions.";
            isOperational = true;
        }

        return new AdiabaticCaesAnalysisResult(
            CavernId: telemetry.CavernId,
            RoundTripExergyEfficiencyPercent: efficiency,
            AvailableStoredExergyMegaWattHours: storedExergyMwh,
            CavernGeomechanicalSafetyFactor: safetyFactor,
            CaesOperatingRegime: regime,
            DispatchAndSafetyAction: action,
            IsCaesOperational: isOperational
        );
    }
}
