using System;

namespace SGrowing.Api.Modules.BioTech.ProtacTernaryComplexKinetics;

public sealed class ProtacTernaryComplexKineticsEngine : IProtacTernaryComplexKineticsEngine
{
    public ProtacTernaryComplexAnalysisResult AnalyzeProtacKinetics(ProtacTernaryComplexTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.ProtacCandidateId))
        {
            throw new ArgumentException("PROTAC candidate ID must be provided.", nameof(telemetry));
        }

        if (telemetry.BinaryKdPoiNanomolar <= 0 || telemetry.BinaryKdE3Nanomolar <= 0 ||
            telemetry.TernaryKdPoiNanomolar <= 0 || telemetry.MeasuredDc50Nanomolar <= 0)
        {
            throw new ArgumentException("Dissociation constants and DC50 must be strictly positive.", nameof(telemetry));
        }

        // Cooperativity factor: alpha = K_D(binary) / K_D(ternary)
        var alpha = telemetry.BinaryKdPoiNanomolar / telemetry.TernaryKdPoiNanomolar;

        // Optimal ternary complex formation concentration: C_opt = sqrt(K_D1 * K_D2 / alpha)
        var cOptNanomolar = Math.Sqrt((telemetry.BinaryKdPoiNanomolar * telemetry.BinaryKdE3Nanomolar) / Math.Max(0.01, alpha));

        string regime;
        string guidance;
        bool isViable = alpha >= 1.5 &&
                        telemetry.MaximalDegradationPercent >= 85.0 &&
                        telemetry.MeasuredDc50Nanomolar <= 50.0;

        if (alpha < 0.2)
        {
            regime = "SEVERE_NEGATIVE_COOPERATIVITY_STERIC_CLASH_HAZARD";
            guidance = $"Severe negative cooperativity (alpha = {alpha:F2} < 0.2). Linker creates steric clashes between {telemetry.TargetProtein} and {telemetry.E3Ligase}; modify linker length, rigidity, or exit vectors.";
            isViable = false;
        }
        else if (telemetry.MaximalDegradationPercent < 50.0 || telemetry.MeasuredDc50Nanomolar > 500.0)
        {
            regime = "INSUFFICIENT_PROTEASOMAL_DEGRADATION_EFFICACY";
            guidance = $"Inadequate degradation (D_max = {telemetry.MaximalDegradationPercent:F1}%, DC50 = {telemetry.MeasuredDc50Nanomolar:F1} nM). Ternary complex geometry does not permit productive lysine polyubiquitination.";
            isViable = false;
        }
        else if (telemetry.CellularHookConcentrationMicromolar < 0.1)
        {
            regime = "NARROW_HOOK_EFFECT_THERAPEUTIC_WINDOW_WARNING";
            guidance = $"Narrow therapeutic window: hook effect manifests at {telemetry.CellularHookConcentrationMicromolar:F2} uM due to high binary affinity mismatch. Retune warhead affinities.";
            isViable = false;
        }
        else if (alpha >= 5.0 && telemetry.MaximalDegradationPercent >= 90.0 && telemetry.MeasuredDc50Nanomolar <= 25.0)
        {
            regime = "OPTIMAL_POSITIVE_COOPERATIVITY_HIGH_DEGRADATION_REGIME";
            guidance = $"Favorable positive cooperativity (alpha = {alpha:F1}x) drives potent catalytic degradation (D_max = {telemetry.MaximalDegradationPercent:F1}%, DC50 = {telemetry.MeasuredDc50Nanomolar:F1} nM, C_opt = {cOptNanomolar:F1} nM). Prime preclinical candidate.";
        }
        else
        {
            regime = "NOMINAL_TERNARY_COMPLEX_DEGRADATION_REGIME";
            guidance = $"PROTAC forms stable ternary complex (alpha = {alpha:F2}). DC50 is {telemetry.MeasuredDc50Nanomolar:F1} nM.";
        }

        return new ProtacTernaryComplexAnalysisResult(
            telemetry.ProtacCandidateId,
            Math.Round(alpha, 2),
            Math.Round(cOptNanomolar, 1),
            regime,
            guidance,
            isViable
        );
    }
}
