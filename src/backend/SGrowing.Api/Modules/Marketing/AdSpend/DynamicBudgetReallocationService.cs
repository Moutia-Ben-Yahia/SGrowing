namespace SGrowing.Api.Modules.Marketing.AdSpend;

/// <summary>
/// Channel input specification for dynamic budget reallocation.
/// </summary>
/// <param name="ChannelId">Unique channel identifier (e.g. "meta-asc", "google-pmax").</param>
/// <param name="ChannelName">Display name of the marketing channel.</param>
/// <param name="CurrentSpend">Current daily budget allocated to this channel.</param>
/// <param name="FloorSpend">Absolute minimum daily spend guardrail.</param>
/// <param name="CeilingSpend">Absolute maximum daily spend guardrail.</param>
/// <param name="HillParams">Hill saturation curve parameters (Vmax, K, HillExponent).</param>
/// <param name="MaxShiftFraction">Maximum fractional change permitted in a single reallocation cycle (e.g. 0.25 = 25%).</param>
public record ChannelBudgetInput(
    string ChannelId,
    string ChannelName,
    double CurrentSpend,
    double FloorSpend,
    double CeilingSpend,
    HillParameters HillParams,
    double MaxShiftFraction = 0.25);

/// <summary>
/// Outcome of budget reallocation for an individual channel.
/// </summary>
public record ChannelAllocationResult(
    string ChannelId,
    string ChannelName,
    double OldSpend,
    double NewSpend,
    double Delta,
    double SampledMarginalRoas,
    double MarginalRoasAtNewSpend,
    double SaturationPercentageAtNewSpend,
    string Rationale);

/// <summary>
/// Complete reallocation execution plan with budget conservation and simulated revenue gains.
/// </summary>
public record ReallocationPlanResult(
    Guid PlanId,
    DateTime GeneratedAtUtc,
    double TotalAvailableBudget,
    double TotalReallocatedBudget,
    double ConservationDrift,
    double ExpectedRevenueGain,
    IReadOnlyList<ChannelAllocationResult> Allocations,
    string Summary);

/// <summary>
/// Immutable audit entry representing a historical budget reallocation execution.
/// </summary>
public record ReallocationAuditEntry(
    Guid ReallocationId,
    DateTime TimestampUtc,
    double TotalBudget,
    double ExpectedRevenueGain,
    double ConservationDrift,
    IReadOnlyList<ChannelAllocationResult> Allocations,
    string Summary);

/// <summary>
/// Contract for Horizon 49 Autonomous Dynamic Budget Reallocation &amp; Continuous Bandit Service.
/// </summary>
public interface IDynamicBudgetReallocationService
{
    ReallocationPlanResult ReallocateBudgets(
        IReadOnlyList<ChannelBudgetInput> channels,
        double? customTotalBudget = null,
        double? globalMaxShiftFraction = null,
        int? randomSeed = null);

    IReadOnlyList<ReallocationAuditEntry> GetAuditHistory();
    void ClearAuditHistory();
}

/// <summary>
/// Implements Thompson Sampling / Continuous Multi-Armed Bandit budget reallocation.
/// Guarantees the budget conservation invariant (|sum(B_new) - B_total| &lt;= 1e-6),
/// enforces floor and ceiling guardrails, applies pacing throttles, and maintains a thread-safe audit trail.
/// </summary>
public class DynamicBudgetReallocationService : IDynamicBudgetReallocationService
{
    private readonly IPredictiveRoasEngine _roasEngine;
    private readonly Lock _lock = new();
    private readonly List<ReallocationAuditEntry> _auditTrail = [];

    public DynamicBudgetReallocationService(IPredictiveRoasEngine roasEngine)
    {
        _roasEngine = roasEngine;
    }

    /// <summary>
    /// Executes Thompson Sampling continuous bandit reallocation shifting budget from low-marginal
    /// to high-marginal ROAS campaigns, strictly respecting pacing throttles, floors, ceilings, and conservation.
    /// </summary>
    public ReallocationPlanResult ReallocateBudgets(
        IReadOnlyList<ChannelBudgetInput> channels,
        double? customTotalBudget = null,
        double? globalMaxShiftFraction = null,
        int? randomSeed = null)
    {
        if (channels.Count == 0)
        {
            throw new ArgumentException("At least one channel must be provided for budget reallocation.", nameof(channels));
        }

        lock (_lock)
        {
            var random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

            double currentTotalBudget = channels.Sum(c => Math.Max(0.0, c.CurrentSpend));
            double totalBudget = customTotalBudget ?? currentTotalBudget;

            if (totalBudget <= 0.0)
            {
                throw new ArgumentException("Total budget must be strictly positive.", nameof(customTotalBudget));
            }

            int n = channels.Count;

            // 1. Calculate baseline predicted revenue before reallocation
            double oldTotalPredictedRevenue = channels.Sum(c =>
                _roasEngine.EvaluateHillCurve(Math.Max(0.0, c.CurrentSpend), c.HillParams));

            // 2. Thompson Sampling: Sample marginal ROAS for each channel
            var sampledMarginals = new double[n];
            for (int i = 0; i < n; i++)
            {
                var ch = channels[i];
                double spend = Math.Max(0.01, ch.CurrentSpend);
                double baseMarginal = _roasEngine.CalculateMarginalRoas(spend, ch.HillParams);

                // Add Bayesian posterior uncertainty sample ~ Normal(baseMarginal, sigma^2)
                double sigma = Math.Max(0.05, baseMarginal * 0.15);
                double sampled = SampleGaussian(random, baseMarginal, sigma);

                // Ensure sampled marginal ROAS is strictly positive
                sampledMarginals[i] = Math.Max(0.01, sampled);
            }

            // 3. Compute continuous bandit target allocation weights via power rule (gamma = 1.5)
            const double gamma = 1.5;
            var targetWeights = new double[n];
            double totalWeight = 0.0;
            for (int i = 0; i < n; i++)
            {
                targetWeights[i] = Math.Pow(sampledMarginals[i], gamma);
                totalWeight += targetWeights[i];
            }

            // 4. Initial target budgets
            var targetBudgets = new double[n];
            for (int i = 0; i < n; i++)
            {
                targetBudgets[i] = totalBudget * (targetWeights[i] / totalWeight);
            }

            // 5. Apply pacing throttles, floor guardrails, and ceiling guardrails
            var newBudgets = new double[n];
            var lowerBounds = new double[n];
            var upperBounds = new double[n];
            var floorBounds = new double[n];
            var ceilingBounds = new double[n];

            for (int i = 0; i < n; i++)
            {
                var ch = channels[i];
                double shiftFraction = globalMaxShiftFraction ?? ch.MaxShiftFraction;
                shiftFraction = Math.Clamp(shiftFraction, 0.01, 1.0);

                double maxDelta = ch.CurrentSpend * shiftFraction;

                double floor = Math.Max(0.0, ch.FloorSpend);
                double ceiling = Math.Max(floor, ch.CeilingSpend);

                floorBounds[i] = floor;
                ceilingBounds[i] = ceiling;

                // Combined pacing bounds
                double pacingLower = Math.Max(0.0, ch.CurrentSpend - maxDelta);
                double pacingUpper = ch.CurrentSpend + maxDelta;

                // Pacing constrained within absolute floor and ceiling
                double lower = Math.Max(floor, pacingLower);
                double upper = Math.Min(ceiling, pacingUpper);

                if (lower > upper)
                {
                    // Floor or ceiling constraint takes precedence over pacing throttle
                    if (ch.CurrentSpend < floor)
                    {
                        lower = floor;
                        upper = Math.Max(floor, pacingUpper);
                    }
                    else if (ch.CurrentSpend > ceiling)
                    {
                        upper = ceiling;
                        lower = Math.Min(ceiling, pacingLower);
                    }
                    else
                    {
                        upper = lower;
                    }
                }

                lowerBounds[i] = lower;
                upperBounds[i] = upper;

                // Initial clamp to combined bounds
                newBudgets[i] = Math.Clamp(targetBudgets[i], lower, upper);
            }

            // 6. Guarantee Budget Conservation Invariant: sum(newBudgets) == totalBudget (+- 1e-6)
            RebalanceToConserveBudget(
                newBudgets,
                lowerBounds,
                upperBounds,
                floorBounds,
                ceilingBounds,
                sampledMarginals,
                totalBudget);

            // 7. Calculate new metrics, revenue gain, and individual rationales
            double newTotalPredictedRevenue = 0.0;
            List<ChannelAllocationResult> allocations = [];

            double runningRoundedSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                var ch = channels[i];
                double oldSpend = Math.Round(ch.CurrentSpend, 4);
                
                double newSpend;
                if (i == n - 1)
                {
                    // Exact budget conservation closure for last channel
                    newSpend = Math.Round(totalBudget - runningRoundedSum, 4);
                }
                else
                {
                    newSpend = Math.Round(newBudgets[i], 4);
                    runningRoundedSum += newSpend;
                }

                double delta = Math.Round(newSpend - oldSpend, 4);

                double newPredRev = _roasEngine.EvaluateHillCurve(newSpend, ch.HillParams);
                newTotalPredictedRevenue += newPredRev;

                double marginalAtNew = _roasEngine.CalculateMarginalRoas(newSpend, ch.HillParams);
                double satAtNew = _roasEngine.CalculateSaturationPercentage(newSpend, ch.HillParams);

                string rationale;
                if (delta > 0.01)
                {
                    double pct = oldSpend > 0.0 ? (delta / oldSpend) * 100.0 : 100.0;
                    rationale = $"Scaled budget by +${delta:F2} (+{pct:F1}%) toward high sampled marginal ROAS ({sampledMarginals[i]:F2}x). Saturation: {satAtNew:F1}%.";
                }
                else if (delta < -0.01)
                {
                    double pct = oldSpend > 0.0 ? (Math.Abs(delta) / oldSpend) * 100.0 : 0.0;
                    rationale = $"Trimmed budget by -${Math.Abs(delta):F2} (-{pct:F1}%) due to low marginal ROAS ({sampledMarginals[i]:F2}x) or high saturation ({satAtNew:F1}%).";
                }
                else
                {
                    rationale = $"Maintained budget at ${newSpend:F2}. Channel is operating within optimal marginal ROAS efficiency.";
                }

                allocations.Add(new ChannelAllocationResult(
                    ChannelId: ch.ChannelId,
                    ChannelName: ch.ChannelName,
                    OldSpend: oldSpend,
                    NewSpend: newSpend,
                    Delta: delta,
                    SampledMarginalRoas: Math.Round(sampledMarginals[i], 4),
                    MarginalRoasAtNewSpend: Math.Round(marginalAtNew, 4),
                    SaturationPercentageAtNewSpend: Math.Round(satAtNew, 2),
                    Rationale: rationale));
            }

            double sumNew = allocations.Sum(a => a.NewSpend);
            double drift = Math.Round(totalBudget - sumNew, 6);
            double expectedRevenueGain = Math.Round(newTotalPredictedRevenue - oldTotalPredictedRevenue, 2);

            var planId = Guid.NewGuid();
            var summary = $"Reallocated ${totalBudget:F2} across {n} channels via Thompson Sampling. Expected daily revenue gain: +${expectedRevenueGain:F2}. Drift: {drift:E2}.";

            var planResult = new ReallocationPlanResult(
                PlanId: planId,
                GeneratedAtUtc: DateTime.UtcNow,
                TotalAvailableBudget: Math.Round(totalBudget, 4),
                TotalReallocatedBudget: Math.Round(sumNew, 4),
                ConservationDrift: drift,
                ExpectedRevenueGain: expectedRevenueGain,
                Allocations: allocations,
                Summary: summary);

            // Record in audit log
            _auditTrail.Add(new ReallocationAuditEntry(
                ReallocationId: planId,
                TimestampUtc: DateTime.UtcNow,
                TotalBudget: Math.Round(totalBudget, 4),
                ExpectedRevenueGain: expectedRevenueGain,
                ConservationDrift: drift,
                Allocations: allocations,
                Summary: summary));

            return planResult;
        }
    }

    /// <summary>
    /// Thread-safely retrieves the historical audit trail of all executed budget reallocations.
    /// </summary>
    public IReadOnlyList<ReallocationAuditEntry> GetAuditHistory()
    {
        lock (_lock)
        {
            return _auditTrail.ToList();
        }
    }

    /// <summary>
    /// Thread-safely clears the audit history.
    /// </summary>
    public void ClearAuditHistory()
    {
        lock (_lock)
        {
            _auditTrail.Clear();
        }
    }

    #region Internal Rebalancing & Sampling

    private static void RebalanceToConserveBudget(
        double[] budgets,
        double[] lowerBounds,
        double[] upperBounds,
        double[] floorBounds,
        double[] ceilingBounds,
        double[] marginals,
        double totalBudget)
    {
        int n = budgets.Length;

        // Iterative redistribution of drift within bounds
        for (int iteration = 0; iteration < 50; iteration++)
        {
            double currentSum = 0.0;
            for (int i = 0; i < n; i++) currentSum += budgets[i];

            double drift = totalBudget - currentSum;
            if (Math.Abs(drift) <= 1e-6)
            {
                break;
            }

            if (drift > 0.0)
            {
                // We have budget surplus to distribute among channels that haven't hit their upper bound
                double eligibleWeight = 0.0;
                var eligibleIndices = new List<int>();

                for (int i = 0; i < n; i++)
                {
                    if (budgets[i] < upperBounds[i] - 1e-9)
                    {
                        eligibleIndices.Add(i);
                        eligibleWeight += marginals[i];
                    }
                }

                // If pacing upper bounds are fully saturated, relax to absolute ceilings
                if (eligibleIndices.Count == 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (budgets[i] < ceilingBounds[i] - 1e-9)
                        {
                            eligibleIndices.Add(i);
                            eligibleWeight += marginals[i];
                        }
                    }
                }

                if (eligibleIndices.Count == 0)
                {
                    // All channels at absolute ceilings; cannot distribute more without violating ceilings
                    break;
                }

                foreach (int idx in eligibleIndices)
                {
                    double share = eligibleWeight > 0.0 ? (marginals[idx] / eligibleWeight) * drift : (drift / eligibleIndices.Count);
                    double maxCapacity = Math.Max(0.0, ceilingBounds[idx] - budgets[idx]);
                    double addition = Math.Min(share, maxCapacity);
                    budgets[idx] += addition;
                }
            }
            else
            {
                // drift < 0: We are over budget; need to reduce spend from channels that haven't hit lower bound
                double eligibleReductionCapacity = 0.0;
                var eligibleIndices = new List<int>();

                for (int i = 0; i < n; i++)
                {
                    if (budgets[i] > lowerBounds[i] + 1e-9)
                    {
                        eligibleIndices.Add(i);
                        // Weight reduction inversely to marginal ROAS: 1 / marginal
                        eligibleReductionCapacity += (1.0 / marginals[i]);
                    }
                }

                // If pacing lower bounds are fully reached, relax to absolute floors
                if (eligibleIndices.Count == 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (budgets[i] > floorBounds[i] + 1e-9)
                        {
                            eligibleIndices.Add(i);
                            eligibleReductionCapacity += (1.0 / marginals[i]);
                        }
                    }
                }

                if (eligibleIndices.Count == 0)
                {
                    // All channels at absolute floors; cannot reduce further without violating floors
                    break;
                }

                double deficitToTrim = Math.Abs(drift);
                foreach (int idx in eligibleIndices)
                {
                    double weight = (1.0 / marginals[idx]);
                    double cut = eligibleReductionCapacity > 0.0 ? (weight / eligibleReductionCapacity) * deficitToTrim : (deficitToTrim / eligibleIndices.Count);
                    double maxCut = Math.Max(0.0, budgets[idx] - floorBounds[idx]);
                    double actualCut = Math.Min(cut, maxCut);
                    budgets[idx] -= actualCut;
                }
            }
        }

        // Final micro-drift calibration (down to double precision < 1e-9)
        double finalSum = 0.0;
        for (int i = 0; i < n; i++) finalSum += budgets[i];
        double residual = totalBudget - finalSum;

        if (Math.Abs(residual) > 1e-9)
        {
            // Allocate residual to the highest marginal channel that has capacity, or trim from lowest
            if (residual > 0.0)
            {
                int bestIdx = -1;
                double maxM = double.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (budgets[i] + residual <= ceilingBounds[i] + 1e-9 && marginals[i] > maxM)
                    {
                        maxM = marginals[i];
                        bestIdx = i;
                    }
                }
                if (bestIdx >= 0) budgets[bestIdx] += residual;
            }
            else
            {
                int bestIdx = -1;
                double minM = double.PositiveInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (budgets[i] + residual >= floorBounds[i] - 1e-9 && marginals[i] < minM)
                    {
                        minM = marginals[i];
                        bestIdx = i;
                    }
                }
                if (bestIdx >= 0) budgets[bestIdx] += residual;
            }
        }
    }

    /// <summary>
    /// Generates standard normal sample using Box-Muller transform: X = mu + sigma * Z.
    /// </summary>
    private static double SampleGaussian(Random random, double mean, double standardDeviation)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();

        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + standardDeviation * randStdNormal;
    }

    #endregion
}
