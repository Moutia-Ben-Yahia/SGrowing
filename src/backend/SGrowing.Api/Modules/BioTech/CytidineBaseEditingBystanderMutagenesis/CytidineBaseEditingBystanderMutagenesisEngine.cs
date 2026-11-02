using System;

namespace SGrowing.Api.Modules.BioTech.CytidineBaseEditingBystanderMutagenesis;

public sealed class CytidineBaseEditingBystanderMutagenesisEngine : ICytidineBaseEditingBystanderMutagenesisEngine
{
    public CytidineBaseEditingAnalysisResult AnalyzeBaseEditing(CytidineBaseEditingTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.EditTargetId))
        {
            throw new ArgumentException("Edit target ID must be provided.", nameof(telemetry));
        }

        if (telemetry.TargetCytidineProtospacerPosition < 1 || telemetry.TargetCytidineProtospacerPosition > 20)
        {
            throw new ArgumentException("Target position must reside within protospacer (1-20).", nameof(telemetry));
        }

        if (telemetry.OnTargetCtoTConversionEfficiencyPercent < 0 || telemetry.BystanderCtoTConversionEfficiencyPercent < 0 ||
            telemetry.IndelFormationFrequencyPercent < 0)
        {
            throw new ArgumentException("Efficiencies and frequencies cannot be negative.", nameof(telemetry));
        }

        var bystanderClamped = Math.Max(0.1, telemetry.BystanderCtoTConversionEfficiencyPercent);
        var specificityRatio = telemetry.OnTargetCtoTConversionEfficiencyPercent / bystanderClamped;

        var bystanderPenalty = Math.Max(0.0, 1.0 - (telemetry.BystanderCtoTConversionEfficiencyPercent / 100.0));
        var indelPenalty = Math.Max(0.0, 1.0 - (telemetry.IndelFormationFrequencyPercent / 100.0));
        var cleanEditingYield = telemetry.OnTargetCtoTConversionEfficiencyPercent * bystanderPenalty * indelPenalty;

        string regime;
        string guidance;
        bool isTherapeuticGrade = specificityRatio >= 10.0 &&
                                  telemetry.OnTargetCtoTConversionEfficiencyPercent >= 50.0 &&
                                  telemetry.IndelFormationFrequencyPercent < 2.5 &&
                                  telemetry.TranscriptomeWideRnaOffTargetEventsPerMillion < 50.0;

        if (telemetry.BystanderCtoTConversionEfficiencyPercent >= 25.0 && telemetry.BystanderCytidineCountInWindow > 0)
        {
            regime = "SEVERE_BYSTANDER_MUTAGENESIS_HAZARD";
            guidance = $"Bystander C-to-T deamination ({telemetry.BystanderCtoTConversionEfficiencyPercent:F1}%) creates off-target missense mutations. Switch deaminase architecture to narrow-window variant (e.g. YE1-BE4max or eA3A).";
            isTherapeuticGrade = false;
        }
        else if (telemetry.IndelFormationFrequencyPercent >= 5.0)
        {
            regime = "ELEVATED_INDEL_UNG_EXCISION_BREAK_WARNING";
            guidance = $"Elevated indel formation ({telemetry.IndelFormationFrequencyPercent:F1}%) indicates incomplete UNG inhibition. Increase UGI tandem domain dosage (e.g. BE4max with 2x UGI) to block base excision repair.";
            isTherapeuticGrade = false;
        }
        else if (telemetry.OnTargetCtoTConversionEfficiencyPercent < 20.0)
        {
            regime = "INSUFFICIENT_DEAMINATION_ON_TARGET_EFFICIENCY";
            guidance = $"Low on-target conversion ({telemetry.OnTargetCtoTConversionEfficiencyPercent:F1}%). Target position C{telemetry.TargetCytidineProtospacerPosition} may be suboptimal in the catalytic R-loop bubble; adjust guide RNA PAM distance.";
            isTherapeuticGrade = false;
        }
        else if (specificityRatio >= 15.0 && telemetry.OnTargetCtoTConversionEfficiencyPercent >= 60.0)
        {
            regime = "HIGH_PRECISION_CLEAN_BASE_EDITING_REGIME";
            guidance = $"Exceptional on-target deamination ({telemetry.OnTargetCtoTConversionEfficiencyPercent:F1}%) with minimal bystander noise ({telemetry.BystanderCtoTConversionEfficiencyPercent:F1}%). Specificity ratio {specificityRatio:F1}x conforms to therapeutic IND criteria.";
        }
        else
        {
            regime = "MODERATE_WINDOW_PERMISSIVE_EDITING_REGIME";
            guidance = $"Permissive deamination window. Clean product yield is {cleanEditingYield:F1}%. Monitor bystander allele distribution by deep amplicon NGS.";
        }

        return new CytidineBaseEditingAnalysisResult(
            telemetry.EditTargetId,
            Math.Round(specificityRatio, 2),
            Math.Round(cleanEditingYield, 2),
            regime,
            guidance,
            isTherapeuticGrade
        );
    }
}
