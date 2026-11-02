using System;

namespace SGrowing.Api.Modules.Energy.Sco2BraytonCompressorSurge;

public interface ISco2BraytonCompressorSurgeEngine
{
    Sco2CompressorAnalysisResult AnalyzeCompressorSurge(Sco2CompressorTelemetry telemetry);
}

public sealed class Sco2BraytonCompressorSurgeEngine : ISco2BraytonCompressorSurgeEngine
{
    public Sco2CompressorAnalysisResult AnalyzeCompressorSurge(Sco2CompressorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.SurgeMassFlowRateKgPerSec <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Surge mass flow rate must be strictly positive.");
        }

        double surgeMarginPercent = ((telemetry.OperatingMassFlowRateKgPerSec - telemetry.SurgeMassFlowRateKgPerSec) 
            / telemetry.SurgeMassFlowRateKgPerSec) * 100.0;

        double bParameter = 0.0;
        if (telemetry.NearCriticalAcousticVelocityMPerSec > 0.0 &&
            telemetry.InletDuctAreaM2 > 0.0 &&
            telemetry.EquivalentDuctLengthMeters > 0.0 &&
            telemetry.PlenumVolumeM3 > 0.0)
        {
            double ductVol = telemetry.InletDuctAreaM2 * telemetry.EquivalentDuctLengthMeters;
            bParameter = (telemetry.RotorTipSpeedMPerSec / (2.0 * telemetry.NearCriticalAcousticVelocityMPerSec))
                         * Math.Sqrt(telemetry.PlenumVolumeM3 / ductVol);
        }

        double requiredBypassKgPerSec = 0.0;
        double recommendedValvePercent = telemetry.CurrentBypassValveOpeningPercent;

        if (surgeMarginPercent < 15.0)
        {
            double safeTargetFlow = telemetry.SurgeMassFlowRateKgPerSec * 1.15;
            requiredBypassKgPerSec = Math.Max(0.0, safeTargetFlow - telemetry.OperatingMassFlowRateKgPerSec);
            double valveIncrement = (requiredBypassKgPerSec / (telemetry.SurgeMassFlowRateKgPerSec * 0.5)) * 100.0;
            recommendedValvePercent = Math.Min(100.0, Math.Max(telemetry.CurrentBypassValveOpeningPercent, valveIncrement));
        }
        else
        {
            // Nominal: trim valve down gradually if it was left open
            recommendedValvePercent = Math.Max(0.0, telemetry.CurrentBypassValveOpeningPercent * 0.8);
        }

        string regime;
        string action;
        bool isSurgeImminent;

        if (surgeMarginPercent < 5.0 || (bParameter >= 0.70 && surgeMarginPercent < 10.0))
        {
            regime = "SURGE_MARGIN_CRITICAL";
            action = "RAPID_OPEN_ANTI_SURGE_BYPASS_VALVE_50_PERCENT";
            isSurgeImminent = true;
            recommendedValvePercent = Math.Max(recommendedValvePercent, 50.0);
        }
        else if (surgeMarginPercent < 15.0)
        {
            regime = "SURGE_WARNING";
            action = "MODULATE_ANTI_SURGE_VALVE_TRIM";
            isSurgeImminent = false;
        }
        else
        {
            regime = "NOMINAL_STABLE";
            action = "MAINTAIN_OPTIMAL_COMPRESSOR_MAP_TRAJECTORY";
            isSurgeImminent = false;
        }

        return new Sco2CompressorAnalysisResult(
            telemetry.CycleId,
            Math.Round(surgeMarginPercent, 2),
            Math.Round(bParameter, 4),
            Math.Round(requiredBypassKgPerSec, 3),
            Math.Round(recommendedValvePercent, 1),
            regime,
            action,
            isSurgeImminent,
            DateTimeOffset.UtcNow);
    }
}
