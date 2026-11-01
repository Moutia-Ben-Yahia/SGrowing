using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Marketing.Retention;

/// <summary>
/// Multi-armed bandit contract utilizing contextual Thompson Sampling over Beta priors.
/// </summary>
public interface IRetentionPolicyBanditService
{
    /// <summary>
    /// Evaluates contextual bandit arms and selects optimal incentive strategy for a customer.
    /// </summary>
    BanditRecommendation RecommendIncentive(
        CustomerBehaviorSignals signals,
        ChurnRiskAssessment? assessment = null,
        Random? random = null);

    /// <summary>
    /// Records an outcome feedback event, updating the posterior Beta distribution (alpha, beta).
    /// </summary>
    BanditArmStats RecordFeedback(IncentiveType arm, bool converted, decimal revenueSavedDzd);

    /// <summary>
    /// Asynchronously records conversion feedback via API contract.
    /// </summary>
    Task<RecordBanditFeedbackResponse> RecordFeedbackAsync(
        RecordBanditFeedbackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current performance statistics for all multi-armed bandit incentive options.
    /// </summary>
    IReadOnlyList<BanditArmStats> GetAllArmStats();

    /// <summary>
    /// Formulates high-level bandit health and leading arm summary report.
    /// </summary>
    BanditStatsResponse GetBanditStats();

    /// <summary>
    /// Retrieves static commercial metadata for an incentive type.
    /// </summary>
    RetentionIncentive GetIncentiveDefinition(IncentiveType type);

    /// <summary>
    /// Retrieves all configured retention incentives.
    /// </summary>
    IReadOnlyList<RetentionIncentive> GetAllIncentives();

    /// <summary>
    /// Resets arm priors and performance statistics back to baseline uniform distribution.
    /// </summary>
    void ResetPriors(double initialAlpha = 1.0, double initialBeta = 1.0);

    /// <summary>
    /// Draws a random sample from Beta(alpha, beta) using Gamma variate transformation.
    /// </summary>
    double SampleBeta(double alpha, double beta, Random? random = null);
}

/// <summary>
/// Production-grade Contextual Multi-Armed Bandit engine powered by Thompson Sampling.
/// </summary>
public class RetentionPolicyBanditService : IRetentionPolicyBanditService
{
    private const double BaselineConversionRate = 0.05; // 5% organic baseline conversion without incentive

    private readonly ILogger<RetentionPolicyBanditService>? _logger;
    private readonly ConcurrentDictionary<IncentiveType, ArmInternalState> _arms = new();
    private readonly Dictionary<IncentiveType, RetentionIncentive> _incentiveCatalog = new();

    public RetentionPolicyBanditService(ILogger<RetentionPolicyBanditService>? logger = null)
    {
        _logger = logger;
        InitializeCatalog();
        InitializeArms(1.0, 1.0);
    }

    private void InitializeCatalog()
    {
        _incentiveCatalog[IncentiveType.ExpressShippingVoucher] = new RetentionIncentive(
            IncentiveId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Type: IncentiveType.ExpressShippingVoucher,
            Description: "Free Express Same-Day/Next-Day Shipping Voucher across Algeria",
            DiscountPercentage: 0m,
            CostDzd: 600m,
            ExpiryDays: 5);

        _incentiveCatalog[IncentiveType.BundleDiscount15] = new RetentionIncentive(
            IncentiveId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Type: IncentiveType.BundleDiscount15,
            Description: "Exclusive 15% Win-Back Discount on Curated Re-order Bundle",
            DiscountPercentage: 15m,
            CostDzd: 1200m,
            ExpiryDays: 7);

        _incentiveCatalog[IncentiveType.VipConciergeCall] = new RetentionIncentive(
            IncentiveId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Type: IncentiveType.VipConciergeCall,
            Description: "Personalized VIP Concierge Callback & Dedicated Account Manager",
            DiscountPercentage: 10m,
            CostDzd: 800m,
            ExpiryDays: 3);

        _incentiveCatalog[IncentiveType.FreeGiftWithPurchase] = new RetentionIncentive(
            IncentiveId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Type: IncentiveType.FreeGiftWithPurchase,
            Description: "Complimentary Premium Gift with Any Re-order over 3,000 DZD",
            DiscountPercentage: 0m,
            CostDzd: 1500m,
            ExpiryDays: 10);
    }

    private void InitializeArms(double alpha, double beta)
    {
        _arms.Clear();
        foreach (var type in Enum.GetValues<IncentiveType>())
        {
            _arms[type] = new ArmInternalState(type, alpha, beta);
        }
    }

    public void ResetPriors(double initialAlpha = 1.0, double initialBeta = 1.0)
    {
        InitializeArms(initialAlpha, initialBeta);
    }

    public RetentionIncentive GetIncentiveDefinition(IncentiveType type)
    {
        return _incentiveCatalog[type];
    }

    public IReadOnlyList<RetentionIncentive> GetAllIncentives()
    {
        return _incentiveCatalog.Values.ToList();
    }

    public BanditRecommendation RecommendIncentive(
        CustomerBehaviorSignals signals,
        ChurnRiskAssessment? assessment = null,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var rng = random ?? Random.Shared;

        IncentiveType bestArm = IncentiveType.ExpressShippingVoucher;
        double bestSampledScore = double.NegativeInfinity;
        double chosenArmSampledTheta = 0.0;
        double chosenArmAlpha = 1.0;
        double chosenArmBeta = 1.0;

        // Find empirical greedy arm for exploration metric
        var leadingEmpiricalArm = _arms.Values
            .OrderByDescending(a => a.GetConversionRate())
            .ThenByDescending(a => a.TotalPulls)
            .First().Type;

        foreach (var (armType, armState) in _arms)
        {
            double alpha, beta;
            lock (armState.SyncRoot)
            {
                alpha = armState.Alpha;
                beta = armState.Beta;
            }

            // Draw Thompson sample from posterior Beta(alpha, beta)
            double sampledTheta = SampleBeta(alpha, beta, rng);

            // Compute contextual multiplier based on customer behavior
            double contextualMultiplier = CalculateContextMultiplier(armType, signals, assessment);
            double contextualScore = sampledTheta * contextualMultiplier;

            if (contextualScore > bestSampledScore)
            {
                bestSampledScore = contextualScore;
                bestArm = armType;
                chosenArmSampledTheta = sampledTheta;
                chosenArmAlpha = alpha;
                chosenArmBeta = beta;
            }
        }

        // Quantify posterior uncertainty / exploration probability via Beta standard deviation
        double variance = (chosenArmAlpha * chosenArmBeta) /
            (Math.Pow(chosenArmAlpha + chosenArmBeta, 2) * (chosenArmAlpha + chosenArmBeta + 1.0));
        double stdDev = Math.Sqrt(variance);
        double explorationProbability = Math.Round(Math.Clamp(stdDev * 3.0, 0.02, 0.95), 4);

        // If arm selected is not empirical leader, flag higher exploration
        if (bestArm != leadingEmpiricalArm && _arms[leadingEmpiricalArm].TotalPulls > 0)
        {
            explorationProbability = Math.Max(explorationProbability, 0.50);
        }

        double estimatedLift = Math.Round(Math.Max(0.0, chosenArmSampledTheta - BaselineConversionRate), 4);
        var recommendedChannel = SelectRecommendedChannel(bestArm, signals);

        return new BanditRecommendation(
            CustomerId: signals.CustomerId,
            SelectedIncentive: _incentiveCatalog[bestArm],
            ExplorationProbability: explorationProbability,
            EstimatedConversionLift: estimatedLift,
            RecommendedChannel: recommendedChannel);
    }

    public BanditArmStats RecordFeedback(IncentiveType arm, bool converted, decimal revenueSavedDzd)
    {
        if (!_arms.TryGetValue(arm, out var armState))
        {
            throw new ArgumentOutOfRangeException(nameof(arm), $"Unknown incentive arm: {arm}");
        }

        lock (armState.SyncRoot)
        {
            armState.TotalPulls++;
            armState.CumulativeRevenueLift += revenueSavedDzd;

            if (converted)
            {
                armState.TotalConversions++;
                armState.Alpha += 1.0;
            }
            else
            {
                armState.Beta += 1.0;
            }

            _logger?.LogInformation(
                "Feedback recorded for Arm {Arm}: Converted={Converted}, Revenue={Revenue:F0} DZD, Alpha={Alpha}, Beta={Beta}, Rate={Rate:P2}",
                arm, converted, revenueSavedDzd, armState.Alpha, armState.Beta, armState.GetConversionRate());

            return armState.ToStats();
        }
    }

    public Task<RecordBanditFeedbackResponse> RecordFeedbackAsync(
        RecordBanditFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var updatedStats = RecordFeedback(request.IncentiveType, request.Converted, request.RevenueSavedDzd);
        var response = new RecordBanditFeedbackResponse(
            Success: true,
            UpdatedStats: updatedStats,
            Message: $"Feedback recorded for {request.IncentiveType}. New conversion rate: {updatedStats.ConversionRate:P2}");

        return Task.FromResult(response);
    }

    public IReadOnlyList<BanditArmStats> GetAllArmStats()
    {
        return _arms.Values.Select(a => a.ToStats()).ToList();
    }

    public BanditStatsResponse GetBanditStats()
    {
        var armStats = GetAllArmStats();
        int totalPulls = armStats.Sum(a => a.TotalPulls);
        int totalConversions = armStats.Sum(a => a.TotalConversions);
        decimal totalRevenue = armStats.Sum(a => a.CumulativeRevenueLift);

        double overallRate = totalPulls > 0
            ? Math.Round((double)totalConversions / totalPulls, 4)
            : 0.0;

        var leadingArm = armStats
            .OrderByDescending(a => a.ConversionRate)
            .ThenByDescending(a => a.TotalConversions)
            .First().IncentiveType;

        return new BanditStatsResponse(
            Success: true,
            Arms: armStats.ToList(),
            LeadingArm: leadingArm,
            OverallConversionRate: overallRate,
            TotalRevenueSavedDzd: totalRevenue);
    }

    public double SampleBeta(double alpha, double beta, Random? random = null)
    {
        var rng = random ?? Random.Shared;

        // Sample Beta(alpha, beta) as X / (X + Y) where X ~ Gamma(alpha, 1) and Y ~ Gamma(beta, 1)
        double x = SampleGamma(alpha, rng);
        double y = SampleGamma(beta, rng);

        if (x + y <= 0.0)
        {
            return 0.5;
        }

        double sample = x / (x + y);
        return Math.Clamp(sample, 0.0001, 0.9999);
    }

    /// <summary>
    /// Marsaglia and Tsang (2000) Gamma distribution generator for alpha > 0.
    /// </summary>
    private static double SampleGamma(double shape, Random rng)
    {
        if (shape < 1.0)
        {
            // Boost using standard property: Gamma(shape, 1) = Gamma(shape + 1, 1) * U^(1 / shape)
            double u = NextUniform(rng);
            return SampleGamma(shape + 1.0, rng) * Math.Pow(u, 1.0 / shape);
        }

        double d = shape - (1.0 / 3.0);
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double z = NextStandardNormal(rng);
            double v = 1.0 + (c * z);

            if (v <= 0.0)
            {
                continue;
            }

            v = v * v * v;
            double u = NextUniform(rng);

            // Fast squeeze test
            if (u < 1.0 - (0.0331 * z * z * z * z))
            {
                return d * v;
            }

            if (Math.Log(u) < (0.5 * z * z) + (d * (1.0 - v + Math.Log(v))))
            {
                return d * v;
            }
        }
    }

    private static double NextUniform(Random rng)
    {
        double u = rng.NextDouble();
        return u <= 0.0 ? 1e-10 : (u >= 1.0 ? 1.0 - 1e-10 : u);
    }

    private static double NextStandardNormal(Random rng)
    {
        // Box-Muller transform
        double u1 = NextUniform(rng);
        double u2 = NextUniform(rng);
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static double CalculateContextMultiplier(
        IncentiveType arm,
        CustomerBehaviorSignals signals,
        ChurnRiskAssessment? assessment)
    {
        return arm switch
        {
            IncentiveType.VipConciergeCall =>
                (signals.AvgOrderValue >= 8000m || signals.UnresolvedTicketCount > 0 || signals.SupportTicketSentimentScore < -0.2)
                    ? 1.25 : 0.95,

            IncentiveType.BundleDiscount15 =>
                (signals.BrowsingDropOffRatio >= 0.60 || signals.CadenceDecayRatio >= 1.3)
                    ? 1.20 : 1.00,

            IncentiveType.ExpressShippingVoucher =>
                (signals.DaysSinceLastOrder >= signals.HistoricalOrderCadenceDays && signals.CadenceDecayRatio < 1.6)
                    ? 1.15 : 1.00,

            IncentiveType.FreeGiftWithPurchase =>
                (signals.AvgOrderValue >= 4000m && signals.SupportTicketSentimentScore >= 0.0)
                    ? 1.15 : 1.00,

            _ => 1.00
        };
    }

    private static RetentionChannel SelectRecommendedChannel(
        IncentiveType arm,
        CustomerBehaviorSignals signals)
    {
        if (arm == IncentiveType.VipConciergeCall)
        {
            return RetentionChannel.AutomatedVoiceConcierge;
        }

        if (signals.WhatsAppMessageOpenRate >= 0.35)
        {
            return RetentionChannel.WhatsAppInteractive;
        }

        if (signals.EmailClickRate >= 0.20)
        {
            return RetentionChannel.TargetedEmail;
        }

        // WhatsApp is the primary e-commerce channel in Algeria
        return RetentionChannel.WhatsAppInteractive;
    }

    private class ArmInternalState
    {
        public readonly object SyncRoot = new();
        public IncentiveType Type { get; }
        public double Alpha { get; set; }
        public double Beta { get; set; }
        public int TotalPulls { get; set; }
        public int TotalConversions { get; set; }
        public decimal CumulativeRevenueLift { get; set; }

        public ArmInternalState(IncentiveType type, double alpha, double beta)
        {
            Type = type;
            Alpha = alpha;
            Beta = beta;
        }

        public double GetConversionRate() =>
            TotalPulls > 0 ? (double)TotalConversions / TotalPulls : 0.0;

        public BanditArmStats ToStats()
        {
            lock (SyncRoot)
            {
                return new BanditArmStats(
                    IncentiveType: Type,
                    TotalPulls: TotalPulls,
                    TotalConversions: TotalConversions,
                    ConversionRate: Math.Round(GetConversionRate(), 4),
                    CumulativeRevenueLift: CumulativeRevenueLift);
            }
        }
    }
}
