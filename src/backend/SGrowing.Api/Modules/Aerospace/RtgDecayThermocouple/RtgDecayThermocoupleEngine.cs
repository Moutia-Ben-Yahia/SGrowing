namespace SGrowing.Api.Modules.Aerospace.RtgDecayThermocouple;

public interface IRtgDecayThermocoupleEngine
{
    RtgDecayAssessment EvaluateRtgPower(RtgEvaluationRequest request);
}

public class RtgDecayThermocoupleEngine : IRtgDecayThermocoupleEngine
{
    // Pu-238 alpha decay: t_half = 87.7 years -> lambda = ln(2) / 87.7
    private const double Pu238DecayConstantPerYear = 0.0079036;

    // SiGe/Skutterudite thermocouple solid-state degradation (sublimation, dopant precipitation)
    private const double ThermocoupleDegradationRatePerYear = 0.00750;

    public RtgDecayAssessment EvaluateRtgPower(RtgEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var lim = request.Limits;

        double years = Math.Max(0.0, tel.YearsSinceFuelEncapsulation);

        // 1. Current radioactive decay thermal power: P_th(t) = P_th(0) * exp(-lambda * t)
        double thermalDecayFactor = Math.Exp(-Pu238DecayConstantPerYear * years);
        double currentThermalPowerWatts = tel.InitialThermalPowerWatts * thermalDecayFactor;

        // 2. Thermocouple conversion efficiency:
        // Initial efficiency eta_0 = P_e(0) / P_th(0)
        double initialEfficiency = tel.InitialThermalPowerWatts > 0
            ? (tel.InitialElectricalPowerWatts / tel.InitialThermalPowerWatts)
            : 0.065;

        // Solid-state degradation factor over decades
        double thermocoupleDegradationFactor = Math.Exp(-ThermocoupleDegradationRatePerYear * years);
        double currentEfficiency = initialEfficiency * thermocoupleDegradationFactor;
        double currentEfficiencyPercent = currentEfficiency * 100.0;

        // 3. Current available electrical power: P_e(t) = P_th(t) * eta(t)
        double currentElectricalPowerWatts = currentThermalPowerWatts * currentEfficiency;

        // 4. Projected electrical power 10 years into the future:
        double tenYearCombinedDecay = Math.Exp(-(Pu238DecayConstantPerYear + ThermocoupleDegradationRatePerYear) * 10.0);
        double projectedPowerTenYears = currentElectricalPowerWatts * tenYearCombinedDecay;

        // 5. Margin and load-shedding analysis:
        double netPowerMargin = currentElectricalPowerWatts - tel.SpacecraftTotalLoadDemandWatts;
        bool loadSheddingRequired = netPowerMargin < 0.0 || currentElectricalPowerWatts <= lim.MinAllowableBusPowerWatts;
        bool marginalWarning = currentElectricalPowerWatts <= lim.CriticalLoadSheddingThresholdWatts && !loadSheddingRequired;

        string status;
        string directive;

        if (loadSheddingRequired)
        {
            status = "NEGATIVE_POWER_MARGIN_CRITICAL";
            directive = $"EMERGENCY_LOAD_SHEDDING: Available RTG output ({currentElectricalPowerWatts:F1} We) cannot support load demand ({tel.SpacecraftTotalLoadDemandWatts:F1} W). Shed non-essential instrument bay heaters and throttle X-band transmitter power by 40% to prevent bus brownout.";
        }
        else if (marginalWarning)
        {
            status = "MARGINAL_POWER_HEADROOM_WARNING";
            directive = $"PREPARE_CONSERVATION_MODE: Power headroom is narrow (+{netPowerMargin:F1} We margin). Projected output in 10 years will drop to {projectedPowerTenYears:F1} We. Queue instrument cycle interleaving for upcoming flyby.";
        }
        else
        {
            status = "POWER_BUDGET_SECURE_NOMINAL";
            directive = $"POWER_NOMINAL: Electrical bus stable with +{netPowerMargin:F1} We excess margin. Pu-238 radiogenic thermal source and {tel.ThermocoupleMaterial} thermocouples performing to multi-decade deep-space flight profiles.";
        }

        return new RtgDecayAssessment(
            RtgId: tel.RtgId,
            CurrentThermalPowerWatts: Math.Round(currentThermalPowerWatts, 1),
            CurrentAvailableElectricalPowerWatts: Math.Round(currentElectricalPowerWatts, 1),
            ThermocoupleConversionEfficiencyPercent: Math.Round(currentEfficiencyPercent, 2),
            ProjectedPowerWattsInTenYears: Math.Round(projectedPowerTenYears, 1),
            LoadSheddingMandatoryDetected: loadSheddingRequired,
            PowerMarginStatus: status,
            PowerManagementDirective: directive
        );
    }
}
