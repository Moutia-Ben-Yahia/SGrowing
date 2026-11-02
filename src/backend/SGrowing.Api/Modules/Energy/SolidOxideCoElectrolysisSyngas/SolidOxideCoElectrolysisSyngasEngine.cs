using System;

namespace SGrowing.Api.Modules.Energy.SolidOxideCoElectrolysisSyngas;

public sealed class SolidOxideCoElectrolysisSyngasEngine : ISolidOxideCoElectrolysisSyngasEngine
{
    private const double TargetFischerTropschRatio = 2.05; // Ideal H2/CO for Fischer-Tropsch

    public SolidOxideCoElectrolysisAnalysisResult AnalyzeCoElectrolysisStack(SolidOxideCoElectrolysisTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.StackId))
        {
            throw new ArgumentException("Stack ID must be provided.", nameof(telemetry));
        }

        if (telemetry.StackOperatingTemperatureCelsius <= 500.0 || telemetry.OperatingCellVoltageVolts <= 0 ||
            telemetry.AreaSpecificResistanceOhmCm2 <= 0)
        {
            throw new ArgumentException("High-temperature SOEC requires T > 500 C, V > 0, and ASR > 0.", nameof(telemetry));
        }

        var tempKelvin = telemetry.StackOperatingTemperatureCelsius + 273.15;

        // Thermoneutral voltage: V_tn ~ 1.285 V at 800 C (1073.15 K)
        var thermoneutralVoltage = 1.285 + (0.00005 * (tempKelvin - 1073.15));

        var voltageDelta = telemetry.OperatingCellVoltageVolts - thermoneutralVoltage;
        string thermalMode;
        if (Math.Abs(voltageDelta) <= 0.04)
        {
            thermalMode = "THERMONEUTRAL_BALANCED";
        }
        else if (voltageDelta < -0.04)
        {
            thermalMode = "ENDOTHERMIC_HEAT_ABSORBING";
        }
        else
        {
            thermalMode = "EXOTHERMIC_HEAT_DISSIPATING";
        }

        var ratioDivergence = Math.Abs(telemetry.MeasuredOutletSyngasH2ToCoRatio - TargetFischerTropschRatio);

        string regime;
        string guidance;
        bool isOptimal = ratioDivergence <= 0.25 &&
                         telemetry.AreaSpecificResistanceOhmCm2 < 0.40 &&
                         telemetry.OperatingCellVoltageVolts <= 1.40;

        if (telemetry.AreaSpecificResistanceOhmCm2 >= 0.55)
        {
            regime = "SEVERE_ELECTRODE_DELAMINATION_ASR_DEGRADATION";
            guidance = $"Elevated ASR ({telemetry.AreaSpecificResistanceOhmCm2:F2} Ohm cm^2) indicates fuel electrode/electrolyte interface delamination or chromium poisoning from metallic interconnects.";
            isOptimal = false;
        }
        else if (telemetry.MeasuredOutletSyngasH2ToCoRatio < 1.0 || telemetry.MeasuredOutletSyngasH2ToCoRatio > 3.5)
        {
            regime = "OFF_SPEC_SYNGAS_COMPOSITION_HAZARD";
            guidance = $"Outlet syngas ratio (H2/CO = {telemetry.MeasuredOutletSyngasH2ToCoRatio:F2}) violates downstream reactor specs. Adjust inlet steam-to-CO2 molar ratio (currently {telemetry.InletSteamToCo2MolarRatio:F2}).";
            isOptimal = false;
        }
        else if (ratioDivergence <= 0.20 && Math.Abs(voltageDelta) <= 0.05)
        {
            regime = "OPTIMAL_THERMONEUTRAL_FISCHER_TROPSCH_SYNGAS_REGIME";
            guidance = $"Stack operating near perfect thermoneutral potential ({telemetry.OperatingCellVoltageVolts:F2} V vs V_tn {thermoneutralVoltage:F2} V) with precision H2/CO ratio ({telemetry.MeasuredOutletSyngasH2ToCoRatio:F2}). Ideal for direct e-fuel synthesis.";
        }
        else if (telemetry.OperatingCellVoltageVolts > 1.45)
        {
            regime = "HIGH_EXOTHERMIC_CELL_OVERHEATING_WARNING";
            guidance = $"Operating voltage ({telemetry.OperatingCellVoltageVolts:F2} V) produces excess Joule heating. Increase air/sweep gas cooling flow to prevent local thermal runaway.";
            isOptimal = false;
        }
        else
        {
            regime = "NOMINAL_CO_ELECTROLYSIS_REGIME";
            guidance = $"Stable co-electrolysis active in {thermalMode} mode. Current density is {telemetry.CurrentDensityAmperesPerSquareCm:F2} A/cm^2.";
        }

        return new SolidOxideCoElectrolysisAnalysisResult(
            telemetry.StackId,
            Math.Round(thermoneutralVoltage, 3),
            Math.Round(ratioDivergence, 3),
            thermalMode,
            regime,
            guidance,
            isOptimal
        );
    }
}
