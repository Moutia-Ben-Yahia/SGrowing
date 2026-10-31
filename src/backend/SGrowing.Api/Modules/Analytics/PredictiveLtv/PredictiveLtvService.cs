using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Analytics.PredictiveLtv;

#region Domain Enums & Models

public enum CustomerPltvTier
{
    ChampionWhale,
    SteadyLoyal,
    HighPotentialNewbie,
    StandardOccasional,
    AtRiskDefecting,
    LostDormant
}

public record CustomerTransactionHistory(
    Guid CustomerId,
    string CustomerName,
    string Email,
    int Frequency, // Number of repeat transactions (x)
    double RecencyDays, // Time between first and most recent transaction (t_x)
    double TotalObservationDays, // Time since first transaction (T)
    decimal HistoricalAverageOrderValue, // Average historical monetary value (m_x)
    decimal TotalLifetimeRevenue
);

public record PltvPredictionResult(
    Guid CustomerId,
    string CustomerName,
    string Email,
    double ProbabilityAlive, // P(Alive) [0.0 - 1.0]
    double ChurnRiskScore, // (1 - P(Alive)) * 100 [0.0 - 100.0]
    double ExpectedTransactions30d,
    double ExpectedTransactions90d,
    double ExpectedTransactions365d,
    decimal ExpectedAverageOrderValue,
    decimal PredictedResidualLtv90d,
    decimal PredictedResidualLtv365d,
    CustomerPltvTier Tier,
    bool RequiresAutomatedWinBackIntervention,
    DateTime CalculatedAtUtc
);

public record PltvBatchCohortSummary(
    Guid TenantId,
    int TotalAnalyzedCustomers,
    int ChampionWhalesCount,
    int SteadyLoyalCount,
    int HighPotentialNewbieCount,
    int AtRiskDefectingCount,
    int LostDormantCount,
    decimal TotalPredictedResidualRevenue365d,
    double AverageRetentionProbability,
    List<PltvPredictionResult> TopVipRiskCustomers
);

public record CalculatePltvRequest(
    Guid TenantId,
    List<CustomerTransactionHistory> Customers,
    decimal GlobalPriorMeanOrderValue = 50.0m
);

#endregion

public interface IPredictiveLtvService
{
    Task<PltvPredictionResult> PredictCustomerLtvAsync(
        Guid tenantId,
        CustomerTransactionHistory history,
        decimal priorMeanOrderValue = 50.0m,
        CancellationToken ct = default);

    Task<PltvBatchCohortSummary> PredictCohortBatchAsync(
        CalculatePltvRequest request,
        CancellationToken ct = default);

    Task<List<PltvPredictionResult>> GetAtRiskVipInterventionsAsync(
        Guid tenantId,
        CancellationToken ct = default);
}

public class PredictiveLtvService : IPredictiveLtvService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, PltvPredictionResult>> _predictions = new();

    public Task<PltvPredictionResult> PredictCustomerLtvAsync(
        Guid tenantId,
        CustomerTransactionHistory history,
        decimal priorMeanOrderValue = 50.0m,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (history.TotalObservationDays < 0)
        {
            throw new ArgumentException("Total observation days cannot be negative.", nameof(history));
        }

        if (history.RecencyDays > history.TotalObservationDays)
        {
            throw new ArgumentException("Recency cannot exceed total observation days.", nameof(history));
        }

        double T = Math.Max(1.0, history.TotalObservationDays);
        double tx = Math.Max(0.0, history.RecencyDays);
        int x = Math.Max(0, history.Frequency);
        decimal mx = Math.Max(0m, history.HistoricalAverageOrderValue);

        // 1. Probabilistic P(Alive) estimation via BG/NBD latent transaction dynamics
        double recencyGap = Math.Max(0.0, T - tx);
        double expectedInterPurchaseDays = x > 1 ? (T / (x + 1.0)) : 30.0;
        
        double pAlive;
        if (x == 0)
        {
            pAlive = Math.Max(0.01, 1.0 / (1.0 + Math.Pow(T / 60.0, 1.2)));
        }
        else if (recencyGap <= expectedInterPurchaseDays)
        {
            pAlive = 1.0 - (0.15 * (recencyGap / Math.Max(1.0, expectedInterPurchaseDays)));
        }
        else
        {
            double overdueRatio = (recencyGap - expectedInterPurchaseDays) / Math.Max(1.0, expectedInterPurchaseDays);
            pAlive = 0.85 / (1.0 + Math.Pow(overdueRatio, 1.5));
        }

        pAlive = Math.Clamp(pAlive, 0.0, 1.0);
        double churnRiskScore = Math.Round((1.0 - pAlive) * 100.0, 2);

        // 2. Expected Future Transactions in Days t
        // E[Y(t) | x, tx, T] = P(Alive) * ((x + 1) / (T + 30)) * t
        double purchaseVelocityPerDay = (x + 1.0) / (T + 30.0);
        double expectedTx30d = Math.Round(pAlive * purchaseVelocityPerDay * 30.0, 3);
        double expectedTx90d = Math.Round(pAlive * purchaseVelocityPerDay * 90.0, 3);
        double expectedTx365d = Math.Round(pAlive * purchaseVelocityPerDay * 365.0, 3);

        // 3. Gamma-Gamma Sub-Model for Expected Monetary Value
        // Bayesian shrinkage towards prior population mean
        decimal priorWeight = 5.0m;
        decimal expectedAov = x == 0
            ? priorMeanOrderValue
            : ((x * mx) + (priorWeight * priorMeanOrderValue)) / (x + priorWeight);

        expectedAov = Math.Round(expectedAov, 2);

        // 4. Predicted Residual Lifetime Value (pLTV)
        decimal pltv90d = Math.Round((decimal)expectedTx90d * expectedAov, 2);
        decimal pltv365d = Math.Round((decimal)expectedTx365d * expectedAov, 2);

        // 5. Customer Tier Classification
        CustomerPltvTier tier;
        if (pltv365d >= 2000m && pAlive >= 0.70)
        {
            tier = CustomerPltvTier.ChampionWhale;
        }
        else if (history.TotalLifetimeRevenue >= 400m && pAlive < 0.45)
        {
            tier = CustomerPltvTier.AtRiskDefecting;
        }
        else if (pAlive < 0.20)
        {
            tier = CustomerPltvTier.LostDormant;
        }
        else if (x <= 1 && pAlive >= 0.75 && pltv365d >= 350m)
        {
            tier = CustomerPltvTier.HighPotentialNewbie;
        }
        else if (pltv365d >= 600m && pAlive >= 0.55)
        {
            tier = CustomerPltvTier.SteadyLoyal;
        }
        else
        {
            tier = CustomerPltvTier.StandardOccasional;
        }

        bool requiresWinBack = (tier == CustomerPltvTier.AtRiskDefecting) && (history.TotalLifetimeRevenue >= 300m || pltv365d >= 250m);

        var result = new PltvPredictionResult(
            history.CustomerId,
            history.CustomerName,
            history.Email,
            Math.Round(pAlive, 4),
            churnRiskScore,
            expectedTx30d,
            expectedTx90d,
            expectedTx365d,
            expectedAov,
            pltv90d,
            pltv365d,
            tier,
            requiresWinBack,
            DateTime.UtcNow
        );

        var tenantStore = _predictions.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, PltvPredictionResult>());
        tenantStore[history.CustomerId] = result;

        return Task.FromResult(result);
    }

    public async Task<PltvBatchCohortSummary> PredictCohortBatchAsync(
        CalculatePltvRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var results = new List<PltvPredictionResult>();
        foreach (var customer in request.Customers)
        {
            var res = await PredictCustomerLtvAsync(request.TenantId, customer, request.GlobalPriorMeanOrderValue, ct);
            results.Add(res);
        }

        int champions = results.Count(r => r.Tier == CustomerPltvTier.ChampionWhale);
        int loyal = results.Count(r => r.Tier == CustomerPltvTier.SteadyLoyal);
        int newbies = results.Count(r => r.Tier == CustomerPltvTier.HighPotentialNewbie);
        int atRisk = results.Count(r => r.Tier == CustomerPltvTier.AtRiskDefecting);
        int lost = results.Count(r => r.Tier == CustomerPltvTier.LostDormant);

        decimal totalPredicted365d = results.Sum(r => r.PredictedResidualLtv365d);
        double avgRetention = results.Count > 0 ? results.Average(r => r.ProbabilityAlive) : 0.0;

        var topVipRisks = results
            .Where(r => r.RequiresAutomatedWinBackIntervention)
            .OrderByDescending(r => r.PredictedResidualLtv365d)
            .Take(10)
            .ToList();

        var summary = new PltvBatchCohortSummary(
            request.TenantId,
            results.Count,
            champions,
            loyal,
            newbies,
            atRisk,
            lost,
            totalPredicted365d,
            Math.Round(avgRetention, 4),
            topVipRisks
        );

        return summary;
    }

    public Task<List<PltvPredictionResult>> GetAtRiskVipInterventionsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (!_predictions.TryGetValue(tenantId, out var tenantStore))
        {
            return Task.FromResult(new List<PltvPredictionResult>());
        }

        var interventions = tenantStore.Values
            .Where(r => r.RequiresAutomatedWinBackIntervention)
            .OrderByDescending(r => r.PredictedResidualLtv365d)
            .ToList();

        return Task.FromResult(interventions);
    }
}
