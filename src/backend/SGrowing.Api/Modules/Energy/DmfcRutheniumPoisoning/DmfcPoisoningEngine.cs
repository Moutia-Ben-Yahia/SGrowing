namespace SGrowing.Api.Modules.Energy.DmfcRutheniumPoisoning;

public interface IDmfcPoisoningEngine
{
    DmfcPoisoningMitigationPlan EvaluateCellHealth(DmfcEvaluationRequest request);
}

public class DmfcPoisoningEngine : IDmfcPoisoningEngine
{
    public DmfcPoisoningMitigationPlan EvaluateCellHealth(DmfcEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var thresh = request.Thresholds;

        // 1. Electrical power density in mW/cm^2: P = V_cell * J_cell
        double powerDensityMilliWattsPerCm2 = tel.CellOperatingVoltageVolts * tel.CurrentDensityMilliAmpsPerCm2;

        // 2. Faradaic fuel utilization efficiency accounting for methanol crossover:
        // eta_fuel = J_operating / (J_operating + J_crossover)
        double totalFuelConsumptionCurrent = tel.CurrentDensityMilliAmpsPerCm2 + tel.MethanolCrossoverCurrentDensityMilliAmpsPerCm2;
        double fuelUtilizationPercent = totalFuelConsumptionCurrent > 0
            ? Math.Clamp(100.0 * (tel.CurrentDensityMilliAmpsPerCm2 / totalFuelConsumptionCurrent), 15.0, 95.0)
            : 50.0;

        // 3. Operational degradation criteria:
        bool coPoisoningCritical = tel.CarbonMonoxideCoverageFraction >= thresh.MaxAllowableCoCoverageFraction;
        bool crossoverCritical = tel.MethanolCrossoverCurrentDensityMilliAmpsPerCm2 >= thresh.MaxAllowableCrossoverCurrentMilliAmpsPerCm2;

        string regime;
        string directive;

        if (coPoisoningCritical)
        {
            regime = "ANODE_CO_POISONING_STARVATION";
            directive = $"APPLY_STRIPPING_PULSE: Anode CO intermediate coverage ({tel.CarbonMonoxideCoverageFraction * 100:F0}%) exceeds {thresh.MaxAllowableCoCoverageFraction * 100:F0}% limit. Inject a {thresh.AnodeElectrochemicalStrippingPulsePotentialVolts:F2}V vs RHE stripping pulse for 500ms to oxidatively desorb CO from Pt-Ru catalytic sites.";
        }
        else if (crossoverCritical)
        {
            regime = "CATHODE_CROSSOVER_DEPOLARIZATION";
            directive = $"DILUTE_METHANOL_FEED: Methanol crossover ({tel.MethanolCrossoverCurrentDensityMilliAmpsPerCm2:F1} mA/cm2) degrades cathode potential. Reduce methanol concentration from {tel.MethanolFeedConcentrationMolar:F1}M to 0.75M to suppress membrane permeation.";
        }
        else
        {
            regime = "BIFUNCTIONAL_CATALYSIS_OPTIMAL";
            directive = $"NOMINAL_CELL_OPERATION: Ruthenium-promoted water activation actively desorbs CO intermediates. Fuel utilization efficiency ({fuelUtilizationPercent:F1}%) and power density ({powerDensityMilliWattsPerCm2:F1} mW/cm2) optimal.";
        }

        return new DmfcPoisoningMitigationPlan(
            StackId: tel.StackId,
            StackElectricalPowerDensityMilliWattsPerCm2: Math.Round(powerDensityMilliWattsPerCm2, 2),
            FuelUtilizationEfficiencyPercent: Math.Round(fuelUtilizationPercent, 1),
            AnodeCoPoisoningCriticalDetected: coPoisoningCritical,
            ExcessiveMethanolCrossoverDetected: crossoverCritical,
            CatalyticRegime: regime,
            RecommendedElectrochemicalDirective: directive
        );
    }
}
