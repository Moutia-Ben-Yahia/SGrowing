using System;

namespace SGrowing.Api.Modules.Environmental.DacSolidAmineChemisorption;

public interface IDacSolidAmineChemisorptionEngine
{
    DacContactorAnalysisResult AnalyzeContactorCycle(DacContactorTelemetry telemetry);
}

public sealed class DacSolidAmineChemisorptionEngine : IDacSolidAmineChemisorptionEngine
{
    public DacContactorAnalysisResult AnalyzeContactorCycle(DacContactorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.AdsorbentBedMassKg <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Bed mass must be strictly positive.");
        }

        // Maximum equilibrium capacity factoring in moisture-promoted bicarbonate formation
        double rhFraction = Math.Clamp(telemetry.AmbientRelativeHumidityPercent / 60.0, 0.0, 1.0);
        double maxEquilibriumCapacity = 2.4 * (1.0 + (0.25 * rhFraction));

        double saturationPercent = (telemetry.CurrentCo2LoadingMmolPerG / maxEquilibriumCapacity) * 100.0;
        saturationPercent = Math.Clamp(saturationPercent, 0.0, 100.0);

        double co2Loading = Math.Max(0.05, telemetry.CurrentCo2LoadingMmolPerG);
        double waterRatio = telemetry.CurrentWaterLoadingMmolPerG / co2Loading;

        // Desorption specific energy: base chemical (2.1) + sensible heat (1.2) + water vaporisation penalty (1.05 * ratio)
        double desorptionEnergyGjPerTon = 2.1 + 1.2 + (1.05 * waterRatio);

        // Instantaneous capture rate: airflow * 3600 * ppm * (44.01 g/mol / 0.0245 m3/mol) * 0.70 capture efficiency
        double airFlowM3PerHour = Math.Max(0.1, telemetry.AirFlowRateM3PerSec) * 3600.0;
        double co2Fraction = Math.Max(100.0, telemetry.AmbientCo2Ppm) * 1e-6;
        double instantaneousCaptureRateKgPerHour = airFlowM3PerHour * co2Fraction * (44.01 / 24.5) * 0.70;

        string state;
        string recommendation;
        bool isDesorptionRequired;

        if (saturationPercent >= 85.0 || telemetry.ContactorPressureDropKpa > 1.8)
        {
            state = "DESORPTION_CYCLE_DUE";
            recommendation = "INITIATE_LOW_PRESSURE_STEAM_DESORPTION_SWING_AT_95_CELSIUS";
            isDesorptionRequired = true;
        }
        else if (waterRatio > 3.0 && desorptionEnergyGjPerTon > 6.5)
        {
            state = "EXCESSIVE_MOISTURE_PARASITIC_PENALTY";
            recommendation = "ENGAGE_PRE_DEHUMIDIFICATION_AIR_BYPASS";
            isDesorptionRequired = false;
        }
        else
        {
            state = "ADSORPTION_PHASE_OPTIMAL";
            recommendation = "CONTINUE_AMBIENT_AIR_CONTACTOR_BLOWER_SWEEP";
            isDesorptionRequired = false;
        }

        return new DacContactorAnalysisResult(
            telemetry.ContactorModuleId,
            Math.Round(saturationPercent, 2),
            Math.Round(waterRatio, 3),
            Math.Round(desorptionEnergyGjPerTon, 2),
            Math.Round(instantaneousCaptureRateKgPerHour, 2),
            state,
            recommendation,
            isDesorptionRequired,
            DateTimeOffset.UtcNow);
    }
}
