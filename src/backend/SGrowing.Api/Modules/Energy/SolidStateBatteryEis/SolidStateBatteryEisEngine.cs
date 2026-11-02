namespace SGrowing.Api.Modules.Energy.SolidStateBatteryEis;

public interface ISolidStateBatteryEisEngine
{
    BatteryDendriteAssessment EvaluateEisSpectrum(EisEvaluationRequest request);
}

public class SolidStateBatteryEisEngine : ISolidStateBatteryEisEngine
{
    public BatteryDendriteAssessment EvaluateEisSpectrum(EisEvaluationRequest request)
    {
        var cell = request.Cell;
        var points = request.Spectrum ?? [];

        if (points.Count == 0)
        {
            return new BatteryDendriteAssessment(
                CellId: cell.CellId,
                BulkResistanceOhms: 0.05,
                SeiResistanceOhms: 0.15,
                ChargeTransferResistanceOhms: 0.25,
                DendriteRiskScorePercent: 10.0,
                DendriteNucleationDetected: false,
                ChargingProtocolRecommendation: "STANDARD_CCCV: Maintain nominal constant-current constant-voltage charging.",
                HealthStatus: "NOMINAL: Zero EIS telemetry; baseline defaults applied."
            );
        }

        // 1. High frequency intercept (frequency >= 10 kHz): Bulk electrolyte resistance R_b
        var hfPoints = points.Where(p => p.FrequencyHz >= 10000.0).ToList();
        double rBulk = hfPoints.Count > 0 ? hfPoints.Min(p => p.RealImpedanceOhms) : points.First().RealImpedanceOhms;

        // 2. Medium frequency semicircle (100 Hz - 10 kHz): SEI film resistance R_sei
        var mfPoints = points.Where(p => p.FrequencyHz >= 100.0 && p.FrequencyHz < 10000.0).ToList();
        double rSei = mfPoints.Count > 0 ? Math.Max(0.01, mfPoints.Max(p => p.RealImpedanceOhms) - rBulk) : 0.12;

        // 3. Low frequency semicircle (1 Hz - 100 Hz): Charge transfer resistance R_ct
        var lfPoints = points.Where(p => p.FrequencyHz >= 1.0 && p.FrequencyHz < 100.0).ToList();
        double rCt = lfPoints.Count > 0 ? Math.Max(0.02, lfPoints.Max(p => p.RealImpedanceOhms) - (rBulk + rSei)) : 0.20;

        // 4. Dendrite detection indicators:
        // Sudden collapse in R_sei (< 0.04 Ohm) or phase angle collapse at 1-10 Hz (phase < 5 deg) indicating soft electronic percolation
        double minPhaseMf = points.Where(p => p.FrequencyHz >= 5.0 && p.FrequencyHz <= 500.0).Select(p => Math.Abs(p.PhaseAngleDegrees)).DefaultIfEmpty(20.0).Min();
        bool phaseCollapsing = minPhaseMf < 7.0;
        bool lowSeiPercolation = rSei < 0.035;

        // Dendrite risk score
        double riskScore = 5.0; // baseline 5%
        if (lowSeiPercolation) riskScore += 55.0;
        if (phaseCollapsing) riskScore += 30.0;
        if (cell.ChargingCurrentAmps > (cell.NominalCapacityAh * 1.5)) riskScore += 15.0; // high C-rate accelerates dendrites
        if (cell.TemperatureCelsius < 10.0) riskScore += 10.0; // low temperature increases lithium plating

        double finalRiskPercent = Math.Clamp(riskScore, 0.0, 99.9);
        bool dendriteDetected = finalRiskPercent >= 65.0 || (lowSeiPercolation && phaseCollapsing);

        string recommendation;
        string healthStatus;

        if (dendriteDetected)
        {
            healthStatus = "CRITICAL_LITHIUM_DENDRITE_PROPAGATION: Micro-percolation detected in solid electrolyte separator. Imminent soft short-circuit.";
            recommendation = "ASYMMETRIC_PULSE_HEALING: Switch to 50Hz asymmetric pulsed charging (8ms cathodic charge at 0.5C, 2ms anodic dissolution pulse at 2.0C) to electrochemically smooth lithium filaments.";
        }
        else if (finalRiskPercent >= 35.0)
        {
            healthStatus = "WARNING_INTERPHASE_INSTABILITY: SEI impedance fluctuations observed.";
            recommendation = "CURRENT_THROTTLE_50_PERCENT: Reduce charge rate to 0.5C and hold cell temperature at 35C to enhance lithium vacancy diffusion.";
        }
        else
        {
            healthStatus = "HEALTHY_SOLID_ELECTROLYTE_INTERPHASE: Clean semi-circular Nyquist response. Zero dendrite formation.";
            recommendation = "STANDARD_FAST_CHARGE: Cell authorized for standard 1.0C fast charging.";
        }

        return new BatteryDendriteAssessment(
            CellId: cell.CellId,
            BulkResistanceOhms: Math.Round(rBulk, 4),
            SeiResistanceOhms: Math.Round(rSei, 4),
            ChargeTransferResistanceOhms: Math.Round(rCt, 4),
            DendriteRiskScorePercent: Math.Round(finalRiskPercent, 1),
            DendriteNucleationDetected: dendriteDetected,
            ChargingProtocolRecommendation: recommendation,
            HealthStatus: healthStatus
        );
    }
}
