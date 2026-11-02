namespace SGrowing.Api.Modules.BioTech.CrisprCasCleavageKinetics;

public interface ICrisprCasCleavageKineticsEngine
{
    CrisprSafetyEvaluationResult EvaluateOffTargetCleavage(CrisprCleavageEvaluationRequest request);
}

public class CrisprCasCleavageKineticsEngine : ICrisprCasCleavageKineticsEngine
{
    private const double RtConstantKcal = 0.616; // R * T at 37 degC (310.15 K)
    private const double ClinicalSafetyCutoffPercent = 0.05; // 0.05% off-target cleavage probability limit

    public CrisprSafetyEvaluationResult EvaluateOffTargetCleavage(CrisprCleavageEvaluationRequest request)
    {
        var guide = request.Guide;
        var loci = request.CandidateOffTargets ?? [];
        var predictions = new List<OffTargetCleavagePrediction>();

        string targetSpacer = guide.SpacerSequence20Nt.ToUpperInvariant();

        foreach (var locus in loci)
        {
            string locusDna = locus.DnaProtospacer20Nt.ToUpperInvariant();
            int minLen = Math.Min(targetSpacer.Length, locusDna.Length);

            int seedMismatches = 0;   // Positions 0 to 7 (seed)
            int distalMismatches = 0; // Positions 8 to 19 (distal)

            for (int i = 0; i < minLen; i++)
            {
                if (targetSpacer[i] != locusDna[i])
                {
                    if (i < 8) seedMismatches++;
                    else distalMismatches++;
                }
            }

            // PAM penalty: Cas12a requires TTTV (T at 0,1,2 and A/C/G at 3)
            double pamPenaltyKcal = 0.0;
            string pam = locus.PamFound.ToUpperInvariant();
            if (pam.Length >= 4)
            {
                if (pam[0] != 'T' || pam[1] != 'T' || pam[2] != 'T')
                    pamPenaltyKcal += 3.8;
                if (pam[3] == 'T') // TTTT is inactive for Cas12a
                    pamPenaltyKcal += 2.5;
            }
            else
            {
                pamPenaltyKcal += 4.5;
            }

            // Thermodynamic Gibbs free energy penalty DeltaDeltaG
            // Seed mismatches penalize ~3.4 kcal/mol each, distal ~0.85 kcal/mol each
            double deltaDeltaG = pamPenaltyKcal + (seedMismatches * 3.4) + (distalMismatches * 0.85);

            // Cleavage probability: P = 1 / (1 + exp(DeltaDeltaG / RT))
            // When DeltaDeltaG = 0 (perfect match), P = 100%
            double cleavageProbFraction = 1.0 / (1.0 + Math.Exp(deltaDeltaG / RtConstantKcal));
            double cleavageProbPercent = cleavageProbFraction * 100.0;

            bool exceedsThreshold = cleavageProbPercent >= ClinicalSafetyCutoffPercent;

            predictions.Add(new OffTargetCleavagePrediction(
                LocusId: locus.LocusId,
                GeneAnnotation: locus.GeneAnnotation,
                SeedMismatchCount: seedMismatches,
                DistalMismatchCount: distalMismatches,
                DeltaDeltaGKcalMol: Math.Round(deltaDeltaG, 2),
                CleavageProbabilityPercent: Math.Round(cleavageProbPercent, 4),
                ExceedsSafetyThreshold: exceedsThreshold
            ));
        }

        int hazardousCount = predictions.Count(p => p.ExceedsSafetyThreshold);
        bool approved = hazardousCount == 0;

        string classification = approved
            ? "APPROVED_FOR_CELL_THERAPY: Zero detected off-target genomic cleavage above 0.05% safety threshold."
            : $"UNSAFE_GENOME_WIDE_MUTAGENESIS: {hazardousCount} candidate loci exceed off-target cleavage safety floor. High risk of off-target chromosomal translocations.";

        return new CrisprSafetyEvaluationResult(
            GuideId: guide.GuideId,
            TargetGeneSymbol: guide.TargetGeneSymbol,
            TotalLociScreened: loci.Count,
            HazardousOffTargetsCount: hazardousCount,
            GuideApprovedForTherapy: approved,
            ClinicalSafetyClassification: classification,
            Predictions: predictions
        );
    }
}
