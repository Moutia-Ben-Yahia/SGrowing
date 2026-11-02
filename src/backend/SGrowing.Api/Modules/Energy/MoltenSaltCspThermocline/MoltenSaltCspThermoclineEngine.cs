namespace SGrowing.Api.Modules.Energy.MoltenSaltCspThermocline;

public interface IMoltenSaltCspThermoclineEngine
{
    ThermoclineStratificationPlan EvaluateThermocline(ThermoclineEvaluationRequest request);
}

public class MoltenSaltCspThermoclineEngine : IMoltenSaltCspThermoclineEngine
{
    private const double SaltDensityKgPerM3 = 1800.0;
    private const double SaltSpecificHeatJPerKgK = 1500.0; // Solar Salt (60% NaNO3, 40% KNO3)

    public ThermoclineStratificationPlan EvaluateThermocline(ThermoclineEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var lim = request.Limits;

        // 1. Tank geometry and total salt mass
        double radius = tel.TankDiameterMeters / 2.0;
        double volumeM3 = Math.PI * Math.Pow(radius, 2.0) * tel.TankHeightMeters;
        double totalSaltMassKg = volumeM3 * SaltDensityKgPerM3;

        // 2. Maximum thermal capacity: Q = m * Cp * Delta T in MWh_th
        double deltaT = Math.Max(10.0, tel.HotZoneTemperatureCelsius - tel.ColdZoneTemperatureCelsius);
        double maxThermalCapacityJoules = totalSaltMassKg * SaltSpecificHeatJPerKgK * deltaT;
        double maxThermalCapacityMwh = maxThermalCapacityJoules / (3600.0 * 1e6);

        // 3. Thermocline thickness effect on effective capacity utilization:
        // A wider thermocline consumes usable hot and cold working zones
        double thermoclineHeightFraction = Math.Clamp(tel.MeasuredThermoclineThicknessMeters / tel.TankHeightMeters, 0.05, 0.60);
        double capacityUtilizationPercent = Math.Clamp(100.0 * (1.0 - (thermoclineHeightFraction * 1.25)), 35.0, 96.0);
        double actualStoredMwh = maxThermalCapacityMwh * (capacityUtilizationPercent / 100.0);

        // 4. Parasitic thermal loss to ambient:
        // Surface area = 2 * pi * r * h + pi * r^2
        double surfaceAreaM2 = (2.0 * Math.PI * radius * tel.TankHeightMeters) + (Math.PI * Math.Pow(radius, 2.0));
        double meanSaltTemp = (tel.HotZoneTemperatureCelsius + tel.ColdZoneTemperatureCelsius) / 2.0;
        double thermalLossKw = surfaceAreaM2 * 0.025 * Math.Max(5.0, meanSaltTemp - tel.AmbientAirTemperatureCelsius);

        // 5. Operational risk evaluations:
        bool freezeRisk = tel.ColdZoneTemperatureCelsius <= (tel.MinColdHeadSafetyLimitCelsius + 10.0);
        bool thermoclineDegraded = tel.MeasuredThermoclineThicknessMeters >= lim.MaxAllowableThermoclineThicknessMeters;

        string status;
        string directive;

        if (freezeRisk)
        {
            status = "CRYSTALLIZATION_FREEZE_PRECURSOR";
            directive = $"ENGAGE_AUX_HEATING: Cold salt temperature ({tel.ColdZoneTemperatureCelsius:F1} C) is near salt freezing point ({tel.MinColdHeadSafetyLimitCelsius:F1} C). Fire electric trace heaters and divert hot salt bypass to prevent irreversible pipe clogging.";
        }
        else if (thermoclineDegraded)
        {
            status = "THERMOCLINE_DEGRADATION_MIXING";
            directive = $"THROTTLE_MASS_FLOW: Thermocline thickness ({tel.MeasuredThermoclineThicknessMeters:F1} m) exceeds {lim.MaxAllowableThermoclineThicknessMeters:F1} m limit due to buoyancy dispersion. Reduce mass flow rate to {tel.SaltMassFlowRateKgPerSec * 0.6:F0} kg/s to re-establish stratified buoyancy gradient.";
        }
        else
        {
            status = "SHARP_STRATIFIED_STORAGE_HEALTHY";
            directive = $"NOMINAL_DISPATCH: Thermocline gradient sharp ({tel.MeasuredThermoclineThicknessMeters:F1} m). Thermal capacity utilization optimal at {capacityUtilizationPercent:F1}%. Ready for dispatch to steam turbine power block.";
        }

        return new ThermoclineStratificationPlan(
            TankId: tel.TankId,
            StoredThermalEnergyMegaWattHours: Math.Round(actualStoredMwh, 1),
            EffectiveCapacityUtilizationPercent: Math.Round(capacityUtilizationPercent, 1),
            ThermalLossRateKiloWatts: Math.Round(thermalLossKw, 1),
            ThermoclineDegradationDetected: thermoclineDegraded,
            FreezePrecursorRiskDetected: freezeRisk,
            StratificationStatus: status,
            RecommendedFlowAdjustmentDirective: directive
        );
    }
}
