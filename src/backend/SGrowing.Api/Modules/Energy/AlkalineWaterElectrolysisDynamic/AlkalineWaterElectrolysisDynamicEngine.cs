using System;

namespace SGrowing.Api.Modules.Energy.AlkalineWaterElectrolysisDynamic;

public sealed class AlkalineWaterElectrolysisDynamicEngine : IAlkalineWaterElectrolysisDynamicEngine
{
    private const double LowerExplosiveLimitH2InO2Percent = 4.0; // % LEL threshold

    public AlkalineWaterElectrolysisAnalysisResult AnalyzeElectrolysisStack(AlkalineWaterElectrolysisTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.StackId))
        {
            throw new ArgumentException("Stack ID must be provided.", nameof(telemetry));
        }

        if (telemetry.StackOperatingTemperatureCelsius <= 0 || telemetry.KohElectrolyteConcentrationPercent <= 0)
        {
            throw new ArgumentException("Operating temperature and KOH concentration must be positive.", nameof(telemetry));
        }

        if (telemetry.MeasuredCellVoltageVolts <= 0 || telemetry.CurrentDensityAmperesPerSquareMeter < 0)
        {
            throw new ArgumentException("Cell voltage and current density must be positive.", nameof(telemetry));
        }

        var tempKelvin = telemetry.StackOperatingTemperatureCelsius + 273.15;

        // Reversible Cell Voltage E_rev(T) = 1.229 - 0.00085 * (T_K - 298.15)
        var reversibleVoltage = 1.229 - (0.00085 * (tempKelvin - 298.15));

        // Specific energy consumption: kWh / Nm3 H2 (Faraday conversion factor ~ 2.392 * V_cell)
        var specificEnergyKwhPerNm3 = telemetry.MeasuredCellVoltageVolts * 2.392;

        // Safety margin against hydrogen crossover explosion
        var crossover = Math.Max(0.0, telemetry.DiaphragmGasCrossoverH2InO2Percent);
        var safetyMarginPercent = Math.Max(0.0, ((LowerExplosiveLimitH2InO2Percent - crossover) / LowerExplosiveLimitH2InO2Percent) * 100.0);

        string regime;
        string guidance;
        bool isHealthy = crossover < 1.8 &&
                         telemetry.MeasuredCellVoltageVolts <= 2.10 &&
                         (telemetry.CurrentDensityAmperesPerSquareMeter >= 800.0 || telemetry.ReverseCurrentProtectionCurrentAmperes > 0.0);

        if (crossover >= 2.0)
        {
            regime = "CRITICAL_EXPLOSIVE_GAS_CROSSOVER_TRIP_HAZARD";
            guidance = $"Hydrogen crossover in oxygen stream ({crossover:F2}%) exceeds 50% LEL safety limit (2.0%). Immediately isolate stack power and initiate nitrogen purge.";
            isHealthy = false;
        }
        else if (telemetry.CurrentDensityAmperesPerSquareMeter < 800.0 && telemetry.ReverseCurrentProtectionCurrentAmperes <= 0.0)
        {
            regime = "REVERSE_CURRENT_CATALYST_DEGRADATION_RISK";
            guidance = $"Low load current density ({telemetry.CurrentDensityAmperesPerSquareMeter:F0} A/m^2) without active cathodic trickle polarization. Risk of nickel hydride oxidation and anode dissolution; activate polarization rectifier.";
            isHealthy = false;
        }
        else if (telemetry.MeasuredCellVoltageVolts > 2.20)
        {
            regime = "HIGH_OVERPOTENTIAL_MEMBRANE_FOULING_REGIME";
            guidance = $"Elevated cell voltage ({telemetry.MeasuredCellVoltageVolts:F2} V) indicates bubble overpotential accumulation or diaphragm carbonate precipitation. Acid wash or increase electrolyte circulation velocity.";
            isHealthy = false;
        }
        else if (telemetry.MeasuredCellVoltageVolts <= 1.95 && crossover < 1.2)
        {
            regime = "OPTIMAL_DYNAMIC_ALKALINE_ELECTROLYSIS_REGIME";
            guidance = $"Stack operating at peak electrochemical efficiency ({specificEnergyKwhPerNm3:F2} kWh/Nm3 H2) with low crossover ({crossover:F2}%). Excellent renewable intermittency tracking.";
        }
        else
        {
            regime = "TRANSIENT_INTERMITTENT_LOAD_FOLLOWING_REGIME";
            guidance = $"Electrolyzer responding dynamically to renewable fluctuations. Current ramp rate is {telemetry.RenewablePowerRampRatePercentPerSecond:F1} %/s. Operating within stable parameters.";
        }

        return new AlkalineWaterElectrolysisAnalysisResult(
            telemetry.StackId,
            Math.Round(reversibleVoltage, 3),
            Math.Round(specificEnergyKwhPerNm3, 2),
            Math.Round(safetyMarginPercent, 1),
            regime,
            guidance,
            isHealthy
        );
    }
}
