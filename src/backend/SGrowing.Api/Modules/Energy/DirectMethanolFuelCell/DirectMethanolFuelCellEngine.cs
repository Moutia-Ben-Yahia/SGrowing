using System;

namespace SGrowing.Api.Modules.Energy.DirectMethanolFuelCell;

public sealed class DirectMethanolFuelCellEngine : IDirectMethanolFuelCellEngine
{
    public DmfcAnalysisResult AnalyzeFuelCell(DmfcTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var cM = Math.Max(0.1, telemetry.AnodeMethanolConcentrationMolarity);
        var tempC = Math.Max(10.0, telemetry.OperatingTemperatureCelsius);
        var currentDensityMaPerCm2 = Math.Max(1.0, telemetry.OperatingCurrentDensityMilliAmpsPerCm2);
        var memThicknessUm = Math.Max(20.0, telemetry.MembraneThicknessMicrometers);
        var activeAreaCm2 = Math.Max(1.0, telemetry.TotalCellActiveAreaCm2);

        // Methanol crossover flux J_cross in mmol / (m^2 * s)
        var tempFactor = 1.0 + Math.Max(0.0, (tempC - 60.0) / 40.0);
        var thicknessFactor = 125.0 / memThicknessUm;
        var concentrationFactor = cM / 1.5;
        var crossoverFlux = 45.0 * concentrationFactor * thicknessFactor * tempFactor;

        // Mixed potential overpotential loss at cathode from methanol oxidation
        var crossoverOverpotentialVolts = 0.080 * (crossoverFlux / 50.0);

        // Ohmic and mass transport losses
        var ohmicLossVolts = (currentDensityMaPerCm2 * 0.0018);

        // Operating cell voltage
        var ocv = Math.Clamp(telemetry.OpenCircuitVoltageVolts, 0.40, 1.25);
        var cellVoltage = Math.Clamp(ocv - crossoverOverpotentialVolts - ohmicLossVolts, 0.05, 1.20);

        // Electric power density: P_D = V_cell * I_density [mW/cm^2]
        var powerDensityMwPerCm2 = cellVoltage * currentDensityMaPerCm2;

        // Total stack electric power: P_total = P_D * Area [Watts]
        var totalPowerWatts = (powerDensityMwPerCm2 * activeAreaCm2) / 1000.0;

        // Fuel utilization efficiency: accounts for parasitic crossover
        var fuelEfficiencyPercent = Math.Clamp(95.0 - (crossoverFlux * 0.45), 15.0, 95.0);

        string regime;
        string guidance;
        bool isNominal;

        if (cM > 3.0 || crossoverFlux > 110.0)
        {
            regime = "SEVERE_METHANOL_CROSSOVER_CATALYST_POISONING";
            guidance = "Methanol crossover flux exceeds 110 mmol/(m^2*s) causing cathode Pt catalyst poisoning and mixed potential drop. Dilute anode feed to < 2.0 M.";
            isNominal = false;
        }
        else if (tempC < 45.0)
        {
            regime = "SUB_OPTIMAL_TEMPERATURE_ACTIVATION_LOSS";
            guidance = "Cell operating temperature below 45 C. High anode methanol electro-oxidation activation barrier. Engage coolant loop preheater.";
            isNominal = false;
        }
        else if (powerDensityMwPerCm2 < 25.0)
        {
            regime = "LOW_ELECTRIC_POWER_DENSITY_WARNING";
            guidance = "Cell electric power density below 25 mW/cm^2. Increase cathode air stoichiometric ratio to purge water flooding.";
            isNominal = false;
        }
        else
        {
            regime = "OPTIMAL_DIRECT_METHANOL_ELECTROCHEMICAL_REGIME";
            guidance = "Anode methanol oxidation kinetics, crossover barrier, and cathode air diffusion in optimal balance. High fuel utilization confirmed.";
            isNominal = true;
        }

        return new DmfcAnalysisResult(
            CellStackId: telemetry.CellStackId,
            MethanolCrossoverFluxMillimolesPerM2Sec: crossoverFlux,
            FuelUtilizationEfficiencyPercent: fuelEfficiencyPercent,
            MeasuredOperatingCellVoltageVolts: cellVoltage,
            ElectricPowerDensityMilliWattsPerCm2: powerDensityMwPerCm2,
            TotalStackElectricPowerWatts: totalPowerWatts,
            DmfcOperatingRegime: regime,
            FuelCellControlGuidance: guidance,
            IsFuelCellPerformanceNominal: isNominal
        );
    }
}
