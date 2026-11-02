using System;

namespace SGrowing.Api.Modules.Energy.PerovskiteSiliconTandemRecombination;

public interface IPerovskiteSiliconTandemEngine
{
    PerovskiteSiliconTandemAnalysisResult AnalyzeTandemCell(PerovskiteSiliconTandemTelemetry telemetry);
}

public sealed class PerovskiteSiliconTandemEngine : IPerovskiteSiliconTandemEngine
{
    private const double MaxOptimalCurrentMismatchPercent = 3.0; // Current mismatch <= 3.0%
    private const double MaxTunnelJunctionResistivityMOhmCm2 = 25.0; // rho_c <= 25 mOhm*cm^2
    private const double RecordTandemEfficiencyThresholdPercent = 30.0; // PCE >= 30.0%

    public PerovskiteSiliconTandemAnalysisResult AnalyzeTandemCell(PerovskiteSiliconTandemTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.TopPerovskiteCurrentDensityMaPerCm2 <= 0.0 || telemetry.BottomSiliconCurrentDensityMaPerCm2 <= 0.0)
        {
            throw new ArgumentException("Top and bottom cell current densities must be positive.", nameof(telemetry));
        }

        // 1. Series Two-Terminal Current Matching:
        // J_sc,tandem = min(J_top, J_bot) - mismatch_penalty
        double jTop = telemetry.TopPerovskiteCurrentDensityMaPerCm2;
        double jBot = telemetry.BottomSiliconCurrentDensityMaPerCm2;
        double currentMismatchRatio = (Math.Abs(jTop - jBot) / (jTop + jBot)) * 100.0;

        double mismatchPenalty = 0.05 * Math.Abs(jTop - jBot);
        double seriesMatchedJsc = Math.Min(jTop, jBot) - mismatchPenalty;
        seriesMatchedJsc = Math.Round(Math.Clamp(seriesMatchedJsc, 5.0, 25.0), 2);

        // 2. Tunnel Junction Ohmic Voltage Drop:
        // Delta V_TJ = J_sc * rho_c (mV)
        // J_sc in mA/cm^2 * rho_c in mOhm*cm^2 = 1e-3 A/cm^2 * 1e-3 Ohm*cm^2 = 1e-6 V = 1e-3 mV
        double deltaVTjMv = (seriesMatchedJsc * telemetry.TunnelJunctionContactResistivityMOhmCm2) * 1e-3;
        deltaVTjMv = Math.Round(deltaVTjMv, 3);

        // 3. Tandem Open Circuit Voltage:
        // V_oc,tandem = V_oc,top + V_oc,bot - Delta V_TJ - Delta V_SRH
        double srhLossVolts = telemetry.BulkPerovskiteSrhRecombinationLossMv * 1e-3;
        double tjLossVolts = deltaVTjMv * 1e-3;
        double tandemVoc = telemetry.TopPerovskiteOpenCircuitVoltageVolts + telemetry.BottomSiliconOpenCircuitVoltageVolts
            - srhLossVolts - tjLossVolts;
        tandemVoc = Math.Round(Math.Clamp(tandemVoc, 1.0, 2.3), 3);

        // 4. Power Conversion Efficiency (PCE):
        // PCE = (J_sc * V_oc * FF) / P_in * 100% (P_in = 100 mW/cm^2 = 1000 W/m^2 AM1.5G)
        double ffFraction = Math.Clamp(telemetry.FillFactorPercent / 100.0, 0.5, 0.9);
        double powerDensityMw = seriesMatchedJsc * tandemVoc * ffFraction;
        double pcePercent = (powerDensityMw / 100.0) * 100.0; // power / 100 mW/cm^2
        pcePercent = Math.Round(Math.Clamp(pcePercent, 10.0, 36.0), 2);

        // 5. Validation Checks:
        bool isCurrentMatchingOk = currentMismatchRatio <= MaxOptimalCurrentMismatchPercent;
        bool isTjOhmicOk = telemetry.TunnelJunctionContactResistivityMOhmCm2 <= MaxTunnelJunctionResistivityMOhmCm2;
        bool isPceRecordGrade = pcePercent >= RecordTandemEfficiencyThresholdPercent;

        // 6. Regime & Guidance:
        string regime;
        string guidance;

        if (!isTjOhmicOk)
        {
            regime = "TUNNEL_JUNCTION_HIGH_RESISTIVITY_BOTTLENECK";
            guidance = $"Tunnel junction contact resistivity ({telemetry.TunnelJunctionContactResistivityMOhmCm2:F1} mOhm*cm^2) exceeds limit ({MaxTunnelJunctionResistivityMOhmCm2:F1} mOhm*cm^2). Thin intermediate ITO/nc-SiOx:H layer or optimize annealing to eliminate interface recombination barrier.";
        }
        else if (!isCurrentMatchingOk)
        {
            regime = "SUB_CELL_CURRENT_MISMATCH_CURRENT_LIMITING";
            string limitingCell = jTop < jBot ? "Top Perovskite" : "Bottom Silicon";
            guidance = $"Current mismatch ({currentMismatchRatio:F1}%) creates a bottleneck limited by the {limitingCell} cell. Adjust top perovskite layer thickness or texturing pyramids to achieve optical balance at ~20 mA/cm^2.";
        }
        else if (telemetry.BulkPerovskiteSrhRecombinationLossMv > 45.0)
        {
            regime = "PEROVSKITE_NON_RADIATIVE_SRH_RECOMBINATION_DEFICIT";
            guidance = $"Excessive Shockley-Read-Hall non-radiative recombination ({telemetry.BulkPerovskiteSrhRecombinationLossMv:F1} mV loss). Apply 2D/3D heterostructure surface passivation (e.g., phenethylammonium iodide PEAI) to suppress grain boundary trap density.";
        }
        else if (!isPceRecordGrade)
        {
            regime = "COMMERCIAL_GRADE_PEROVSKITE_SILICON_TANDEM";
            guidance = $"Tandem efficiency ({pcePercent:F1}%) is commercial-grade but below record threshold ({RecordTandemEfficiencyThresholdPercent:F1}%). Enhance fill factor through improved self-assembled monolayer (SAM) hole transport layers (Me-4PACz).";
        }
        else
        {
            regime = "RECORD_GRADE_PEROVSKITE_SILICON_TANDEM_STABLE";
            guidance = "Perovskite/silicon tandem operating in world-class regime: near-perfect current matching, low tunnel junction voltage loss, minimal SRH recombination, and > 30% PCE achieved.";
        }

        return new PerovskiteSiliconTandemAnalysisResult
        {
            TandemCellId = telemetry.TandemCellId,
            SeriesMatchedCurrentDensityMaPerCm2 = seriesMatchedJsc,
            CurrentMismatchRatioPercent = Math.Round(currentMismatchRatio, 2),
            TunnelJunctionOhmicVoltageDropMv = deltaVTjMv,
            TandemOpenCircuitVoltageVolts = tandemVoc,
            PowerConversionEfficiencyPercent = pcePercent,
            IsCurrentMatchingOptimal = isCurrentMatchingOk,
            IsTunnelJunctionOhmicLossLow = isTjOhmicOk,
            IsTandemEfficiencyRecordGrade = isPceRecordGrade,
            TandemOperationRegime = regime,
            TandemOptimizationRecommendation = guidance
        };
    }
}
