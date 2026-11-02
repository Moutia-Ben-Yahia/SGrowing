namespace SGrowing.Api.Modules.Energy.DirectAirCaptureTvsaSorbent;

public interface IDirectAirCaptureTvsaEngine
{
    DirectAirCaptureTvsaEvaluation AnalyzeDacTrain(DirectAirCaptureTvsaTelemetry telemetry);
}

public class DirectAirCaptureTvsaEngine : IDirectAirCaptureTvsaEngine
{
    public DirectAirCaptureTvsaEvaluation AnalyzeDacTrain(DirectAirCaptureTvsaTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Working capacity = q_ads - q_des
        double workingCapacity = Math.Max(0.0, telemetry.AdsorptionCapacityMmolPerGram - telemetry.DesorptionResidualCapacityMmolPerGram);

        // Total specific energy in GJ/t CO2
        double totalSpecificEnergy = telemetry.SpecificThermalEnergyGigaJoulesPerTonCo2 + telemetry.SpecificElectricalEnergyGigaJoulesPerTonCo2;

        // Estimated sorbent lifespan cycles (cycles until 20% loss in capacity)
        double degRate = Math.Max(1e-4, telemetry.AmineDegradationRatePercentPerCycle);
        double lifespanCycles = 20.0 / degRate;

        bool isCapacityAdequate = workingCapacity >= 1.20;
        bool isEnergyOptimal = telemetry.SpecificThermalEnergyGigaJoulesPerTonCo2 <= 5.8 &&
                               telemetry.SpecificElectricalEnergyGigaJoulesPerTonCo2 <= 1.4 &&
                               telemetry.ContactorBedPressureDropPascals <= 180.0;
        bool isOxidationSuppressed = !(telemetry.DesorptionTemperatureCelsius > 105.0 && telemetry.OxygenConcentrationDuringDesorptionVolPercent > 0.50);

        if (!isOxidationSuppressed)
        {
            regimes.Add("AMINE_SORBENT_THERMAL_OXIDATIVE_DEGRADATION");
            mitigations.Add("Purge contactor vessel with low-pressure saturated steam or nitrogen inert sweep gas prior to thermal desorption step.");
        }

        if (telemetry.ContactorBedPressureDropPascals > 180.0)
        {
            regimes.Add("FAN_PRESSURE_DROP_PARASITIC_POWER_SPIKE");
            mitigations.Add("Inspect and de-dust monolithic honeycomb flow channels to mitigate aerodynamic parasitic resistance and fan kilowatt-hour consumption.");
        }

        if (workingCapacity < 0.90)
        {
            regimes.Add("DESORPTION_WORKING_CAPACITY_DEPLETION");
            mitigations.Add("Deepen vacuum level below 0.10 bar or run thermal re-activation cycle to strip irreversibly chemisorbed species.");
        }

        bool isCertified = isCapacityAdequate && isEnergyOptimal && isOxidationSuppressed && totalSpecificEnergy <= 7.2;

        return new DirectAirCaptureTvsaEvaluation
        {
            ContactorTrainId = telemetry.ContactorTrainId,
            WorkingCapacityMmolPerGram = Math.Round(workingCapacity, 3),
            TotalSpecificEnergyGigaJoulesPerTonCo2 = Math.Round(totalSpecificEnergy, 2),
            ProjectedSorbentCycleLifespan = Math.Round(lifespanCycles, 0),
            IsWorkingCapacityAdequate = isCapacityAdequate,
            IsEnergeticPerformanceOptimal = isEnergyOptimal,
            IsOxidativeDegradationSuppressed = isOxidationSuppressed,
            IsTvsaDecarbonizationCertified = isCertified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
