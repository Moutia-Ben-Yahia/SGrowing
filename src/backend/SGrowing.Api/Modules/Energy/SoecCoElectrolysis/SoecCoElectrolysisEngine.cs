namespace SGrowing.Api.Modules.Energy.SoecCoElectrolysis;

public class SoecCoElectrolysisEngine
{
    private const double GasConstantR = 8.314; // J/(mol*K)
    private const double ThermoneutralVoltage = 1.287; // Volts

    public SoecCoElectrolysisAnalysisResult AnalyzeStack(SoecCoElectrolysisTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.OperatingTemperatureCelsius < 500.0 || telemetry.OperatingTemperatureCelsius > 1100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Operating temperature must be between 500 and 1100 °C.");
        }

        if (telemetry.InletCo2FlowRateNlMin <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Inlet CO2 flow rate must be positive.");
        }

        if (telemetry.InletSteamFlowRateNlMin <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Inlet steam flow rate must be positive.");
        }

        double tempKelvin = telemetry.OperatingTemperatureCelsius + 273.15;
        double steamToCarbonRatio = telemetry.InletSteamFlowRateNlMin / telemetry.InletCo2FlowRateNlMin;

        // Thermal operating regime relative to thermoneutral voltage (1.287 V)
        string thermalOperatingRegime;
        if (Math.Abs(telemetry.CellVoltageVolts - ThermoneutralVoltage) <= 0.02)
        {
            thermalOperatingRegime = "THERMONEUTRAL_EQUILIBRIUM";
        }
        else if (telemetry.CellVoltageVolts < ThermoneutralVoltage)
        {
            thermalOperatingRegime = "ENDOTHERMIC_HEAT_ABSORBING";
        }
        else
        {
            thermalOperatingRegime = "EXOTHERMIC_JOULE_HEATING";
        }

        // Boudouard Reaction Gibbs Free Energy & Equilibrium Constant:
        // 2 CO <=> C + CO2
        // dG_B(T) ~ -172000 + 175 * T [J/mol]
        double deltaGBoudouard = -172000.0 + (175.0 * tempKelvin);
        double kBoudouard = Math.Exp(-deltaGBoudouard / (GasConstantR * tempKelvin));

        // Carbon Activity aC = (p_CO^2) / (p_CO2 * K_B)
        double pCo = Math.Max(telemetry.CoConcentrationMolarFraction, 1e-4);
        double pCo2 = Math.Max(telemetry.Co2ConcentrationMolarFraction, 1e-4);
        double carbonActivity = (Math.Pow(pCo, 2)) / (pCo2 * kBoudouard);

        bool isCokingRisk = carbonActivity >= 1.0 || steamToCarbonRatio < 1.8;

        string operatingState;
        string recommendedAction;
        string summary;

        if (carbonActivity >= 2.0 || (carbonActivity >= 1.0 && steamToCarbonRatio < 1.5))
        {
            operatingState = "CRITICAL_COKING_DELAMINATION_ALERT";
            recommendedAction = "EMERGENCY_CATHODE_POTENTIAL_MODULATION";
            summary = $"CRITICAL ALERT: Severe carbon coking activity (aC={carbonActivity:F2}) on Ni-YSZ cathode! S/C ratio collapsed to {steamToCarbonRatio:F2}. Modulating cathode bias and throttling CO2 to prevent irreversible microstructural delamination.";
        }
        else if (isCokingRisk)
        {
            operatingState = "COKING_WARNING_STEAM_STARVATION";
            recommendedAction = "BOOST_STEAM_SWEEP_GAS_FLOW";
            summary = $"WARNING: Elevated carbon deposition activity (aC={carbonActivity:F2}) with steam/carbon ratio S/C={steamToCarbonRatio:F2}. Command 35% steam flow boost to suppress Boudouard carbon precipitation.";
        }
        else
        {
            operatingState = "NOMINAL_SYNGAS_PRODUCTION";
            recommendedAction = "MAINTAIN_NOMINAL_INJECTION";
            summary = $"Nominal high-temperature SOEC co-electrolysis at {telemetry.OperatingTemperatureCelsius:F0}°C ({thermalOperatingRegime}). Syngas production stable with negligible carbon deposition activity (aC={carbonActivity:F3}).";
        }

        return new SoecCoElectrolysisAnalysisResult(
            telemetry.StackId,
            Math.Round(steamToCarbonRatio, 2),
            ThermoneutralVoltage,
            thermalOperatingRegime,
            Math.Round(carbonActivity, 3),
            isCokingRisk,
            operatingState,
            recommendedAction,
            summary);
    }
}
