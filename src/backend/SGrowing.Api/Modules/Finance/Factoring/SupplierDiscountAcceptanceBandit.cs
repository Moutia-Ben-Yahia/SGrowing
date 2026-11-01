using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Finance.Factoring;

/// <summary>
/// Core contract for the Contextual Multi-Armed Bandit using Beta-Bernoulli Thompson Sampling
/// to optimize supplier dynamic discounting rates against cash urgency profiles.
/// </summary>
public interface ISupplierDiscountAcceptanceBandit
{
    decimal PredictOptimalDiscountRate(string supplierId, SupplierCashUrgencyProfile profile);

    BanditOptimizeResponse GetOptimalDiscountRecommendation(string supplierId, SupplierCashUrgencyProfile profile);

    void RecordSupplierDecision(string supplierId, decimal offeredDiscountRate, bool accepted);

    void RecordSupplierOutcome(string supplierId, decimal offeredDiscountRate, SupplierOfferOutcome outcome);

    SupplierBanditSummary GetSupplierBanditState(string supplierId);

    IReadOnlyList<decimal> GetArms();
}

/// <summary>
/// Mutable Bayesian Beta distribution state for an individual discount rate arm.
/// </summary>
internal sealed class ArmDistributionState
{
    public required decimal DiscountRate { get; init; }
    public double Alpha { get; set; } = 1.0;
    public double Beta { get; set; } = 1.0;
    public long TotalPulls { get; set; }
    public long Successes { get; set; }

    public double Mean => Alpha / (Alpha + Beta);

    public BanditArmDetail ToDto() => new(
        DiscountRatePct: Math.Round(DiscountRate * 100m, 2),
        Alpha: Math.Round(Alpha, 2),
        Beta: Math.Round(Beta, 2),
        TotalPulls: TotalPulls,
        Successes: Successes,
        MeanAcceptanceRate: Math.Round(Mean, 4)
    );
}

/// <summary>
/// Production-grade Contextual Multi-Armed Bandit implementing Beta-Bernoulli Thompson Sampling.
/// Balances buyer APR yield maximization with supplier acceptance probability conditioned on
/// working capital cash urgency tiers and historical liquidity behaviors.
/// </summary>
public sealed class SupplierDiscountAcceptanceBandit : ISupplierDiscountAcceptanceBandit
{
    /// <summary>
    /// Default discretized discount arms (1.0%, 1.5%, 2.0%, 2.5%, 3.0%).
    /// </summary>
    public static readonly decimal[] DefaultArms = [0.010m, 0.015m, 0.020m, 0.025m, 0.030m];

    private readonly decimal[] _arms;
    private readonly Lock _banditLock = new();
    private readonly ConcurrentDictionary<string, Dictionary<decimal, ArmDistributionState>> _supplierDistributions = new();
    private readonly Dictionary<decimal, ArmDistributionState> _globalDistributions = new();
    private readonly Random _random;

    public SupplierDiscountAcceptanceBandit(decimal[]? customArms = null, int? seed = null)
    {
        _arms = customArms is { Length: > 0 } ? customArms : DefaultArms;
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;

        foreach (var arm in _arms)
        {
            _globalDistributions[arm] = new ArmDistributionState
            {
                DiscountRate = arm,
                Alpha = 1.0,
                Beta = 1.0,
                TotalPulls = 0,
                Successes = 0
            };
        }
    }

    /// <summary>
    /// Predicts the optimal discount rate (e.g. 0.020m for 2.0%) using contextual Thompson Sampling.
    /// </summary>
    public decimal PredictOptimalDiscountRate(string supplierId, SupplierCashUrgencyProfile profile)
    {
        var recommendation = GetOptimalDiscountRecommendation(supplierId, profile);
        return recommendation.RecommendedDiscountRatePct / 100m;
    }

    /// <summary>
    /// Computes full contextual Thompson Sampling recommendation with sampled posterior probabilities,
    /// buyer expected utility ranking, and strategic explanation.
    /// </summary>
    public BanditOptimizeResponse GetOptimalDiscountRecommendation(string supplierId, SupplierCashUrgencyProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierId);
        ArgumentNullException.ThrowIfNull(profile);

        lock (_banditLock)
        {
            var armStates = GetOrCreateSupplierArms(supplierId, profile);

            decimal bestArm = _arms[0];
            double maxUtility = double.MinValue;
            double bestProbability = 0.0;

            var isCritical = profile.WorkingCapitalBand.Equals("Critical", StringComparison.OrdinalIgnoreCase) || profile.UrgencyScore >= 0.70;
            var isLowUrgency = profile.WorkingCapitalBand.Equals("Low", StringComparison.OrdinalIgnoreCase) || profile.UrgencyScore <= 0.30;

            foreach (var arm in _arms)
            {
                // Enforce maximum policy ceiling if configured
                if (profile.RecommendedDiscountMaxPct > 0m && (arm * 100m) > profile.RecommendedDiscountMaxPct)
                {
                    continue;
                }

                var state = armStates[arm];

                // Thompson Sampling: sample posterior acceptance probability from Beta(alpha, beta)
                var sampledTheta = SampleBeta(state.Alpha, state.Beta, _random);

                // Contextual urgency adjustments:
                // Critical suppliers accept higher discounts to access immediate working capital.
                // Low urgency suppliers are rate-sensitive and will reject steep discounts.
                double adjustedProbability;
                if (isCritical)
                {
                    var urgencyBoost = 0.30 + (profile.UrgencyScore * 0.20);
                    adjustedProbability = Math.Clamp(sampledTheta * (1.0 + urgencyBoost), 0.05, 0.99);
                }
                else if (isLowUrgency)
                {
                    // Rate sensitivity penalty on higher discount rates
                    var penaltyFactor = arm > 0.015m ? (double)(arm * 12.0m) : 0.0;
                    adjustedProbability = Math.Clamp(sampledTheta * (1.0 - (penaltyFactor * 0.25)), 0.01, 0.95);
                }
                else
                {
                    // Moderate urgency
                    adjustedProbability = Math.Clamp(sampledTheta, 0.01, 0.99);
                }

                // Expected buyer utility: AcceptanceProbability * DiscountRate
                // Reflects expected savings capture per DZD invoiced
                var expectedUtility = adjustedProbability * (double)arm;

                if (expectedUtility > maxUtility)
                {
                    maxUtility = expectedUtility;
                    bestArm = arm;
                    bestProbability = adjustedProbability;
                }
            }

            var discountPct = Math.Round(bestArm * 100m, 2);
            var rationale = isCritical
                ? $"Supplier in Critical liquidity state (Urgency: {profile.UrgencyScore:P0}). Optimized for elevated discount rate {discountPct}% with strong acceptance probability {bestProbability:P1}."
                : isLowUrgency
                    ? $"Supplier with Low cash urgency. Discount rate moderated to {discountPct}% to preserve supplier relationship and avoid offer rejection."
                    : $"Balanced yield optimization under Moderate working capital urgency: {discountPct}% APR target.";

            return new BanditOptimizeResponse(
                SupplierId: supplierId,
                RecommendedDiscountRatePct: discountPct,
                EstimatedAcceptanceProbability: Math.Round(bestProbability, 4),
                UrgencyBand: profile.WorkingCapitalBand,
                StrategyRationale: rationale
            );
        }
    }

    /// <summary>
    /// Ingests binary acceptance feedback from supplier for a specific offered discount rate.
    /// </summary>
    public void RecordSupplierDecision(string supplierId, decimal offeredDiscountRate, bool accepted)
    {
        RecordSupplierOutcome(
            supplierId,
            offeredDiscountRate,
            accepted ? SupplierOfferOutcome.Accepted : SupplierOfferOutcome.Rejected);
    }

    /// <summary>
    /// Ingests tri-state outcome feedback:
    ///   - Accepted: +1.0 success to alpha
    ///   - Rejected: +1.0 failure to beta
    ///   - Expired:  +0.5 failure to beta (partial indifference / passive rejection)
    /// </summary>
    public void RecordSupplierOutcome(string supplierId, decimal offeredDiscountRate, SupplierOfferOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierId);

        lock (_banditLock)
        {
            var matchingArm = FindClosestArm(offeredDiscountRate);
            var armStates = GetOrCreateSupplierArms(supplierId);

            var state = armStates[matchingArm];
            var globalState = _globalDistributions[matchingArm];

            state.TotalPulls++;
            globalState.TotalPulls++;

            switch (outcome)
            {
                case SupplierOfferOutcome.Accepted:
                    state.Alpha += 1.0;
                    state.Successes++;
                    globalState.Alpha += 0.2; // Inductive cross-supplier transfer
                    globalState.Successes++;
                    break;

                case SupplierOfferOutcome.Rejected:
                    state.Beta += 1.0;
                    globalState.Beta += 0.2;
                    break;

                case SupplierOfferOutcome.Expired:
                    state.Beta += 0.5;
                    globalState.Beta += 0.1;
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the Bayesian arm parameters and empirical acceptance rates for a supplier.
    /// </summary>
    public SupplierBanditSummary GetSupplierBanditState(string supplierId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierId);

        lock (_banditLock)
        {
            var armStates = GetOrCreateSupplierArms(supplierId);
            var armDtos = armStates.Values.Select(a => a.ToDto()).ToList();

            return new SupplierBanditSummary(supplierId, armDtos);
        }
    }

    /// <summary>
    /// Returns configured discrete discount arms.
    /// </summary>
    public IReadOnlyList<decimal> GetArms() => _arms;

    private Dictionary<decimal, ArmDistributionState> GetOrCreateSupplierArms(
        string supplierId,
        SupplierCashUrgencyProfile? profile = null)
    {
        if (_supplierDistributions.TryGetValue(supplierId, out var existing))
        {
            return existing;
        }

        var newDict = new Dictionary<decimal, ArmDistributionState>();
        var priorAlpha = 1.0;
        var priorBeta = 1.0;

        if (profile is { HistoricalAcceptanceRate: > 0.0 })
        {
            // Seed priors informed by historical acceptance rate
            priorAlpha = Math.Clamp(profile.HistoricalAcceptanceRate * 4.0, 1.0, 5.0);
            priorBeta = Math.Clamp((1.0 - profile.HistoricalAcceptanceRate) * 4.0, 1.0, 5.0);
        }

        foreach (var arm in _arms)
        {
            newDict[arm] = new ArmDistributionState
            {
                DiscountRate = arm,
                Alpha = priorAlpha,
                Beta = priorBeta,
                TotalPulls = 0,
                Successes = 0
            };
        }

        _supplierDistributions[supplierId] = newDict;
        return newDict;
    }

    private decimal FindClosestArm(decimal rate)
    {
        // Normalize if passed as percentage (e.g. 2.0 vs 0.02)
        var normalizedRate = rate > 0.10m ? rate / 100m : rate;
        return _arms.OrderBy(a => Math.Abs(a - normalizedRate)).First();
    }

    /// <summary>
    /// Marsaglia and Tsang method for sampling from Gamma distribution, followed by Beta ratio.
    /// For X ~ Gamma(alpha, 1) and Y ~ Gamma(beta, 1), Z = X / (X + Y) ~ Beta(alpha, beta).
    /// </summary>
    private static double SampleBeta(double alpha, double beta, Random rng)
    {
        var x = SampleGamma(alpha, 1.0, rng);
        var y = SampleGamma(beta, 1.0, rng);

        if (x + y <= 0.0)
        {
            return 0.5;
        }

        return Math.Clamp(x / (x + y), 0.001, 0.999);
    }

    private static double SampleGamma(double shape, double scale, Random rng)
    {
        if (shape < 1.0)
        {
            var u = Math.Max(1e-10, rng.NextDouble());
            return SampleGamma(shape + 1.0, scale, rng) * Math.Pow(u, 1.0 / shape);
        }

        var d = shape - (1.0 / 3.0);
        var c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z;
            double v;

            do
            {
                // Box-Muller standard normal transform
                var u1 = Math.Max(1e-10, rng.NextDouble());
                var u2 = rng.NextDouble();
                z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                v = 1.0 + (c * z);
            } while (v <= 0.0);

            v = v * v * v;
            var u = rng.NextDouble();

            if (u < 1.0 - (0.0331 * z * z * z * z))
            {
                return d * v * scale;
            }

            if (Math.Log(u) < (0.5 * z * z) + (d * (1.0 - v + Math.Log(v))))
            {
                return d * v * scale;
            }
        }
    }
}
