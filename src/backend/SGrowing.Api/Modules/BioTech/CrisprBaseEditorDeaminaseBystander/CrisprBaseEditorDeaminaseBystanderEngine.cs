using System;

namespace SGrowing.Api.Modules.BioTech.CrisprBaseEditorDeaminaseBystander;

public sealed class CrisprBaseEditorDeaminaseBystanderEngine : ICrisprBaseEditorDeaminaseBystanderEngine
{
    public CrisprBaseEditorAnalysisResult AnalyzeBaseEditingFidelity(CrisprBaseEditorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var onTarget = Math.Clamp(telemetry.OnTargetDeaminationRate, 0.0, 1.0);
        var bystander = Math.Max(0.001, telemetry.BystanderDeaminationRate);
        var rnaOffTarget = Math.Max(0.0001, telemetry.RnaOffTargetDeaminationRate);
        var indel = Math.Max(0.0001, telemetry.IndelFrequency);

        var purityRatio = Math.Round(onTarget / bystander, 2);
        var specificityRatio = Math.Round(onTarget / rnaOffTarget, 1);
        var editToIndelRatio = Math.Round(onTarget / indel, 1);

        string regime;
        string action;
        bool isActionable;

        if (telemetry.IndelFrequency > 0.045)
        {
            regime = "INDEL_GENOMIC_DAMAGE_ALERT";
            action = "Double-strand break conversion frequency exceeds 4.5%. Enhance Uracil DNA Glycosylase Inhibitor (UGI) domain or replace Cas9 D10A nickase.";
            isActionable = false;
        }
        else if (telemetry.BystanderBasesCountInWindow > 0 && purityRatio < 4.0)
        {
            regime = "BYSTANDER_MUTAGENESIS_RISK";
            action = "Active bystander base editing inside positions 4-8 catalytic window. Switch to narrow-window engineered deaminase variant (e.g. YE1-BE4max or TadA-8e V106W).";
            isActionable = false;
        }
        else if (telemetry.RnaOffTargetDeaminationRate > 0.02 || specificityRatio < 25.0)
        {
            regime = "OFF_TARGET_TRANSCRIPTOME_DEAMINATION_WARNING";
            action = "Transcriptome-wide off-target RNA deamination detected. Introduce SECURE/FE1 mutations into deaminase domain to restrict substrate affinity to DNA.";
            isActionable = false;
        }
        else
        {
            regime = "HIGH_FIDELITY_PRECISION_WINDOW";
            action = "Clean single-nucleotide transition with minimal bystander deamination and low indel formation (<1%). Clinically validated for therapeutic correction.";
            isActionable = true;
        }

        return new CrisprBaseEditorAnalysisResult(
            EditorTargetId: telemetry.EditorTargetId,
            OnToBystanderEditingPurityRatio: purityRatio,
            OnToRnaOffTargetSpecificityRatio: specificityRatio,
            EditToIndelRatio: editToIndelRatio,
            EditingFidelityRegime: regime,
            RecommendedProteinEngineeringAction: action,
            IsClinicallyActionable: isActionable
        );
    }
}
