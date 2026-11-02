using System;

namespace SGrowing.Api.Modules.Energy.LfpBatteryThermalRunawaySuppression;

public sealed class LfpBatteryThermalRunawaySuppressionEngine : ILfpBatteryThermalRunawaySuppressionEngine
{
    private const double CriticalRunawayTemperatureCelsius = 210.0; // LFP critical temperature threshold

    public LfpBatteryThermalRunawayAnalysisResult AnalyzeBatterySafety(LfpBatteryThermalRunawayTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.RackEnclosureId))
        {
            throw new ArgumentException("Rack enclosure ID must be provided.", nameof(telemetry));
        }

        if (telemetry.OffGasCarbonMonoxidePpm < 0 || telemetry.OffGasHydrogenPpm < 0 ||
            telemetry.LowerExplosiveLimitContainerAtmospherePercent < 0)
        {
            throw new ArgumentException("Gas concentrations cannot be negative.", nameof(telemetry));
        }

        var compositeGasIndex = (telemetry.OffGasCarbonMonoxidePpm / 25.0) + (telemetry.OffGasHydrogenPpm / 50.0);
        var criticalTempMargin = Math.Max(0.0, CriticalRunawayTemperatureCelsius - telemetry.MaxCellSurfaceTemperatureCelsius);

        string regime;
        string guidance;
        bool isSafe = telemetry.MaxCellSurfaceTemperatureCelsius <= 55.0 &&
                      telemetry.OffGasCarbonMonoxidePpm < 20.0 &&
                      telemetry.OffGasHydrogenPpm < 30.0 &&
                      telemetry.LowerExplosiveLimitContainerAtmospherePercent < 15.0;

        bool shouldTripHv = false;
        bool shouldDeployDeluge = false;

        if (telemetry.MaxCellSurfaceTemperatureCelsius >= 180.0 || telemetry.TemperatureRateOfRiseCelsiusPerSecond >= 1.5)
        {
            regime = "IMMINENT_THERMAL_RUNAWAY_DELUGE_SUPPRESSION_TRIGGERED";
            guidance = $"Critical thermal runaway active: cell temperature {telemetry.MaxCellSurfaceTemperatureCelsius:F1} C (dT/dt = {telemetry.TemperatureRateOfRiseCelsiusPerSecond:F2} C/s). Tripping master HV contactor and discharging water/aerosol deluge suppression immediately.";
            isSafe = false;
            shouldTripHv = true;
            shouldDeployDeluge = true;
        }
        else if (telemetry.LowerExplosiveLimitContainerAtmospherePercent >= 25.0)
        {
            regime = "DEFLAGRATION_EXPLOSION_HAZARD_PURGE_MANDATORY";
            guidance = $"Flammable gas concentration ({telemetry.LowerExplosiveLimitContainerAtmospherePercent:F1}% LEL) exceeds NFPA 855 25% threshold. Trip electrical loads and activate explosion-proof mechanical purge fans.";
            isSafe = false;
            shouldTripHv = true;
        }
        else if (telemetry.OffGasCarbonMonoxidePpm >= 25.0 || telemetry.OffGasHydrogenPpm >= 50.0)
        {
            regime = "EARLY_OFF_GAS_VENTING_ELECTRICAL_ISOLATION_TRIGGERED";
            guidance = $"Early stage cell venting detected: CO = {telemetry.OffGasCarbonMonoxidePpm:F1} ppm, H2 = {telemetry.OffGasHydrogenPpm:F1} ppm (Off-gas index = {compositeGasIndex:F2}). Open HV contactors to halt current flow before propagation.";
            isSafe = false;
            shouldTripHv = true;
        }
        else if (telemetry.MaxCellSurfaceTemperatureCelsius <= 45.0 && telemetry.OffGasCarbonMonoxidePpm < 5.0 && telemetry.OffGasHydrogenPpm < 5.0)
        {
            regime = "NOMINAL_STABLE_BESS_STORAGE_REGIME";
            guidance = $"BESS rack operating under nominal thermal and electrochemical conditions. Cell surface temperature is {telemetry.MaxCellSurfaceTemperatureCelsius:F1} C with zero off-gas signatures.";
        }
        else
        {
            regime = "ELEVATED_CELL_THERMAL_SURVEILLANCE_REGIME";
            guidance = $"Elevated temperature ({telemetry.MaxCellSurfaceTemperatureCelsius:F1} C) or trace gas detected. Increase HVAC chiller cooling and monitor sensor telemetry.";
        }

        return new LfpBatteryThermalRunawayAnalysisResult(
            telemetry.RackEnclosureId,
            Math.Round(compositeGasIndex, 3),
            Math.Round(criticalTempMargin, 1),
            regime,
            guidance,
            isSafe,
            shouldTripHv,
            shouldDeployDeluge
        );
    }
}
