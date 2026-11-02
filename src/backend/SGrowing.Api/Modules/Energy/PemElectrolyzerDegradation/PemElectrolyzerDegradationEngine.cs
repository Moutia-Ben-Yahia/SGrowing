namespace SGrowing.Api.Modules.Energy.PemElectrolyzerDegradation;

public interface IPemElectrolyzerDegradationEngine
{
    PemDegradationAssessment AssessDegradation(PemElectrolyzerEvaluationRequest request);
}

public class PemElectrolyzerDegradationEngine : IPemElectrolyzerDegradationEngine
{
    private const double FaradayConstant = 96485.33; // C/mol
    private const double MolarVolumeGasNm3PerMol = 0.022414; // Nm^3 / mol at standard condition

    public PemDegradationAssessment AssessDegradation(PemElectrolyzerEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var thresh = request.Thresholds;

        // Active geometric cell area assumed 500 cm^2
        double activeAreaCm2 = 500.0;
        double totalCurrentAmperes = tel.CurrentDensityAmpsPerCm2 * activeAreaCm2;

        // Faraday efficiency accounts for hydrogen parasitic crossover permeation
        // crossover current density approx j_cross ~ 0.015 A/cm^2 at 30 bar
        double crossoverFraction = Math.Min(0.08, (tel.HydrogenInOxygenPercent / 100.0) * 1.5);
        double faradayEfficiencyPercent = Math.Clamp(100.0 * (1.0 - crossoverFraction), 88.0, 99.5);

        // Production rate in Nm3/h: (I * N_cells * eta_F / (2 * F)) * 3600 * 0.022414
        double hydrogenNm3PerHour = (totalCurrentAmperes * tel.CellCount * (faradayEfficiencyPercent / 100.0) / (2.0 * FaradayConstant))
                                    * 3600.0 * MolarVolumeGasNm3PerMol;

        // Approximate ohmic area-specific resistance: R_ohm ~ (V_cell - V_thermo - eta_act) / j
        double thermoVoltage = 1.229 - (0.00085 * (tel.OperatingTemperatureCelsius + 273.15 - 298.15));
        double overpotentialTotal = Math.Max(0.1, tel.CellVoltageVolts - thermoVoltage);
        double ohmicResistanceMilliOhmCm2 = (overpotentialTotal * 0.45 / Math.Max(0.1, tel.CurrentDensityAmpsPerCm2)) * 1000.0;

        // Degradation health analysis:
        bool crossoverHazard = tel.HydrogenInOxygenPercent >= thresh.MaxAllowableHydrogenInOxygenPercent;
        bool voltageWarning = tel.CellVoltageVolts >= thresh.MaxAllowableCellVoltageVolts;
        bool ferHigh = tel.FluorineEmissionRateMicrogramsPerCm2Hour >= thresh.MaxAllowableFluorineEmissionRate;

        // Remaining life prediction:
        double degradationFactor = Math.Max(1.0, (tel.FluorineEmissionRateMicrogramsPerCm2Hour / 0.12) * (tel.CellVoltageVolts / 1.90));
        double remainingMembraneHours = Math.Max(0.0, (50000.0 - tel.CumulativeOperatingHours) / degradationFactor);

        string status;
        string directive;

        if (crossoverHazard)
        {
            status = "CRITICAL_CROSSOVER_SHUTDOWN";
            directive = "EMERGENCY_CATHODE_PURGE: Hydrogen crossover in oxygen exceeded 1.8% LEL threshold. Interrupt stack current immediately, trigger high-pressure nitrogen sweep across both compartments, and isolate hydrogen buffer storage.";
        }
        else if (ferHigh || voltageWarning)
        {
            status = "ACCELERATED_MEMBRANE_THINNING";
            directive = "THROTTLE_CURRENT_AND_COOL: High fluorine emission or elevated overpotential detected. Lower current density to 1.2 A/cm2, increase deionized water circulation rate, and schedule catalyst coated membrane (CCM) refurbishment.";
        }
        else
        {
            status = "NOMINAL_ELECTROLYSIS_HEALTH";
            directive = "CONTINUE_DYNAMIC_DISPATCH: Stack impedance, fluorine emission rate, and gas purity optimal for MW-scale green hydrogen production with high Faraday yield.";
        }

        return new PemDegradationAssessment(
            StackId: tel.StackId,
            FaradayEfficiencyPercent: Math.Round(faradayEfficiencyPercent, 2),
            HydrogenProductionRateNm3PerHour: Math.Round(hydrogenNm3PerHour, 2),
            OhmicResistanceMilliOhmCm2: Math.Round(ohmicResistanceMilliOhmCm2, 1),
            EstimatedRemainingMembraneLifeHours: Math.Round(remainingMembraneHours, 0),
            FlammabilityCrossoverRiskDetected: crossoverHazard,
            StackDegradationStatus: status,
            RecommendedMitigationDirective: directive
        );
    }
}
