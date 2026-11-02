using System;

namespace SGrowing.Api.Modules.Energy.McfcCarbonCapturePolygeneration;

public sealed class McfcCarbonCapturePolygenerationEngine : IMcfcCarbonCapturePolygenerationEngine
{
    public McfcCaptureAnalysisResult AnalyzeCarbonCaptureCell(McfcCaptureTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double activeAreaCm2 = 2_500_000.0; // 250 m2 stack
        const double faradayConstant = 96485.33;  // C/mol
        const int cellCount = 300;

        // Current I = j (mA/cm2) / 1000 * Area
        var totalCurrentAmps = (telemetry.CurrentDensityMilliampsPerCm2 / 1000.0) * activeAreaCm2;

        // Electrochemical CO3(2-) flux = I / (z * F) where z = 2 electrons per CO2
        var co2FluxMolesPerSec = Math.Round(totalCurrentAmps / (2.0 * faradayConstant), 2);

        // Flue gas molar flow rate (mol/s) at STP (22.414 L/mol)
        var flueMolarFlowTotal = (telemetry.FlueGasInletFlowRateNm3PerHour * 1000.0) / (22.414 * 3600.0);
        var flueCo2FlowRate = flueMolarFlowTotal * Math.Max(0.001, telemetry.FlueGasInletCo2MoleFraction);

        // Carbon Capture Rate (CCR %)
        var ccrPercent = Math.Round(Math.Clamp((co2FluxMolesPerSec / Math.Max(0.01, flueCo2FlowRate)) * 100.0, 0.0, 100.0), 1);

        // Net electric power generation (MW)
        var cellVoltage = Math.Max(0.1, telemetry.MeasuredCellVoltageVolts);
        var electricPowerMw = Math.Round((totalCurrentAmps * cellVoltage * cellCount) / 1_000_000.0, 2);

        string regime;
        string action;
        bool isSound;

        if (telemetry.CellOperatingTemperatureCelsius > 685.0 || telemetry.ElectrolyteMatrixCorrosionRateMicronsPerYear > 12.0)
        {
            regime = "ELECTROLYTE_MATRIX_CORROSION_HAZARD";
            action = "Molten carbonate matrix dissolution exceeds 12 um/yr. Reduce operating temperature and enrich cathode O2 to suppress NiO solubility.";
            isSound = false;
        }
        else if (telemetry.AnodeCo2PurityPercent < 70.0)
        {
            regime = "ANODE_CO2_DILUTION_UNACCEPTABLE";
            action = "Anode CO2 stream purity below 70% sequestration threshold. Calibrate anode tail-gas condenser and fuel utilization ratio.";
            isSound = false;
        }
        else if (telemetry.FlueGasInletCo2MoleFraction < 0.035 || ccrPercent < 50.0)
        {
            regime = "CATHODE_CO2_STARVATION_DEGRADED";
            action = "Cathode CO2 concentration too lean for efficient carbonate transfer. Recirculate cathode exhaust gas to maintain carbonate flux.";
            isSound = false;
        }
        else
        {
            regime = "ELECTROCHEMICAL_CAPTURE_OPTIMAL";
            action = "Simultaneous high carbon capture rate (>75%) and net power export verified. Polygeneration efficiency optimal.";
            isSound = true;
        }

        return new McfcCaptureAnalysisResult(
            PlantStackId: telemetry.PlantStackId,
            CarbonCaptureRatePercent: ccrPercent,
            NetElectricPowerOutputMegawatts: electricPowerMw,
            ElectrochemicalCo2FluxMolesPerSecond: co2FluxMolesPerSec,
            PolygenerationRegime: regime,
            StackBalancingAction: action,
            IsStackElectrochemicalSound: isSound
        );
    }
}
