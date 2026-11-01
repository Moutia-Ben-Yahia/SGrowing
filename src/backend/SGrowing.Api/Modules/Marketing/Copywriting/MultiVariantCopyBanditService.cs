using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Marketing.Copywriting;

/// <summary>
/// Interface for real-time multi-armed bandit copy variant optimization using Thompson Sampling.
/// </summary>
public interface IMultiVariantCopyBanditService
{
    /// <summary>
    /// Selects the optimal copy variant using Thompson Sampling from Beta posterior distributions.
    /// </summary>
    VariantSelectionResult SelectBestVariant(string campaignId, IEnumerable<string> variantIds);

    /// <summary>
    /// Ingests real-time interaction feedback (Delivered, Opened, Clicked, Converted, Unsubscribed)
    /// to update Bayesian Beta posteriors.
    /// </summary>
    VariantFeedbackResult RecordFeedback(string campaignId, string variantId, CopyInteractionEvent interaction);

    /// <summary>
    /// Returns statistical performance, expected conversion values, and empirical regret for a campaign.
    /// </summary>
    CampaignBanditPerformance GetVariantPerformance(string campaignId);

    /// <summary>
    /// Clears bandit memory for a given campaign.
    /// </summary>
    void ResetCampaign(string campaignId);
}

/// <summary>
/// Production Thompson Sampling contextual multi-armed bandit service for autonomous copywriting optimization.
/// </summary>
public sealed class MultiVariantCopyBanditService : IMultiVariantCopyBanditService
{
    private readonly ConcurrentDictionary<string, CampaignBanditState> _campaigns = new();

    /// <inheritdoc />
    public VariantSelectionResult SelectBestVariant(string campaignId, IEnumerable<string> variantIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(variantIds);

        var idList = variantIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (idList.Count == 0)
        {
            throw new ArgumentException("At least one valid variant ID is required.", nameof(variantIds));
        }

        var state = _campaigns.GetOrAdd(campaignId, id => new CampaignBanditState(id));
        return state.SelectVariant(idList);
    }

    /// <inheritdoc />
    public VariantFeedbackResult RecordFeedback(string campaignId, string variantId, CopyInteractionEvent interaction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(variantId);
        ArgumentNullException.ThrowIfNull(interaction);

        var state = _campaigns.GetOrAdd(campaignId, id => new CampaignBanditState(id));
        return state.ApplyFeedback(variantId, interaction);
    }

    /// <inheritdoc />
    public CampaignBanditPerformance GetVariantPerformance(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        var state = _campaigns.GetOrAdd(campaignId, id => new CampaignBanditState(id));
        return state.ComputePerformance();
    }

    /// <inheritdoc />
    public void ResetCampaign(string campaignId)
    {
        if (_campaigns.TryRemove(campaignId, out var state))
        {
            state.Reset();
        }
    }

    #region Internal State & Thompson Sampler

    private sealed class CampaignBanditState(string campaignId)
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, VariantArmInternal> _arms = [];

        public string CampaignId { get; } = campaignId;

        public VariantSelectionResult SelectVariant(IReadOnlyList<string> candidateIds)
        {
            lock (_lock)
            {
                // Ensure all candidates exist
                foreach (string id in candidateIds)
                {
                    if (!_arms.ContainsKey(id))
                    {
                        _arms[id] = new VariantArmInternal(id);
                    }
                }

                string? bestArmId = null;
                double maxSample = double.NegativeInfinity;

                foreach (string id in candidateIds)
                {
                    var arm = _arms[id];
                    double sample = ThompsonSampler.SampleBeta(arm.Alpha, arm.Beta);

                    if (sample > maxSample)
                    {
                        maxSample = sample;
                        bestArmId = id;
                    }
                }

                bestArmId ??= candidateIds[0];
                var selected = _arms[bestArmId];
                selected.Impressions++;
                selected.LastUpdatedUtc = DateTime.UtcNow;

                return new VariantSelectionResult(
                    CampaignId: CampaignId,
                    SelectedVariantId: selected.VariantId,
                    SampledTheta: Math.Round(maxSample, 4),
                    ExpectedValue: Math.Round(selected.ExpectedValue, 4),
                    Variance: Math.Round(selected.Variance, 6),
                    SelectedAtUtc: DateTime.UtcNow);
            }
        }

        public VariantFeedbackResult ApplyFeedback(string variantId, CopyInteractionEvent interaction)
        {
            lock (_lock)
            {
                if (!_arms.TryGetValue(variantId, out var arm))
                {
                    arm = new VariantArmInternal(variantId);
                    _arms[variantId] = arm;
                }

                double weight = Math.Max(0.01, interaction.Weight);

                switch (interaction.EventType.Trim().ToLowerInvariant())
                {
                    case "delivered":
                        // Observation increment
                        arm.Beta += 0.05 * weight;
                        break;

                    case "opened":
                        arm.Opens++;
                        arm.Alpha += 0.2 * weight;
                        break;

                    case "clicked":
                        arm.Clicks++;
                        arm.Alpha += 0.5 * weight;
                        break;

                    case "converted":
                        arm.Conversions++;
                        arm.Alpha += 1.0 * weight;
                        break;

                    case "unsubscribed":
                        arm.Unsubscribes++;
                        arm.Beta += 2.0 * weight;
                        break;

                    default:
                        arm.Beta += 0.1 * weight;
                        break;
                }

                arm.LastUpdatedUtc = DateTime.UtcNow;

                return new VariantFeedbackResult(
                    Success: true,
                    CampaignId: CampaignId,
                    VariantId: variantId,
                    NewAlpha: Math.Round(arm.Alpha, 3),
                    NewBeta: Math.Round(arm.Beta, 3),
                    NewExpectedValue: Math.Round(arm.ExpectedValue, 4),
                    Message: $"Bayesian posterior updated for event '{interaction.EventType}'.");
            }
        }

        public CampaignBanditPerformance ComputePerformance()
        {
            lock (_lock)
            {
                if (_arms.Count == 0)
                {
                    return new CampaignBanditPerformance(CampaignId, string.Empty, 0.0, 0.0, 0, []);
                }

                double bestExpectedValue = _arms.Values.Max(a => a.ExpectedValue);
                var bestVariant = _arms.Values.First(a => Math.Abs(a.ExpectedValue - bestExpectedValue) < 1e-9);

                List<VariantPerformanceMetric> metrics = [];
                double totalEstimatedRegret = 0.0;
                int totalInteractions = 0;

                foreach (var arm in _arms.Values)
                {
                    double regret = Math.Max(0.0, bestExpectedValue - arm.ExpectedValue);
                    totalEstimatedRegret += (arm.Impressions * regret);
                    totalInteractions += arm.Impressions + arm.Opens + arm.Clicks + arm.Conversions + arm.Unsubscribes;

                    double cr = arm.Impressions > 0
                        ? (double)arm.Conversions / arm.Impressions
                        : 0.0;

                    metrics.Add(new VariantPerformanceMetric(
                        VariantId: arm.VariantId,
                        Alpha: Math.Round(arm.Alpha, 3),
                        Beta: Math.Round(arm.Beta, 3),
                        ExpectedValue: Math.Round(arm.ExpectedValue, 4),
                        Variance: Math.Round(arm.Variance, 6),
                        ConversionRate: Math.Round(cr, 4),
                        Impressions: arm.Impressions,
                        Conversions: arm.Conversions,
                        Regret: Math.Round(regret, 4)));
                }

                return new CampaignBanditPerformance(
                    CampaignId: CampaignId,
                    BestVariantId: bestVariant.VariantId,
                    BestExpectedValue: Math.Round(bestExpectedValue, 4),
                    TotalEstimatedRegret: Math.Round(totalEstimatedRegret, 4),
                    TotalInteractions: totalInteractions,
                    Variants: metrics);
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _arms.Clear();
            }
        }
    }

    private sealed class VariantArmInternal(string variantId)
    {
        public string VariantId { get; } = variantId;
        public double Alpha { get; set; } = 1.0; // Uninformative prior Beta(1, 1)
        public double Beta { get; set; } = 1.0;
        public int Impressions { get; set; } = 0;
        public int Opens { get; set; } = 0;
        public int Clicks { get; set; } = 0;
        public int Conversions { get; set; } = 0;
        public int Unsubscribes { get; set; } = 0;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        public double ExpectedValue => Alpha / (Alpha + Beta);

        public double Variance => (Alpha * Beta) / (Math.Pow(Alpha + Beta, 2) * (Alpha + Beta + 1));
    }

    /// <summary>
    /// Exact numerical sampling from Beta(alpha, beta) using Gamma variates via Marsaglia &amp; Tsang method.
    /// </summary>
    public static class ThompsonSampler
    {
        public static double SampleBeta(double alpha, double beta, Random? random = null)
        {
            var rng = random ?? Random.Shared;
            alpha = Math.Max(0.001, alpha);
            beta = Math.Max(0.001, beta);

            double x = SampleGamma(alpha, rng);
            double y = SampleGamma(beta, rng);

            double sum = x + y;
            if (sum <= 0.0 || double.IsNaN(sum) || double.IsInfinity(sum))
            {
                return alpha / (alpha + beta);
            }

            return Math.Clamp(x / sum, 0.0, 1.0);
        }

        private static double SampleGamma(double shape, Random random)
        {
            if (shape < 1.0)
            {
                double u = random.NextDouble();
                while (u <= 0.0) u = random.NextDouble();
                return SampleGamma(shape + 1.0, random) * Math.Pow(u, 1.0 / shape);
            }

            double d = shape - (1.0 / 3.0);
            double c = 1.0 / Math.Sqrt(9.0 * d);

            for (int iter = 0; iter < 1000; iter++)
            {
                double u1 = random.NextDouble();
                double u2 = random.NextDouble();
                while (u1 <= 0.0) u1 = random.NextDouble();

                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                double v = 1.0 + (c * z);

                if (v <= 0.0) continue;

                v = v * v * v;
                double u = random.NextDouble();
                while (u <= 0.0) u = random.NextDouble();

                if (u < 1.0 - (0.0331 * z * z * z * z))
                {
                    return d * v;
                }

                if (Math.Log(u) < (0.5 * z * z) + (d * (1.0 - v + Math.Log(v))))
                {
                    return d * v;
                }
            }

            return shape;
        }
    }

    #endregion
}
