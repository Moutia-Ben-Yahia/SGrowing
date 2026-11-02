namespace SGrowing.Api.Modules.Energy.SolidOxideFuelCellTriplePhaseBoundary;

public interface ISolidOxideFuelCellEngine
{
    SolidOxideFuelCellEvaluation AnalyzeFuelCell(SolidOxideFuelCellTelemetry telemetry);
}

public class SolidOxideFuelCellEngine : ISolidOxideFuelCellEngine
{
    public SolidOxideFuelCellEvaluation AnalyzeFuelCell(SolidOxideFuelCellTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        double tKelvin = telemetry.OperatingTemperatureCelsius + 273.15;
        // Standard reversible Nernst potential at operating temperature ~ 1.05V - 1.15V
        double standardE0 = 1.25 - 0.00025 * tKelvin;
        double nernstVolts = standardE0 - (8.314 * tKelvin / (4.0 * 96485.0)) * Math.Log(Math.Max(1.0e-3, telemetry.FuelUtilizationPercent / 100.0));
        nernstVolts = Math.Clamp(nernstVolts, 0.90, 1.20);

        // Polarization overpotentials
        // 1. Ohmic overpotential
        double etaOhm = telemetry.CurrentDensityAmpsPerCm2 * telemetry.AreaSpecificResistanceOhmCm2;

        // 2. Activation overpotential at TPB
        double tpbFactor = Math.Max(0.5, telemetry.TriplePhaseBoundaryLengthMicronsPerMicron2);
        double etaAct = (0.065 / Math.Sqrt(tpbFactor)) * Math.Log(1.0 + telemetry.CurrentDensityAmpsPerCm2 * 2.0);

        // 3. Concentration overpotential
        double uFraction = telemetry.FuelUtilizationPercent / 100.0;
        double etaConc = -0.045 * Math.Log(Math.Max(0.01, 1.0 - uFraction * 0.95));

        double operatingVoltage = Math.Max(0.1, nernstVolts - etaOhm - etaAct - etaConc);
        double powerDensityWattsPerCm2 = telemetry.CurrentDensityAmpsPerCm2 * operatingVoltage;

        // Coking risk evaluation: S/C < 2.0 creates high carbon deposition risk
        double cokingRiskIndex = 1.0;
        if (telemetry.SteamToCarbonRatio < 2.0)
        {
            cokingRiskIndex += (2.0 - telemetry.SteamToCarbonRatio) * 6.0;
        }
        if (telemetry.OperatingTemperatureCelsius < 700.0 && telemetry.AnodeInletMethaneFractionPercent > 20.0)
        {
            cokingRiskIndex += 1.8;
        }
        cokingRiskIndex = Math.Clamp(cokingRiskIndex, 0.5, 10.0);

        bool isSteamToCarbonSafe = telemetry.SteamToCarbonRatio >= 2.0;
        bool isTpbIntact = telemetry.TriplePhaseBoundaryLengthMicronsPerMicron2 >= 4.5 && etaAct <= 0.15;
        bool isNiProtected = telemetry.AnodeOxygenPartialPressureAtm <= 1.0e-14;
        bool isHighEfficiency = operatingVoltage >= 0.65 && isSteamToCarbonSafe && isTpbIntact && isNiProtected;

        if (!isSteamToCarbonSafe)
        {
            regimes.Add("ANODE_CARBON_COKING_DEACTIVATION");
            mitigations.Add("Increase steam boiler flow rate to maintain S/C >= 2.2 and suppress Boudouard carbon soot precipitation on Ni catalyst sites.");
        }

        if (!isTpbIntact)
        {
            regimes.Add("TRIPLE_PHASE_BOUNDARY_SINTERING_DEGRADATION");
            mitigations.Add("Apply gadolinium-doped ceria (GDC) barrier layer and optimize thermal cycling ramps to halt Ni-YSZ particle coarsening.");
        }

        if (!isNiProtected)
        {
            regimes.Add("NICKEL_REOXIDATION_VOLUME_FRACTURE_RISK");
            mitigations.Add("Inject emergency forming gas (95% N2 / 5% H2) purge to lower anode PO2 below 10^-15 atm and prevent destructive NiO volumetric swelling.");
        }

        return new SolidOxideFuelCellEvaluation
        {
            StackId = telemetry.StackId,
            CalculatedNernstVoltageVolts = Math.Round(nernstVolts, 3),
            CalculatedOperatingCellVoltageVolts = Math.Round(operatingVoltage, 3),
            StackPowerDensityWattsPerCm2 = Math.Round(powerDensityWattsPerCm2, 3),
            CokingRiskIndex = Math.Round(cokingRiskIndex, 2),
            IsSteamToCarbonRatioSafe = isSteamToCarbonSafe,
            IsTriplePhaseBoundaryIntact = isTpbIntact,
            IsNickelAnodeProtectedFromOxidation = isNiProtected,
            IsStackHighEfficiencyOperating = isHighEfficiency,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
