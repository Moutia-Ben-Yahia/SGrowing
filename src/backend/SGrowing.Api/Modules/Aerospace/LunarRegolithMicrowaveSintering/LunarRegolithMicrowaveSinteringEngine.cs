using System;

namespace SGrowing.Api.Modules.Aerospace.LunarRegolithMicrowaveSintering;

public interface ILunarRegolithMicrowaveSinteringEngine
{
    LunarRegolithSinteringResult AnalyzeSinteringProcess(LunarRegolithSinteringTelemetry telemetry);
}

public sealed class LunarRegolithMicrowaveSinteringEngine : ILunarRegolithMicrowaveSinteringEngine
{
    private const double VacuumPermittivity = 8.854187817e-12;
    private const double TheoreticalSolidDensityGPerCm3 = 3.10;

    public LunarRegolithSinteringResult AnalyzeSinteringProcess(LunarRegolithSinteringTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.InitialBulkDensityGPerCm3 <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Initial bulk density must be strictly positive.");
        }

        // Dissipated power density: Pv = 2 * pi * f * eps0 * eps_r * tan_delta * E^2
        double angularFreq = 2.0 * Math.PI * (telemetry.MicrowaveFrequencyGhz * 1e9);
        double dissipatedPowerDensity = angularFreq * VacuumPermittivity * telemetry.RelativeDielectricPermittivity * telemetry.LossTangent
                                        * Math.Pow(telemetry.CavityElectricFieldVPerM, 2.0);

        // Arrhenius-like densification kinetics
        double temperatureKelvin = Math.Max(273.15, telemetry.RegolithCoreTemperatureCelsius + 273.15);
        double kineticRate = 1200.0 * Math.Exp(-14000.0 / temperatureKelvin);
        double densificationProgress = 1.0 - Math.Exp(-kineticRate * Math.Max(0.0, telemetry.SinteringDurationMinutes));

        double densifiedSpan = TheoreticalSolidDensityGPerCm3 - telemetry.InitialBulkDensityGPerCm3;
        double finalDensity = telemetry.InitialBulkDensityGPerCm3 + (densifiedSpan * Math.Clamp(densificationProgress, 0.0, 1.0));
        finalDensity = Math.Min(TheoreticalSolidDensityGPerCm3, Math.Max(telemetry.InitialBulkDensityGPerCm3, finalDensity));

        double residualPorosityFraction = Math.Max(0.0, 1.0 - (finalDensity / TheoreticalSolidDensityGPerCm3));
        double residualPorosityPercent = residualPorosityFraction * 100.0;

        // Compressive strength: sigma_c = sigma_0 * (1 - p)^3.5
        double compressiveStrengthMpa = 140.0 * Math.Pow(1.0 - residualPorosityFraction, 3.5);

        string regime;
        string action;
        bool isStructuralGrade;

        if (telemetry.RegolithCoreTemperatureCelsius > 1250.0 || telemetry.HeatingRateCelsiusPerMin > 120.0)
        {
            regime = "THERMAL_RUNAWAY_WARNING";
            action = "PULSE_MODULATE_MICROWAVE_MAGNETRON_DUTY_CYCLE_TO_40_PERCENT";
            isStructuralGrade = false;
        }
        else if (compressiveStrengthMpa >= 25.0 && finalDensity >= 2.80)
        {
            regime = "SINTERING_OPTIMAL_STRUCTURAL_GRADE";
            action = "MAINTAIN_COOLING_CYCLE_FOR_RADIATION_SHIELDING";
            isStructuralGrade = true;
        }
        else
        {
            regime = "UNDER_SINTERED_LOW_STRENGTH";
            action = "EXTEND_MICROWAVE_SOAK_TIME_AND_INCREASE_POWER";
            isStructuralGrade = false;
        }

        return new LunarRegolithSinteringResult(
            telemetry.BatchBrickId,
            Math.Round(dissipatedPowerDensity, 1),
            Math.Round(finalDensity, 3),
            Math.Round(residualPorosityPercent, 2),
            Math.Round(compressiveStrengthMpa, 2),
            regime,
            action,
            isStructuralGrade,
            DateTimeOffset.UtcNow);
    }
}
