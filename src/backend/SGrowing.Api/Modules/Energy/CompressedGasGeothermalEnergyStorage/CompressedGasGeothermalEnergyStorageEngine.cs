using System;

namespace SGrowing.Api.Modules.Energy.CompressedGasGeothermalEnergyStorage;

public sealed class CompressedGasGeothermalEnergyStorageEngine : ICompressedGasGeothermalEnergyStorageEngine
{
    public CompressedGasGeothermalAnalysisResult AnalyzeGeothermalStorage(CompressedGasGeothermalTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.StorageAquiferWellId))
        {
            throw new ArgumentException("Storage well ID must be provided.", nameof(telemetry));
        }

        if (telemetry.AquiferDepthMeters <= 0 || telemetry.FormationFracturePressureBar <= 0 ||
            telemetry.InjectionCompressorPowerMegawatts <= 0)
        {
            throw new ArgumentException("Depth, fracture pressure, and compressor power must be positive.", nameof(telemetry));
        }

        var exergyEfficiency = (telemetry.ExpansionTurbinePowerMegawatts / telemetry.InjectionCompressorPowerMegawatts) * 100.0;
        var fractureMarginPercent = ((telemetry.FormationFracturePressureBar - telemetry.CurrentAquiferPorePressureBar) / telemetry.FormationFracturePressureBar) * 100.0;
        var thermalBoost = Math.Max(0.0, telemetry.AmbientGeothermalTemperatureCelsius - telemetry.InjectedFluidTemperatureCelsius);

        string regime;
        string guidance;
        bool isSafe = telemetry.CurrentAquiferPorePressureBar < telemetry.FormationFracturePressureBar * 0.85 &&
                      fractureMarginPercent >= 15.0 &&
                      telemetry.AmbientGeothermalTemperatureCelsius >= 70.0;

        if (telemetry.CurrentAquiferPorePressureBar >= telemetry.FormationFracturePressureBar * 0.90)
        {
            regime = "CRITICAL_CAPROCK_HYDROFRACTURE_PRESSURE_HAZARD";
            guidance = $"Pore pressure ({telemetry.CurrentAquiferPorePressureBar:F1} bar) approaches formation breakdown threshold ({telemetry.FormationFracturePressureBar:F1} bar). Fracture safety margin is only {fractureMarginPercent:F1}%; throttle back surface compressor immediately.";
            isSafe = false;
        }
        else if (exergyEfficiency >= 115.0 && fractureMarginPercent >= 20.0)
        {
            regime = "OPTIMAL_GEOTHERMALLY_ENHANCED_EXERGY_SURPLUS_REGIME";
            guidance = $"Harvesting deep geothermal heat surplus: Round-trip efficiency is {exergyEfficiency:F1}% with {thermalBoost:F1} C geothermal boost. Safe geomechanical cushion ({fractureMarginPercent:F1}%) maintained.";
        }
        else if (telemetry.AmbientGeothermalTemperatureCelsius < 65.0)
        {
            regime = "INSUFFICIENT_GEOTHERMAL_HEAT_GRADIENT";
            guidance = $"Low geothermal temperature ({telemetry.AmbientGeothermalTemperatureCelsius:F1} C at {telemetry.AquiferDepthMeters:F0} m). Wellbore depth is inadequate to achieve thermodynamic exergy surplus.";
            isSafe = false;
        }
        else if (telemetry.ExpansionTurbinePowerMegawatts < telemetry.InjectionCompressorPowerMegawatts * 0.65)
        {
            regime = "POOR_WELLBORE_TRANSMISSIBILITY_PRESSURE_LOSS";
            guidance = $"Excessive drawdown pressure drop across near-wellbore skin zone. Round-trip recovery is only {exergyEfficiency:F1}%; acidize or perform matrix stimulation.";
            isSafe = false;
        }
        else
        {
            regime = "NOMINAL_COMPRESSED_GAS_GEO_STORAGE_REGIME";
            guidance = $"Subsurface gas storage functioning nominally. Round-trip exergy recovery is {exergyEfficiency:F1}%.";
        }

        return new CompressedGasGeothermalAnalysisResult(
            telemetry.StorageAquiferWellId,
            Math.Round(exergyEfficiency, 1),
            Math.Round(fractureMarginPercent, 1),
            Math.Round(thermalBoost, 1),
            regime,
            guidance,
            isSafe
        );
    }
}
