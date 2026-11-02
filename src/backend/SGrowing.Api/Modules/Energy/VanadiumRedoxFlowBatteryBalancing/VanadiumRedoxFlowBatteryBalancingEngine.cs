using System;

namespace SGrowing.Api.Modules.Energy.VanadiumRedoxFlowBatteryBalancing;

public sealed class VanadiumRedoxFlowBatteryBalancingEngine : IVanadiumRedoxFlowBatteryBalancingEngine
{
    public VanadiumFlowBatteryAnalysisResult AnalyzeElectrolyteBalance(VanadiumFlowBatteryTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.FlowBatteryStackId))
        {
            throw new ArgumentException("Flow battery stack ID must be provided.", nameof(telemetry));
        }

        if (telemetry.NegalyteV2ConcentrationMolar < 0 || telemetry.NegalyteV3ConcentrationMolar < 0 ||
            telemetry.PosalyteV4ConcentrationMolar < 0 || telemetry.PosalyteV5ConcentrationMolar < 0)
        {
            throw new ArgumentException("Vanadium ion concentrations cannot be negative.", nameof(telemetry));
        }

        if (telemetry.PumpElectrolyteFlowRateLitersPerMinute < 0)
        {
            throw new ArgumentException("Flow rate cannot be negative.", nameof(telemetry));
        }

        var totalNeg = telemetry.NegalyteV2ConcentrationMolar + telemetry.NegalyteV3ConcentrationMolar;
        var totalPos = telemetry.PosalyteV4ConcentrationMolar + telemetry.PosalyteV5ConcentrationMolar;

        var socNeg = totalNeg > 0 ? (telemetry.NegalyteV2ConcentrationMolar / totalNeg) * 100.0 : 0.0;
        var socPos = totalPos > 0 ? (telemetry.PosalyteV5ConcentrationMolar / totalPos) * 100.0 : 0.0;

        var deltaSoc = Math.Abs(socNeg - socPos);
        var deltaConcentration = Math.Abs(totalNeg - totalPos);

        string regime;
        string guidance;
        bool isBalanced = deltaSoc <= 5.0 &&
                          deltaConcentration <= 0.15 &&
                          (telemetry.PosalyteV5ConcentrationMolar < 1.60 || telemetry.ElectrolyteTemperatureCelsius < 38.0);

        if (telemetry.PosalyteV5ConcentrationMolar >= 1.65 && telemetry.ElectrolyteTemperatureCelsius >= 40.0)
        {
            regime = "CRITICAL_V2O5_THERMAL_PRECIPITATION_RISK";
            guidance = $"Elevated V5+ concentration ({telemetry.PosalyteV5ConcentrationMolar:F2} M) at high temperature ({telemetry.ElectrolyteTemperatureCelsius:F1} C) risks irreversible V2O5 precipitation. Increase chiller circulation and reduce stack SOC below 80%.";
            isBalanced = false;
        }
        else if (deltaSoc >= 15.0)
        {
            regime = "SEVERE_CROSSOVER_CAPACITY_IMBALANCE_REBALANCING_TRIGGERED";
            guidance = $"Severe SOC imbalance (Delta SOC = {deltaSoc:F1}%). Ion crossover or H2 side evolution has shifted half-cell stoichiometries. Activate auxiliary electrochemical rebalancing cell.";
            isBalanced = false;
        }
        else if (telemetry.StackOpenCircuitVoltageVolts < 1.15)
        {
            regime = "DEEP_DISCHARGE_HYDROGEN_EVOLUTION_LIMIT";
            guidance = $"Stack OCV ({telemetry.StackOpenCircuitVoltageVolts:F2} V) indicates over-discharge. Halt discharge current to prevent negative electrode corrosion.";
            isBalanced = false;
        }
        else if (deltaSoc <= 3.0 && deltaConcentration <= 0.10)
        {
            regime = "OPTIMAL_SYMMETRIC_REVERSIBLE_VRFB_REGIME";
            guidance = $"Spectrophotometry confirms symmetric SOC balance (Neg = {socNeg:F1}%, Pos = {socPos:F1}%, Delta = {deltaSoc:F1}%). Maximum Coulombic and round-trip energy efficiency maintained.";
        }
        else
        {
            regime = "MONITORED_ELECTROLYTE_STATE_REGIME";
            guidance = $"Electrolyte within acceptable operational bounds. Monitor crossover trend; total vanadium mismatch is {deltaConcentration:F3} M.";
        }

        return new VanadiumFlowBatteryAnalysisResult(
            telemetry.FlowBatteryStackId,
            Math.Round(socNeg, 2),
            Math.Round(socPos, 2),
            Math.Round(deltaSoc, 2),
            Math.Round(deltaConcentration, 3),
            regime,
            guidance,
            isBalanced
        );
    }
}
