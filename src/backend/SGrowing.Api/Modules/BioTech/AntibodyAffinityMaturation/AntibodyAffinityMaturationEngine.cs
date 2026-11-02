namespace SGrowing.Api.Modules.BioTech.AntibodyAffinityMaturation;

public class AntibodyAffinityMaturationEngine
{
    private const double GasConstantTempRt = 0.592; // kcal/mol at 298.15 K
    private const double TmAlphaFactor = 2.5;       // deg C / (kcal/mol)

    public AntibodyMaturationResult EvaluateCandidates(AntibodyMaturationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.WildTypeKdNanoMolar <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Wild-type Kd must be positive.");
        }

        if (request.WildTypeMeltingTempCelsius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Wild-type melting temperature must be positive.");
        }

        var candidateMutations = request.CandidateMutations ?? [];
        var scoredList = new List<MaturedMutantScore>();

        foreach (var candidate in candidateMutations)
        {
            // Calculate binding Kd
            // Kd_mut = Kd_wt * exp(ddG_bind / RT)
            double ratio = Math.Exp(candidate.DeltaDeltaGBindingKcalMol / GasConstantTempRt);
            double predictedKd = request.WildTypeKdNanoMolar * ratio;
            double foldImprovement = request.WildTypeKdNanoMolar / Math.Max(predictedKd, 1e-6);

            // Calculate Tm shift: dTm = -alpha * ddG_fold
            double predictedTm = request.WildTypeMeltingTempCelsius - (TmAlphaFactor * candidate.DeltaDeltaGFoldingKcalMol);

            // Pareto Fitness Score: prioritize binding affinity improvement while penalizing fold destabilization and aggregation
            double paretoScore = (-candidate.DeltaDeltaGBindingKcalMol * 2.0) - (candidate.DeltaDeltaGFoldingKcalMol * 1.0) - (candidate.SpatialAggregationPropensity * 3.0);

            // Filtering rules
            bool hasLiability = candidate.ContainsLiabilityMotif;
            bool isFoldDestabilized = candidate.DeltaDeltaGFoldingKcalMol > 1.5;
            bool isAggregationProne = candidate.SpatialAggregationPropensity > 0.45;
            bool isHighAffinity = candidate.DeltaDeltaGBindingKcalMol <= -0.6;
            bool hasAdequateTm = predictedTm >= 68.0;

            bool isRecommended = !hasLiability && !isFoldDestabilized && !isAggregationProne && isHighAffinity && hasAdequateTm;

            string summary;
            if (hasLiability)
            {
                summary = "REJECTED: Contains sequence liability motif (deamidation/isomerization/unpaired cysteine).";
            }
            else if (isFoldDestabilized)
            {
                summary = $"REJECTED: Severe folding destabilization (ddG_fold={candidate.DeltaDeltaGFoldingKcalMol:F2} kcal/mol, Tm dropped to {predictedTm:F1}°C).";
            }
            else if (isAggregationProne)
            {
                summary = $"REJECTED: Excessive spatial aggregation propensity (SAP={candidate.SpatialAggregationPropensity:F2}).";
            }
            else if (isRecommended)
            {
                summary = $"HIGH_PRIORITY_LEAD: {foldImprovement:F1}x affinity boost (Kd: {predictedKd:F2} nM) with robust thermostability (Tm: {predictedTm:F1}°C).";
            }
            else
            {
                summary = $"MARGINAL: Minor affinity change ({foldImprovement:F1}x) with acceptable biophysical profile.";
            }

            scoredList.Add(new MaturedMutantScore(
                candidate.MutationCode,
                candidate.CdrRegion,
                Math.Round(predictedKd, 3),
                Math.Round(predictedTm, 2),
                Math.Round(foldImprovement, 2),
                Math.Round(paretoScore, 3),
                isRecommended,
                summary));
        }

        // Sort by Pareto score descending
        var ranked = scoredList.OrderByDescending(s => s.ParetoScore).ToList();
        var bestRecommended = ranked.FirstOrDefault(s => s.IsRecommended);

        string optimalLead = bestRecommended?.MutationCode ?? "NONE_RECOMMENDED";
        string globalSummary;

        if (bestRecommended != null)
        {
            globalSummary = $"Optimal lead candidate {bestRecommended.MutationCode} ({bestRecommended.CdrRegion}) achieves {bestRecommended.FoldAffinityImprovement:F1}x affinity enhancement (Kd: {bestRecommended.PredictedKdNanoMolar:F2} nM, Tm: {bestRecommended.PredictedMeltingTempCelsius:F1}°C) without developability liabilities.";
        }
        else
        {
            globalSummary = "No candidates met simultaneous affinity, stability, and developability criteria.";
        }

        return new AntibodyMaturationResult(
            request.AntibodyId,
            request.TargetAntigenName,
            candidateMutations.Count,
            ranked.Count(r => r.IsRecommended),
            ranked,
            optimalLead,
            globalSummary);
    }
}
