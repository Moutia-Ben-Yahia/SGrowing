namespace SGrowing.Api.Modules.Energy.SoecSteamElectrolysisThermalNeutral;

public interface ISoecSteamElectrolysisEngine
{
    SoecSteamElectrolysisEvaluation AnalyzeStack(SoecSteamElectrolysisTelemetry telemetry);
}

public class SoecSteamElectrolysisEngine : ISoecSteamElectrolysisEngine
{
    public SoecSteamElectrolysisEvaluation AnalyzeStack(SoecSteamElectrolysisTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Thermal neutral voltage deviation = V_cell - V_tn
        double deltaVtn = telemetry.OperatingCellVoltageVolts - telemetry.ThermalNeutralVoltageVolts;

        // Hydrogen production rate: j (A/cm2) * 10000 cm2/m2 / (2 * F) * 3600 s/hr * 0.022414 Nm3/mol
        // F = 96485.3 C/mol
        // Rate = j * 10000 * 3600 * 0.022414 / (2 * 96485.3) * (eta_F / 100) ~ j * 4.1813 * (eta_F / 100)
        double rate = telemetry.CurrentDensityAmperesPerCm2 * 4.1813 * (telemetry.FaradayEfficiencyPercent / 100.0);

        bool isThermalNeutral = Math.Abs(deltaVtn) <= 0.065;
        bool isSteamOk = telemetry.SteamUtilizationPercent <= 85.0;
        bool isDelaminationAverted = telemetry.OxygenPartialPressureAtElectrodeBar <= 120.0;

        if (telemetry.OperatingCellVoltageVolts > 1.36)
        {
            regimes.Add("SOEC_EXOTHERMIC_THERMAL_RUNAWAY");
            mitigations.Add("Increase steam sweep flow rate to absorb excess Joule heat and throttle current density to bring cell voltage into 1.28 - 1.30 V thermal-neutral band.");
        }

        if (!isSteamOk)
        {
            regimes.Add("NICKEL_REOXIDATION_STEAM_STARVATION");
            mitigations.Add("Reduce steam utilization to < 80% and enrich cathode feed gas with 10% H2 recycle to prevent nickel fuel electrode re-oxidation to NiO.");
        }

        if (!isDelaminationAverted)
        {
            regimes.Add("OXYGEN_ELECTRODE_DELAMINATION_OVERPOTENTIAL");
            mitigations.Add("Enlarge oxygen electrode barrier layer porosity (CeO2/YSZ interface) to facilitate high-pressure O2 desorption and prevent acoustic blistering.");
        }

        bool isCertified = isThermalNeutral && isSteamOk && isDelaminationAverted &&
                           telemetry.CurrentDensityAmperesPerCm2 >= 0.80 &&
                           telemetry.AreaSpecificResistanceOhmCm2 <= 0.32;

        return new SoecSteamElectrolysisEvaluation
        {
            ElectrolyzerStackUid = telemetry.ElectrolyzerStackUid,
            ThermalNeutralVoltageDeviationVolts = Math.Round(deltaVtn, 4),
            HydrogenProductionRateNm3PerHourPerM2 = Math.Round(rate, 2),
            IsOperatingInThermalNeutralBand = isThermalNeutral,
            IsSteamStarvationAverted = isSteamOk,
            IsElectrodeDelaminationAverted = isDelaminationAverted,
            IsSoecElectrolyzerOperationCertified = isCertified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
