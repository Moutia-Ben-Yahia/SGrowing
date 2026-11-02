namespace SGrowing.Api.Modules.Energy.PemElectrolyzerGasCrossover;

public interface IPemElectrolyzerEngine
{
    PemElectrolyzerEvaluation AnalyzeElectrolyzer(PemElectrolyzerTelemetry telemetry);
}

public class PemElectrolyzerEngine : IPemElectrolyzerEngine
{
    public PemElectrolyzerEvaluation AnalyzeElectrolyzer(PemElectrolyzerTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        double tKelvin = telemetry.OperatingTemperatureCelsius + 273.15;
        // Reversible water splitting potential E_rev ~ 1.23V - 0.00085*(T - 298.15)
        double eRev = 1.229 - 0.00085 * (tKelvin - 298.15);

        // Nernst pressure correction: dE = (R*T / 4F) * ln(P_O2 * P_H2^2)
        double r = 8.314;
        double f = 96485.0;
        double pressureTerm = (r * tKelvin / (4.0 * f)) * Math.Log(Math.Max(1.0, telemetry.OperatingAnodeOxygenPressureBar) * Math.Pow(Math.Max(1.0, telemetry.OperatingCathodeHydrogenPressureBar), 2.0));
        double nernstPotential = eRev + pressureTerm;

        // Overpotentials:
        // Anode OER activation (Tafel equation): eta_anode ~ 0.045 * ln(10 * j)
        double j = Math.Max(0.1, telemetry.OperatingCurrentDensityAmpsPerCm2);
        double etaAnode = 0.28 + 0.055 * Math.Log(j * 5.0);

        // Cathode HER activation: eta_cathode ~ 0.04 + 0.02 * ln(j)
        double etaCathode = 0.05 + 0.025 * Math.Log(j * 2.0);

        // Ohmic loss: membrane resistance + contact resistance
        // Membrane conductivity ~ 0.12 S/cm = 120 mS/cm
        double tMemCm = telemetry.MembraneThicknessMicrons * 1e-4;
        double rMemOhmCm2 = tMemCm / 0.12; // e.g. 125 um -> 0.0125 / 0.12 = 0.104 Ohm.cm2
        double rContactOhmCm2 = telemetry.TitaniumBipolarPlateContactResistanceMilliOhmCm2 * 1e-3;
        double etaOhm = j * (rMemOhmCm2 + rContactOhmCm2);

        double cellVoltage = Math.Clamp(nernstPotential + etaAnode + etaCathode + etaOhm, 1.45, 2.80);

        // Higher Heating Value (HHV) efficiency: 1.482V / cellVoltage * 100
        double hhvEfficiencyPercent = Math.Clamp((1.482 / cellVoltage) * 100.0, 45.0, 95.0);

        // Gas crossover explosion safety margin: 4.0 vol% LEL / actual H2 in O2
        double actualH2InO2 = Math.Max(0.01, telemetry.HydrogenInOxygenAnodeFractionPercent);
        double safetyMargin = 4.0 / actualH2InO2;

        bool isCrossoverSafe = telemetry.HydrogenInOxygenAnodeFractionPercent <= 1.80;
        bool isContactOptimal = telemetry.TitaniumBipolarPlateContactResistanceMilliOhmCm2 <= 25.0;
        bool isMembranePinholeAbsent = telemetry.HydrogenInOxygenAnodeFractionPercent <= 2.20 &&
                                       telemetry.CellVoltageDegradationMicrovoltsPerHour <= 15.0;

        if (telemetry.HydrogenInOxygenAnodeFractionPercent > 1.80)
        {
            regimes.Add("HYDROGEN_IN_OXYGEN_CROSSOVER_EXPLOSIVE_HAZARD");
            mitigations.Add("Ramp down current density or install recombinant Pt-OER catalytic recombining layer to oxidize crossed-over hydrogen into water.");
        }

        if (!isContactOptimal)
        {
            regimes.Add("ANODE_TITANIUM_BIPOLAR_PASSIVATION_RESISTANCE");
            mitigations.Add("Apply conductive protective platinum-group or gold-doped titanium nitride (TiN) coating onto anode porous transport layers.");
        }

        if (telemetry.HydrogenInOxygenAnodeFractionPercent > 2.50 || telemetry.CellVoltageDegradationMicrovoltsPerHour > 15.0)
        {
            regimes.Add("MEMBRANE_PINHOLE_DEGRADATION_THINNING");
            mitigations.Add("Schedule immediate stack replacement or hot-standby switchover before micro-fissure crossover propagates into localized hot spots.");
        }

        bool isElectrolysisReady = isCrossoverSafe && isContactOptimal && isMembranePinholeAbsent && cellVoltage <= 2.10;

        return new PemElectrolyzerEvaluation
        {
            StackUnitId = telemetry.StackUnitId,
            CalculatedOperatingCellVoltageVolts = Math.Round(cellVoltage, 3),
            StackHighHeatingValueEfficiencyPercent = Math.Round(hhvEfficiencyPercent, 1),
            HydrogenCrossoverExplosionSafetyMargin = Math.Round(safetyMargin, 2),
            IsGasCrossoverSafe = isCrossoverSafe,
            IsBipolarPlateContactOptimal = isContactOptimal,
            IsMembranePinholeAbsent = isMembranePinholeAbsent,
            IsHighCurrentElectrolysisReady = isElectrolysisReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
