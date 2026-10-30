using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Funnels;

/// <summary>
/// Funnel step outcomes in the Dirichlet-Multinomial model.
/// K = 5 mutually exclusive terminal outcomes for each visitor interaction.
/// </summary>
public enum FunnelStepOutcome
{
    Bounce = 0,
    LeadCaptured = 1,
    CheckoutInitiated = 2,
    OrderPaid = 3,
    UpsellAccepted = 4
}

/// <summary>
/// Configuration of values for each outcome step in a funnel.
/// </summary>
public record FunnelOutcomeValueConfig(
    decimal Bounce = 0.00m,
    decimal LeadCaptured = 2.50m,
    decimal CheckoutInitiated = 15.00m,
    decimal OrderPaid = 97.00m,
    decimal UpsellAccepted = 197.00m
)
{
    public decimal GetValue(FunnelStepOutcome outcome) => outcome switch
    {
        FunnelStepOutcome.Bounce => Bounce,
        FunnelStepOutcome.LeadCaptured => LeadCaptured,
        FunnelStepOutcome.CheckoutInitiated => CheckoutInitiated,
        FunnelStepOutcome.OrderPaid => OrderPaid,
        FunnelStepOutcome.UpsellAccepted => UpsellAccepted,
        _ => 0.00m
    };

    public decimal[] ToArray() => new[]
    {
        Bounce,
        LeadCaptured,
        CheckoutInitiated,
        OrderPaid,
        UpsellAccepted
    };
}

/// <summary>
/// State of a single funnel variant in the Dirichlet-Multinomial bandit.
/// </summary>
public class FunnelVariant
{
    private readonly object _lock = new();

    public string VariantId { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBaseline { get; set; }
    public bool IsWinner { get; set; }
    public bool IsCannibalized { get; set; }

    /// <summary>
    /// Dirichlet parameter vector alpha for outcomes [Bounce, Lead, Checkout, Paid, Upsell].
    /// </summary>
    public double[] Alphas { get; set; } = new double[5];

    /// <summary>
    /// Initial baseline prior alphas for reset.
    /// </summary>
    public double[] InitialAlphas { get; set; } = new double[5];

    public int TotalVisitors { get; set; }
    public decimal TotalRevenue { get; set; }

    public double WinProbability { get; set; }
    public double SuperiorityVsBaseline { get; set; }
    public decimal ExpectedRpv { get; set; }
    public decimal CredibleIntervalLow { get; set; }
    public decimal CredibleIntervalHigh { get; set; }
    public double PaidConversionRate { get; set; }
    public double LeadConversionRate { get; set; }
    public double RecommendedTrafficShare { get; set; }

    public FunnelVariant Clone()
    {
        lock (_lock)
        {
            return new FunnelVariant
            {
                VariantId = VariantId,
                VariantName = VariantName,
                Description = Description,
                IsBaseline = IsBaseline,
                IsWinner = IsWinner,
                IsCannibalized = IsCannibalized,
                Alphas = (double[])Alphas.Clone(),
                InitialAlphas = (double[])InitialAlphas.Clone(),
                TotalVisitors = TotalVisitors,
                TotalRevenue = TotalRevenue,
                WinProbability = WinProbability,
                SuperiorityVsBaseline = SuperiorityVsBaseline,
                ExpectedRpv = ExpectedRpv,
                CredibleIntervalLow = CredibleIntervalLow,
                CredibleIntervalHigh = CredibleIntervalHigh,
                PaidConversionRate = PaidConversionRate,
                LeadConversionRate = LeadConversionRate,
                RecommendedTrafficShare = RecommendedTrafficShare
            };
        }
    }

    public void RecordOutcomeAtomic(FunnelStepOutcome outcome, decimal stepValue)
    {
        lock (_lock)
        {
            int idx = (int)outcome;
            if (idx >= 0 && idx < Alphas.Length)
            {
                Alphas[idx] += 1.0;
            }
            TotalVisitors++;
            TotalRevenue += stepValue;
        }
    }

    public void ResetAtomic()
    {
        lock (_lock)
        {
            Alphas = (double[])InitialAlphas.Clone();
            TotalVisitors = (int)Alphas.Sum() - Alphas.Length; // Adjusted for prior counts
            if (TotalVisitors < 0) TotalVisitors = 0;
            TotalRevenue = 0m;
            IsWinner = false;
            IsCannibalized = false;
            WinProbability = 0;
            SuperiorityVsBaseline = 0;
        }
    }

    public double[] GetAlphasCopy()
    {
        lock (_lock)
        {
            return (double[])Alphas.Clone();
        }
    }
}

/// <summary>
/// A funnel split-testing experiment containing multiple variants and Dirichlet-Multinomial configuration.
/// </summary>
public class FunnelExperiment
{
    private readonly object _experimentLock = new();

    public string FunnelId { get; set; } = string.Empty;
    public string FunnelName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public FunnelOutcomeValueConfig OutcomeValues { get; set; } = new();
    public double ExplorationFloor { get; set; } = 0.05; // 5% epsilon floor
    public int MinVisitorThreshold { get; set; } = 100; // Minimum visitors before auto-cannibalization can trigger
    public double SuperiorityThreshold { get; set; } = 0.95; // 95% posterior superiority threshold
    public bool AutoCannibalizationActive { get; set; }
    public string? WinningVariantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public List<FunnelVariant> Variants { get; set; } = new();

    public FunnelExperiment Clone()
    {
        lock (_experimentLock)
        {
            return new FunnelExperiment
            {
                FunnelId = FunnelId,
                FunnelName = FunnelName,
                Description = Description,
                Currency = Currency,
                OutcomeValues = OutcomeValues,
                ExplorationFloor = ExplorationFloor,
                MinVisitorThreshold = MinVisitorThreshold,
                SuperiorityThreshold = SuperiorityThreshold,
                AutoCannibalizationActive = AutoCannibalizationActive,
                WinningVariantId = WinningVariantId,
                CreatedAt = CreatedAt,
                LastActivityAt = LastActivityAt,
                Variants = Variants.Select(v => v.Clone()).ToList()
            };
        }
    }

    public void UpdateMetrics(List<FunnelVariant> updatedVariants, bool cannibalizationActive, string? winningId)
    {
        lock (_experimentLock)
        {
            AutoCannibalizationActive = cannibalizationActive;
            WinningVariantId = winningId;
            LastActivityAt = DateTime.UtcNow;
        }
    }
}

// Request and Response DTOs
public record RouteVisitorRequest(string FunnelId, string? VisitorId = null);

public record RouteVisitorResult(
    string FunnelId,
    string VisitorId,
    string SelectedVariantId,
    string SelectedVariantName,
    string RoutingReason, // "ThompsonSampling" | "ExplorationFloor" | "CannibalizationFloor"
    Dictionary<string, decimal> SampledRpvs,
    double ExplorationRate,
    DateTime Timestamp
);

public record RecordOutcomeRequest(
    string FunnelId,
    string VariantId,
    FunnelStepOutcome Outcome,
    decimal? CustomValue = null,
    string? VisitorId = null
);

public record RecordOutcomeResult(
    string FunnelId,
    string VariantId,
    FunnelStepOutcome Outcome,
    decimal AttributedValue,
    int TotalVisitors,
    double[] UpdatedAlphas,
    decimal ExpectedRpv,
    double PaidConversionRate,
    double LeadConversionRate,
    bool AutoCannibalizationActive,
    string? WinningVariantId,
    string StatusMessage
);

public record SimulateSuperiorityRequest(
    string FunnelId,
    int Iterations = 1000,
    int? MinVisitorThreshold = null
);

public record VariantSimulationReport(
    string VariantId,
    string VariantName,
    double WinProbability,
    double SuperiorityVsBaseline,
    decimal ExpectedRpv,
    decimal CredibleIntervalLow,
    decimal CredibleIntervalHigh,
    int TotalVisitors,
    decimal TotalRevenue,
    double PaidConversionRate,
    double LeadConversionRate,
    bool IsWinner,
    bool IsCannibalized,
    double RecommendedTrafficShare
);

public record SimulateSuperiorityResponse(
    string FunnelId,
    int Iterations,
    string BaselineVariantId,
    List<VariantSimulationReport> Variants,
    bool AutoCannibalizationActive,
    string? WinningVariantId,
    string DecisionSummary,
    DateTime Timestamp
);

public record FunnelExperimentSummaryDto(
    string FunnelId,
    string FunnelName,
    string Description,
    string Currency,
    int TotalVisitors,
    decimal TotalRevenue,
    int VariantCount,
    bool AutoCannibalizationActive,
    string? WinningVariantId,
    DateTime LastActivityAt
);

/// <summary>
/// Mathematical sampler for Gamma and Dirichlet distributions using Marsaglia & Tsang (2000).
/// </summary>
public static class DirichletSampler
{
    /// <summary>
    /// Generates a standard Gamma variate with shape parameter alpha and scale 1.0
    /// using Marsaglia and Tsang's method (2000), with Johnk / transformation for alpha < 1.
    /// </summary>
    public static double SampleGamma(double alpha, Random? rng = null)
    {
        rng ??= Random.Shared;

        if (alpha <= 0.0 || double.IsNaN(alpha) || double.IsInfinity(alpha))
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha parameter must be strictly positive and finite.");
        }

        // For alpha < 1, use Marsaglia & Tsang transformation: Gamma(alpha) = Gamma(alpha + 1) * U^(1 / alpha)
        if (alpha < 1.0)
        {
            double gammaPlusOne = SampleGamma(alpha + 1.0, rng);
            double u = NextUniformNonZero(rng);
            return gammaPlusOne * Math.Pow(u, 1.0 / alpha);
        }

        // Marsaglia & Tsang algorithm for alpha >= 1
        double d = alpha - (1.0 / 3.0);
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z = SampleStandardNormal(rng);
            double v = 1.0 + (c * z);

            if (v <= 0.0)
                continue;

            double v3 = v * v * v;
            double u = NextUniformNonZero(rng);

            // Fast squeeze test
            if (u < 1.0 - (0.0331 * z * z * z * z))
                return d * v3;

            // Log acceptance condition
            if (Math.Log(u) < (0.5 * z * z) + (d * (1.0 - v3 + Math.Log(v3))))
                return d * v3;
        }
    }

    /// <summary>
    /// Standard Normal random generator using Box-Muller transform.
    /// </summary>
    public static double SampleStandardNormal(Random? rng = null)
    {
        rng ??= Random.Shared;
        double u1 = NextUniformNonZero(rng);
        double u2 = NextUniformNonZero(rng);
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Generates uniform (0, 1) strictly avoiding boundary 0.0 and 1.0.
    /// </summary>
    public static double NextUniformNonZero(Random rng)
    {
        double val;
        do
        {
            val = rng.NextDouble();
        } while (val <= 1e-12 || val >= 1.0);
        return val;
    }

    /// <summary>
    /// Samples a probability vector p ~ Dirichlet(alphas) where sum(p) == 1.0.
    /// </summary>
    public static double[] SampleDirichlet(double[] alphas, Random? rng = null)
    {
        if (alphas == null || alphas.Length == 0)
            throw new ArgumentException("Alpha parameters cannot be empty.", nameof(alphas));

        rng ??= Random.Shared;
        var samples = new double[alphas.Length];
        double sum = 0.0;

        for (int i = 0; i < alphas.Length; i++)
        {
            double alpha = alphas[i];
            if (alpha <= 0.0)
                alpha = 0.001; // Numerical floor

            double g = SampleGamma(alpha, rng);
            samples[i] = g;
            sum += g;
        }

        if (sum <= 0.0 || double.IsNaN(sum) || double.IsInfinity(sum))
        {
            // Fallback to uniform distribution if numerical issue occurs
            double uniform = 1.0 / alphas.Length;
            for (int i = 0; i < samples.Length; i++)
                samples[i] = uniform;
            return samples;
        }

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] /= sum;
        }

        return samples;
    }
}

/// <summary>
/// Service contract for Dirichlet-Multinomial Multi-Armed Funnel Split Testing.
/// </summary>
public interface IFunnelSplitTestingBanditService
{
    IReadOnlyList<FunnelExperimentSummaryDto> GetAllExperiments();
    FunnelExperiment? GetExperiment(string funnelId);
    RouteVisitorResult RouteVisitor(string funnelId, string? visitorId = null);
    RecordOutcomeResult RecordOutcome(string funnelId, string variantId, FunnelStepOutcome outcome, decimal? customValue = null, string? visitorId = null);
    SimulateSuperiorityResponse RunSuperioritySimulation(string funnelId, int iterations = 1000, int? minVisitorThreshold = null);
    void ResetExperiment(string funnelId);
}

/// <summary>
/// In-memory, thread-safe implementation of Dirichlet-Multinomial Multi-Armed Bandit Funnel Split Testing.
/// </summary>
public class FunnelSplitTestingBanditService : IFunnelSplitTestingBanditService
{
    private readonly ConcurrentDictionary<string, FunnelExperiment> _experiments = new();
    private readonly Random _rng = new();
    private readonly object _serviceLock = new();

    public FunnelSplitTestingBanditService()
    {
        SeedRealisticExperiments();
    }

    public IReadOnlyList<FunnelExperimentSummaryDto> GetAllExperiments()
    {
        return _experiments.Values.Select(exp =>
        {
            var clone = exp.Clone();
            int totalVisitors = clone.Variants.Sum(v => v.TotalVisitors);
            decimal totalRevenue = clone.Variants.Sum(v => v.TotalRevenue);

            return new FunnelExperimentSummaryDto(
                clone.FunnelId,
                clone.FunnelName,
                clone.Description,
                clone.Currency,
                totalVisitors,
                totalRevenue,
                clone.Variants.Count,
                clone.AutoCannibalizationActive,
                clone.WinningVariantId,
                clone.LastActivityAt
            );
        }).ToList();
    }

    public FunnelExperiment? GetExperiment(string funnelId)
    {
        if (string.IsNullOrWhiteSpace(funnelId))
            return null;

        if (_experiments.TryGetValue(funnelId, out var experiment))
        {
            return experiment.Clone();
        }

        return null;
    }

    public RouteVisitorResult RouteVisitor(string funnelId, string? visitorId = null)
    {
        visitorId ??= $"vis_{Guid.NewGuid():N}";

        if (!_experiments.TryGetValue(funnelId, out var experiment))
        {
            throw new KeyNotFoundException($"Funnel experiment with ID '{funnelId}' not found.");
        }

        var activeVariants = experiment.Variants.ToList();
        if (activeVariants.Count == 0)
        {
            throw new InvalidOperationException($"No variants available in funnel '{funnelId}'.");
        }

        double randomDraw;
        lock (_rng)
        {
            randomDraw = _rng.NextDouble();
        }

        FunnelVariant selectedVariant;
        string routingReason;
        var sampledRpvs = new Dictionary<string, decimal>();

        // Sample Thompson RPV for all variants regardless, for transparency & telemetry
        foreach (var variant in activeVariants)
        {
            double[] dirichletSample = DirichletSampler.SampleDirichlet(variant.GetAlphasCopy());
            decimal sampledRpv = CalculateSampledRpv(dirichletSample, experiment.OutcomeValues);
            sampledRpvs[variant.VariantId] = Math.Round(sampledRpv, 4);
        }

        // 1. Auto-Cannibalization Floor Check:
        // If an arm has won with >= 95% superiority and is actively cannibalizing traffic,
        // it captures (1 - epsilon) of the traffic directly, with epsilon reserved for ongoing safety checks.
        if (experiment.AutoCannibalizationActive && !string.IsNullOrEmpty(experiment.WinningVariantId))
        {
            var winner = activeVariants.FirstOrDefault(v => v.VariantId == experiment.WinningVariantId);
            if (winner != null && randomDraw >= experiment.ExplorationFloor)
            {
                selectedVariant = winner;
                routingReason = "CannibalizationFloor";
                experiment.LastActivityAt = DateTime.UtcNow;

                return new RouteVisitorResult(
                    funnelId,
                    visitorId,
                    selectedVariant.VariantId,
                    selectedVariant.VariantName,
                    routingReason,
                    sampledRpvs,
                    experiment.ExplorationFloor,
                    DateTime.UtcNow
                );
            }
        }

        // 2. Exploration Floor (Epsilon = 0.05): With probability epsilon, choose uniformly at random
        if (randomDraw < experiment.ExplorationFloor)
        {
            int randomIndex;
            lock (_rng)
            {
                randomIndex = _rng.Next(activeVariants.Count);
            }
            selectedVariant = activeVariants[randomIndex];
            routingReason = "ExplorationFloor";
        }
        else
        {
            // 3. Thompson Sampling: Argmax of sampled RPV
            string bestVariantId = sampledRpvs.MaxBy(kv => kv.Value).Key;
            selectedVariant = activeVariants.First(v => v.VariantId == bestVariantId);
            routingReason = "ThompsonSampling";
        }

        experiment.LastActivityAt = DateTime.UtcNow;

        return new RouteVisitorResult(
            funnelId,
            visitorId,
            selectedVariant.VariantId,
            selectedVariant.VariantName,
            routingReason,
            sampledRpvs,
            experiment.ExplorationFloor,
            DateTime.UtcNow
        );
    }

    public RecordOutcomeResult RecordOutcome(
        string funnelId,
        string variantId,
        FunnelStepOutcome outcome,
        decimal? customValue = null,
        string? visitorId = null)
    {
        if (!_experiments.TryGetValue(funnelId, out var experiment))
        {
            throw new KeyNotFoundException($"Funnel experiment with ID '{funnelId}' not found.");
        }

        var variant = experiment.Variants.FirstOrDefault(v => v.VariantId.Equals(variantId, StringComparison.OrdinalIgnoreCase));
        if (variant == null)
        {
            throw new KeyNotFoundException($"Variant '{variantId}' not found in funnel '{funnelId}'.");
        }

        decimal stepValue = customValue ?? experiment.OutcomeValues.GetValue(outcome);

        // Atomic update of Dirichlet parameter alpha_{v, outcome} += 1
        variant.RecordOutcomeAtomic(outcome, stepValue);

        // Recalculate posterior mean metrics for this variant
        double[] alphas = variant.GetAlphasCopy();
        double alphaSum = alphas.Sum();
        decimal expectedRpv = CalculatePosteriorMeanRpv(alphas, experiment.OutcomeValues);
        variant.ExpectedRpv = expectedRpv;

        // Paid conversion: OrderPaid + UpsellAccepted
        variant.PaidConversionRate = alphaSum > 0 ? (alphas[(int)FunnelStepOutcome.OrderPaid] + alphas[(int)FunnelStepOutcome.UpsellAccepted]) / alphaSum : 0;
        // Lead conversion: Lead + Checkout + OrderPaid + UpsellAccepted
        variant.LeadConversionRate = alphaSum > 0 ? (alphaSum - alphas[(int)FunnelStepOutcome.Bounce]) / alphaSum : 0;

        // Trigger fast Monte Carlo evaluation check if visitors >= MinVisitorThreshold
        string statusMessage = $"Outcome '{outcome}' (+${stepValue:F2}) recorded for Variant {variant.VariantId}.";

        if (variant.TotalVisitors >= experiment.MinVisitorThreshold)
        {
            // Run quick simulation to update superiority and check cannibalization
            var simResult = RunSuperioritySimulation(funnelId, iterations: 1000, minVisitorThreshold: experiment.MinVisitorThreshold);
            if (simResult.AutoCannibalizationActive && simResult.WinningVariantId == variant.VariantId)
            {
                statusMessage += $" AUTO-CANNIBALIZATION ACTIVATED: Variant {variant.VariantId} reached {simResult.Variants.First(v => v.VariantId == variant.VariantId).SuperiorityVsBaseline * 100:F1}% superiority.";
            }
        }

        return new RecordOutcomeResult(
            funnelId,
            variant.VariantId,
            outcome,
            stepValue,
            variant.TotalVisitors,
            alphas,
            variant.ExpectedRpv,
            variant.PaidConversionRate,
            variant.LeadConversionRate,
            experiment.AutoCannibalizationActive,
            experiment.WinningVariantId,
            statusMessage
        );
    }

    public SimulateSuperiorityResponse RunSuperioritySimulation(
        string funnelId,
        int iterations = 1000,
        int? minVisitorThreshold = null)
    {
        if (!_experiments.TryGetValue(funnelId, out var experiment))
        {
            throw new KeyNotFoundException($"Funnel experiment with ID '{funnelId}' not found.");
        }

        int simIterations = Math.Max(500, iterations);
        int visitorThreshold = minVisitorThreshold ?? experiment.MinVisitorThreshold;

        var variants = experiment.Variants.ToList();
        var baseline = variants.FirstOrDefault(v => v.IsBaseline) ?? variants.First();

        var winCounts = new Dictionary<string, int>();
        var superiorityVsBaselineCounts = new Dictionary<string, int>();
        var rpvDraws = new Dictionary<string, List<decimal>>();

        foreach (var v in variants)
        {
            winCounts[v.VariantId] = 0;
            superiorityVsBaselineCounts[v.VariantId] = 0;
            rpvDraws[v.VariantId] = new List<decimal>(simIterations);
        }

        var rng = new Random();

        // Run Monte Carlo simulation across Dirichlet-Multinomial samples
        for (int i = 0; i < simIterations; i++)
        {
            var iterationRpvs = new Dictionary<string, decimal>();

            foreach (var v in variants)
            {
                double[] p = DirichletSampler.SampleDirichlet(v.GetAlphasCopy(), rng);
                decimal rpv = CalculateSampledRpv(p, experiment.OutcomeValues);
                iterationRpvs[v.VariantId] = rpv;
                rpvDraws[v.VariantId].Add(rpv);
            }

            // Winner of this draw
            string drawWinner = iterationRpvs.MaxBy(kv => kv.Value).Key;
            winCounts[drawWinner]++;

            // Superiority vs baseline
            decimal baselineRpv = iterationRpvs[baseline.VariantId];
            foreach (var v in variants)
            {
                if (iterationRpvs[v.VariantId] > baselineRpv)
                {
                    superiorityVsBaselineCounts[v.VariantId]++;
                }
            }
        }

        // Compute metrics, credible intervals, and check cannibalization
        bool cannibalizationActive = false;
        string? winningVariantId = null;
        var simulationReports = new List<VariantSimulationReport>();

        foreach (var v in variants)
        {
            var draws = rpvDraws[v.VariantId];
            draws.Sort();

            int lowIdx = (int)Math.Floor(0.025 * simIterations);
            int highIdx = (int)Math.Floor(0.975 * simIterations);
            if (highIdx >= draws.Count) highIdx = draws.Count - 1;

            decimal ciLow = draws[lowIdx];
            decimal ciHigh = draws[highIdx];

            double winProb = (double)winCounts[v.VariantId] / simIterations;
            double superiority = v.VariantId == baseline.VariantId
                ? 0.50
                : (double)superiorityVsBaselineCounts[v.VariantId] / simIterations;

            double[] alphas = v.GetAlphasCopy();
            double alphaSum = alphas.Sum();
            decimal expRpv = CalculatePosteriorMeanRpv(alphas, experiment.OutcomeValues);

            double paidConv = alphaSum > 0 ? (alphas[(int)FunnelStepOutcome.OrderPaid] + alphas[(int)FunnelStepOutcome.UpsellAccepted]) / alphaSum : 0;
            double leadConv = alphaSum > 0 ? (alphaSum - alphas[(int)FunnelStepOutcome.Bounce]) / alphaSum : 0;

            v.WinProbability = winProb;
            v.SuperiorityVsBaseline = superiority;
            v.ExpectedRpv = expRpv;
            v.CredibleIntervalLow = ciLow;
            v.CredibleIntervalHigh = ciHigh;
            v.PaidConversionRate = paidConv;
            v.LeadConversionRate = leadConv;

            // Check auto-cannibalization threshold
            if (v.TotalVisitors >= visitorThreshold && (superiority >= experiment.SuperiorityThreshold || winProb >= experiment.SuperiorityThreshold))
            {
                cannibalizationActive = true;
                winningVariantId = v.VariantId;
            }
        }

        // Apply Winner and Cannibalized flags & traffic shares
        foreach (var v in variants)
        {
            if (cannibalizationActive && v.VariantId == winningVariantId)
            {
                v.IsWinner = true;
                v.IsCannibalized = false;
                // Winner absorbs (1 - epsilon) of traffic
                v.RecommendedTrafficShare = Math.Round(1.0 - experiment.ExplorationFloor, 4);
            }
            else if (cannibalizationActive)
            {
                v.IsWinner = false;
                v.IsCannibalized = true;
                // Inferior arms share the remaining exploration pool
                int nonWinners = variants.Count - 1;
                v.RecommendedTrafficShare = Math.Round(experiment.ExplorationFloor / Math.Max(1, nonWinners), 4);
            }
            else
            {
                v.IsWinner = false;
                v.IsCannibalized = false;
                // Proportional to win probability with exploration floor
                double share = ((1.0 - experiment.ExplorationFloor) * v.WinProbability) + (experiment.ExplorationFloor / variants.Count);
                v.RecommendedTrafficShare = Math.Round(share, 4);
            }

            simulationReports.Add(new VariantSimulationReport(
                v.VariantId,
                v.VariantName,
                Math.Round(v.WinProbability, 4),
                Math.Round(v.SuperiorityVsBaseline, 4),
                v.ExpectedRpv,
                v.CredibleIntervalLow,
                v.CredibleIntervalHigh,
                v.TotalVisitors,
                v.TotalRevenue,
                Math.Round(v.PaidConversionRate, 4),
                Math.Round(v.LeadConversionRate, 4),
                v.IsWinner,
                v.IsCannibalized,
                v.RecommendedTrafficShare
            ));
        }

        experiment.UpdateMetrics(variants, cannibalizationActive, winningVariantId);

        string decisionSummary = cannibalizationActive
            ? $"Variant {winningVariantId} has achieved statistically decisive superiority (>= {experiment.SuperiorityThreshold * 100:F0}%). Auto-Cannibalization is ACTIVE: traffic is channeled to variant {winningVariantId} ({(1.0 - experiment.ExplorationFloor) * 100:F0}% share) while inferior variants are absorbed."
            : $"Exploration ongoing. No variant has yet crossed {experiment.SuperiorityThreshold * 100:F0}% superiority with >= {visitorThreshold} visitors. Traffic routed via Thompson Sampling.";

        return new SimulateSuperiorityResponse(
            funnelId,
            simIterations,
            baseline.VariantId,
            simulationReports,
            cannibalizationActive,
            winningVariantId,
            decisionSummary,
            DateTime.UtcNow
        );
    }

    public void ResetExperiment(string funnelId)
    {
        if (!_experiments.TryGetValue(funnelId, out var experiment))
        {
            throw new KeyNotFoundException($"Funnel experiment '{funnelId}' not found.");
        }

        foreach (var variant in experiment.Variants)
        {
            variant.ResetAtomic();
        }

        experiment.AutoCannibalizationActive = false;
        experiment.WinningVariantId = null;
        experiment.LastActivityAt = DateTime.UtcNow;
    }

    private static decimal CalculateSampledRpv(double[] probabilities, FunnelOutcomeValueConfig values)
    {
        decimal rpv = 0m;
        for (int i = 0; i < probabilities.Length; i++)
        {
            var outcome = (FunnelStepOutcome)i;
            rpv += (decimal)probabilities[i] * values.GetValue(outcome);
        }
        return rpv;
    }

    public static decimal CalculatePosteriorMeanRpv(double[] alphas, FunnelOutcomeValueConfig values)
    {
        double sum = alphas.Sum();
        if (sum <= 0) return 0m;

        decimal rpv = 0m;
        for (int i = 0; i < alphas.Length; i++)
        {
            double meanP = alphas[i] / sum;
            var outcome = (FunnelStepOutcome)i;
            rpv += (decimal)meanP * values.GetValue(outcome);
        }
        return Math.Round(rpv, 4);
    }

    /// <summary>
    /// Seeds 3 realistic multi-variant funnels with Dirichlet-Multinomial observations.
    /// </summary>
    private void SeedRealisticExperiments()
    {
        // 1. SaaS Enterprise Free-Trial to Annual Expansion
        var exp1 = new FunnelExperiment
        {
            FunnelId = "funnel-saas-enterprise",
            FunnelName = "SaaS Enterprise Free-Trial to Annual Expansion",
            Description = "Optimizes the enterprise buyer journey from landing page to annual contract commit.",
            Currency = "USD",
            ExplorationFloor = 0.05,
            MinVisitorThreshold = 100,
            SuperiorityThreshold = 0.95,
            OutcomeValues = new FunnelOutcomeValueConfig(
                Bounce: 0.00m,
                LeadCaptured: 5.00m,
                CheckoutInitiated: 49.00m,
                OrderPaid: 290.00m,
                UpsellAccepted: 990.00m
            ),
            Variants = new List<FunnelVariant>
            {
                new()
                {
                    VariantId = "A",
                    VariantName = "Interactive Demo Tour",
                    Description = "Self-guided product walkthrough with inline feature highlights.",
                    IsBaseline = true,
                    // [Bounce, Lead, Checkout, Paid, Upsell]
                    Alphas = new double[] { 340, 110, 45, 22, 6 },
                    InitialAlphas = new double[] { 340, 110, 45, 22, 6 },
                    TotalVisitors = 523,
                    TotalRevenue = (110 * 5m) + (45 * 49m) + (22 * 290m) + (6 * 990m)
                },
                new()
                {
                    VariantId = "B",
                    VariantName = "Executive Whitepaper & ROI Calculator",
                    Description = "Financial ROI assessment tool with downloadable enterprise whitepaper.",
                    IsBaseline = false,
                    Alphas = new double[] { 310, 140, 58, 31, 12 },
                    InitialAlphas = new double[] { 310, 140, 58, 31, 12 },
                    TotalVisitors = 551,
                    TotalRevenue = (140 * 5m) + (58 * 49m) + (31 * 290m) + (12 * 990m)
                },
                new()
                {
                    VariantId = "C",
                    VariantName = "Live Sandboxed Playground",
                    Description = "Instant zero-login sandbox environment showcasing live CRM data sync.",
                    IsBaseline = false,
                    Alphas = new double[] { 260, 175, 80, 48, 21 },
                    InitialAlphas = new double[] { 260, 175, 80, 48, 21 },
                    TotalVisitors = 584,
                    TotalRevenue = (175 * 5m) + (80 * 49m) + (48 * 290m) + (21 * 990m)
                }
            }
        };

        // 2. High-Ticket B2B Strategy Session
        var exp2 = new FunnelExperiment
        {
            FunnelId = "funnel-highticket-b2b",
            FunnelName = "High-Ticket B2B Strategy Session",
            Description = "Direct pipeline funnel routing high-intent enterprise executives to advisory booking.",
            Currency = "USD",
            ExplorationFloor = 0.05,
            MinVisitorThreshold = 100,
            SuperiorityThreshold = 0.95,
            OutcomeValues = new FunnelOutcomeValueConfig(
                Bounce: 0.00m,
                LeadCaptured: 15.00m,
                CheckoutInitiated: 150.00m,
                OrderPaid: 1500.00m,
                UpsellAccepted: 4500.00m
            ),
            Variants = new List<FunnelVariant>
            {
                new()
                {
                    VariantId = "A",
                    VariantName = "Video Sales Letter (VSL)",
                    Description = "18-minute comprehensive founder breakdown of enterprise methodology.",
                    IsBaseline = true,
                    Alphas = new double[] { 420, 85, 24, 12, 4 },
                    InitialAlphas = new double[] { 420, 85, 24, 12, 4 },
                    TotalVisitors = 545,
                    TotalRevenue = (85 * 15m) + (24 * 150m) + (12 * 1500m) + (4 * 4500m)
                },
                new()
                {
                    VariantId = "B",
                    VariantName = "Founder Direct Booking Page",
                    Description = "Concise calendar availability with verified partner testimonials.",
                    IsBaseline = false,
                    Alphas = new double[] { 380, 105, 32, 16, 6 },
                    InitialAlphas = new double[] { 380, 105, 32, 16, 6 },
                    TotalVisitors = 539,
                    TotalRevenue = (105 * 15m) + (32 * 150m) + (16 * 1500m) + (6 * 4500m)
                },
                new()
                {
                    VariantId = "C",
                    VariantName = "Interactive Diagnostic Assessment",
                    Description = "7-question diagnostic scorecard estimating pipeline leakage.",
                    IsBaseline = false,
                    Alphas = new double[] { 330, 135, 44, 24, 9 },
                    InitialAlphas = new double[] { 330, 135, 44, 24, 9 },
                    TotalVisitors = 542,
                    TotalRevenue = (135 * 15m) + (44 * 150m) + (24 * 1500m) + (9 * 4500m)
                }
            }
        };

        // 3. E-Commerce Growth Accelerator
        var exp3 = new FunnelExperiment
        {
            FunnelId = "funnel-ecommerce-growth",
            FunnelName = "E-Commerce Growth Accelerator",
            Description = "Direct-to-consumer checkout optimization with 1-click order bumps and bundle upsells.",
            Currency = "USD",
            ExplorationFloor = 0.05,
            MinVisitorThreshold = 100,
            SuperiorityThreshold = 0.95,
            OutcomeValues = new FunnelOutcomeValueConfig(
                Bounce: 0.00m,
                LeadCaptured: 1.50m,
                CheckoutInitiated: 12.00m,
                OrderPaid: 69.00m,
                UpsellAccepted: 149.00m
            ),
            Variants = new List<FunnelVariant>
            {
                new()
                {
                    VariantId = "A",
                    VariantName = "Flash Sale Hero Banner",
                    Description = "High-urgency countdown timer with direct collection grid.",
                    IsBaseline = true,
                    Alphas = new double[] { 750, 180, 95, 42, 14 },
                    InitialAlphas = new double[] { 750, 180, 95, 42, 14 },
                    TotalVisitors = 1081,
                    TotalRevenue = (180 * 1.5m) + (95 * 12m) + (42 * 69m) + (14 * 149m)
                },
                new()
                {
                    VariantId = "B",
                    VariantName = "Personalized Quiz Recommender",
                    Description = "3-step quiz recommending tailored SKU bundles with instant checkout.",
                    IsBaseline = false,
                    Alphas = new double[] { 580, 290, 160, 78, 33 },
                    InitialAlphas = new double[] { 580, 290, 160, 78, 33 },
                    TotalVisitors = 1141,
                    TotalRevenue = (290 * 1.5m) + (160 * 12m) + (78 * 69m) + (33 * 149m)
                },
                new()
                {
                    VariantId = "C",
                    VariantName = "Social Proof Wall + 1-Click Order Bump",
                    Description = "UGC video reviews banner with embedded 1-click express upsell checkout.",
                    IsBaseline = false,
                    Alphas = new double[] { 620, 240, 140, 65, 26 },
                    InitialAlphas = new double[] { 620, 240, 140, 65, 26 },
                    TotalVisitors = 1091,
                    TotalRevenue = (240 * 1.5m) + (140 * 12m) + (65 * 69m) + (26 * 149m)
                }
            }
        };

        _experiments[exp1.FunnelId] = exp1;
        _experiments[exp2.FunnelId] = exp2;
        _experiments[exp3.FunnelId] = exp3;

        // Precompute initial superiority simulations so metrics are live immediately
        RunSuperioritySimulation(exp1.FunnelId, 1000);
        RunSuperioritySimulation(exp2.FunnelId, 1000);
        RunSuperioritySimulation(exp3.FunnelId, 1000);
    }
}
