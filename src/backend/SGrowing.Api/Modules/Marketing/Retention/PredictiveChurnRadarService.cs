using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Marketing.Retention;

/// <summary>
/// Core contract for predictive customer churn radar utilizing Cox Proportional Hazards modeling.
/// </summary>
public interface IPredictiveChurnRadarService
{
    /// <summary>
    /// Evaluates survival hazard and computes multidimensional churn risk assessment for a customer.
    /// </summary>
    ChurnRiskAssessment AssessCustomerRisk(CustomerBehaviorSignals signals);

    /// <summary>
    /// Performs a high-throughput batch scan of customer signals to detect at-risk cohorts.
    /// </summary>
    Task<BatchRadarScanResponse> ScanBatchAsync(
        BatchRadarScanRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Cox proportional hazard rate h(t|X) = h0 * exp(beta1*Cadence + beta2*(1-Sentiment) + beta3*(1-OpenRate)).
    /// </summary>
    double CalculateHazardRate(double cadenceDecay, double sentimentScore, double openRate);

    /// <summary>
    /// Maps continuous hazard rate into normalized percentage churn risk score [0.0, 100.0].
    /// </summary>
    double CalculateRiskScore(double hazardRate);

    /// <summary>
    /// Categorizes churn risk score into Low, Moderate, High, or Critical risk tiers.
    /// </summary>
    ChurnRiskTier CategorizeRiskTier(double riskScore);

    /// <summary>
    /// Projects remaining expected days until definitive churn based on order cadence decay and hazard acceleration.
    /// </summary>
    int CalculateExpectedDaysToChurn(int daysSinceLastOrder, int historicalCadenceDays, double hazardRate);

    /// <summary>
    /// Identifies the leading behavioral contributing factors driving the customer's churn hazard.
    /// </summary>
    IReadOnlyList<string> IdentifyPrimaryRiskFactors(CustomerBehaviorSignals signals, double hazardRate);
}

/// <summary>
/// High-performance implementation of Cox Proportional Hazards survival analysis for retention radar.
/// </summary>
public class PredictiveChurnRadarService : IPredictiveChurnRadarService
{
    private const double BaselineHazardH0 = 0.05;
    private const double BetaCadenceDecay = 1.8;
    private const double BetaSentiment = 1.4;
    private const double BetaOpenRate = 1.2;

    private readonly ILogger<PredictiveChurnRadarService>? _logger;

    public PredictiveChurnRadarService(ILogger<PredictiveChurnRadarService>? logger = null)
    {
        _logger = logger;
    }

    public ChurnRiskAssessment AssessCustomerRisk(CustomerBehaviorSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        // Calculate cadence decay ratio if not directly provided
        double cadenceDecay = signals.CadenceDecayRatio > 0.0
            ? signals.CadenceDecayRatio
            : (signals.HistoricalOrderCadenceDays > 0
                ? (double)signals.DaysSinceLastOrder / signals.HistoricalOrderCadenceDays
                : 1.0);

        double hazardRate = CalculateHazardRate(
            cadenceDecay,
            signals.SupportTicketSentimentScore,
            signals.WhatsAppMessageOpenRate);

        double riskScore = CalculateRiskScore(hazardRate);
        ChurnRiskTier tier = CategorizeRiskTier(riskScore);
        int expectedDays = CalculateExpectedDaysToChurn(
            signals.DaysSinceLastOrder,
            signals.HistoricalOrderCadenceDays,
            hazardRate);

        IReadOnlyList<string> riskFactors = IdentifyPrimaryRiskFactors(signals, hazardRate);

        var assessment = new ChurnRiskAssessment(
            CustomerId: signals.CustomerId,
            ChurnRiskScore: riskScore,
            RiskTier: tier,
            HazardRate: Math.Round(hazardRate, 4),
            ExpectedDaysToChurn: expectedDays,
            PrimaryRiskFactors: riskFactors,
            AssessedAtUtc: DateTime.UtcNow);

        _logger?.LogDebug(
            "Customer {CustomerId} assessed: Score={Score} ({Tier}), Hazard={Hazard:F4}, ExpectedDays={ExpectedDays}",
            signals.CustomerId, riskScore, tier, hazardRate, expectedDays);

        return assessment;
    }

    public Task<BatchRadarScanResponse> ScanBatchAsync(
        BatchRadarScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Customers.Count == 0)
        {
            return Task.FromResult(new BatchRadarScanResponse(
                Success: true,
                TotalScanned: 0,
                AtRiskCount: 0,
                AtRiskCustomers: [],
                Message: "Empty customer cohort provided."));
        }

        double threshold = request.RiskThreshold ?? 30.0;
        List<ChurnRiskAssessment> atRisk = [];

        foreach (var signals in request.Customers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assessment = AssessCustomerRisk(signals);
            if (assessment.ChurnRiskScore >= threshold)
            {
                atRisk.Add(assessment);
            }
        }

        // Rank by highest churn risk score first
        var ranked = atRisk
            .OrderByDescending(a => a.ChurnRiskScore)
            .ThenBy(a => a.ExpectedDaysToChurn)
            .ToList();

        var response = new BatchRadarScanResponse(
            Success: true,
            TotalScanned: request.Customers.Count,
            AtRiskCount: ranked.Count,
            AtRiskCustomers: ranked,
            Message: $"Radar scan completed. Identified {ranked.Count} at-risk customer(s) at or above {threshold:F1}% risk threshold.");

        return Task.FromResult(response);
    }

    public double CalculateHazardRate(double cadenceDecay, double sentimentScore, double openRate)
    {
        double clampedCadence = Math.Clamp(cadenceDecay, 0.0, 10.0);
        double clampedSentiment = Math.Clamp(sentimentScore, -1.0, 1.0);
        double clampedOpenRate = Math.Clamp(openRate, 0.0, 1.0);

        // Deficit metrics: high sentiment -> 0 deficit; high open rate -> 0 deficit
        double sentimentDeficit = 1.0 - clampedSentiment;
        double openRateDeficit = 1.0 - clampedOpenRate;

        // Cox proportional hazard linear predictor
        double linearPredictor = (BetaCadenceDecay * clampedCadence)
            + (BetaSentiment * sentimentDeficit)
            + (BetaOpenRate * openRateDeficit);

        // Guard against double overflow with exponential scaling
        double safePredictor = Math.Min(linearPredictor, 20.0);
        return BaselineHazardH0 * Math.Exp(safePredictor);
    }

    public double CalculateRiskScore(double hazardRate)
    {
        if (hazardRate <= 0.0)
        {
            return 0.0;
        }

        // Standard cumulative failure incidence F(t) = 1 - exp(-h(t)) mapped to [0, 100]
        double score = 100.0 * (1.0 - Math.Exp(-hazardRate));
        return Math.Round(Math.Clamp(score, 0.0, 100.0), 2);
    }

    public ChurnRiskTier CategorizeRiskTier(double riskScore)
    {
        return riskScore switch
        {
            < 30.0 => ChurnRiskTier.Low,
            < 60.0 => ChurnRiskTier.Moderate,
            < 85.0 => ChurnRiskTier.High,
            _ => ChurnRiskTier.Critical
        };
    }

    public int CalculateExpectedDaysToChurn(int daysSinceLastOrder, int historicalCadenceDays, double hazardRate)
    {
        int cadence = Math.Max(1, historicalCadenceDays > 0 ? historicalCadenceDays : 30);
        int daysSince = Math.Max(0, daysSinceLastOrder);

        // Typical churn threshold window: 2.5x customer's historical order frequency
        double churnHorizonDays = cadence * 2.5;
        double remainingWindow = Math.Max(0.0, churnHorizonDays - daysSince);

        // Hazard rate accelerates timeline towards definitive churn
        double accelerationFactor = Math.Max(0.5, 1.0 + (hazardRate * 0.25));
        int projectedDays = (int)Math.Round(remainingWindow / accelerationFactor);

        // Bounded minimum of 1 day to allow emergency win-back intervention
        return Math.Max(1, projectedDays);
    }

    public IReadOnlyList<string> IdentifyPrimaryRiskFactors(CustomerBehaviorSignals signals, double hazardRate)
    {
        List<string> factors = [];

        double cadenceRatio = signals.CadenceDecayRatio > 0.0
            ? signals.CadenceDecayRatio
            : (signals.HistoricalOrderCadenceDays > 0
                ? (double)signals.DaysSinceLastOrder / signals.HistoricalOrderCadenceDays
                : 1.0);

        if (cadenceRatio >= 1.5)
        {
            factors.Add($"Order cadence delayed ({cadenceRatio:F1}x historical cycle)");
        }
        else if (cadenceRatio >= 1.1)
        {
            factors.Add($"Order cadence beginning to slip ({cadenceRatio:F1}x cadence)");
        }

        if (signals.SupportTicketSentimentScore < -0.2)
        {
            factors.Add($"Negative support ticket sentiment ({signals.SupportTicketSentimentScore:F2})");
        }

        if (signals.UnresolvedTicketCount > 0)
        {
            factors.Add($"Unresolved customer support tickets ({signals.UnresolvedTicketCount} open)");
        }

        if (signals.WhatsAppMessageOpenRate < 0.35)
        {
            factors.Add($"WhatsApp channel disengagement ({signals.WhatsAppMessageOpenRate:P0} open rate)");
        }

        if (signals.EmailClickRate < 0.10)
        {
            factors.Add($"Low email click-through rate ({signals.EmailClickRate:P0})");
        }

        if (signals.BrowsingDropOffRatio >= 0.70)
        {
            factors.Add($"Severe browse/cart drop-off without purchase ({signals.BrowsingDropOffRatio:P0})");
        }

        if (hazardRate > 2.0)
        {
            factors.Add($"Accelerated survival hazard rate ({hazardRate:F2})");
        }

        if (factors.Count == 0)
        {
            factors.Add("Normal customer engagement profile");
        }

        return factors;
    }
}
