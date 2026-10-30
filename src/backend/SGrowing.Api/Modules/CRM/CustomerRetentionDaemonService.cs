using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SGrowing.Api.Modules.CRM;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RfmSegment
{
    Champions,
    LoyalCustomers,
    PotentialLoyalists,
    AtRisk,
    Hibernating,
    AboutToSleep,
    Lost
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChurnRiskTier
{
    Low,        // P < 0.25
    Moderate,   // 0.25 <= P < 0.50
    AtRisk,     // 0.50 <= P < 0.75
    Critical    // P >= 0.75
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WinbackPlaybookType
{
    VipDiscountOffer,        // 20% discount coupon sent over WhatsApp/Email
    SuccessManagerReachout,  // High-priority CRM task auto-assigned to Account Executive
    ExecutivePulseSurvey     // 1-click micro-survey to uncover dissatisfaction
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WinbackStatus
{
    Pending,
    Dispatched,
    Opened,
    Converted,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WinbackChannel
{
    WhatsApp,
    Email,
    PhoneCall,
    InApp
}

#endregion

#region Models & DTOs

public record RfmQuintiles(
    int RecencyScore,   // 1 - 5 (5 is most recent)
    int FrequencyScore, // 1 - 5 (5 is most frequent)
    int MonetaryScore   // 1 - 5 (5 is highest spend)
)
{
    public string Cell => $"{RecencyScore}-{FrequencyScore}-{MonetaryScore}";
}

public record CustomerRfmProfile
{
    public Guid CustomerId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public int RecencyDays { get; init; }
    public int FrequencyOrders { get; init; }
    public decimal MonetaryTotalLtv { get; init; }
    public int RecencyScore { get; init; }
    public int FrequencyScore { get; init; }
    public int MonetaryScore { get; init; }
    public string RfmCell { get; init; } = "1-1-1";
    public RfmSegment Segment { get; init; }
    public int UnresolvedTickets { get; init; }
    public int NegativeCsatCount { get; init; }
    public double ChurnProbability { get; init; }
    public ChurnRiskTier RiskTier { get; init; }
    public DateTime LastEvaluatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastPurchaseDateUtc { get; init; } = DateTime.UtcNow;
    public WinbackCampaignDto? ActivePlaybook { get; init; }
}

public record RfmCohortDistributionDto(
    RfmSegment Segment,
    string SegmentLabel,
    int CustomerCount,
    double Percentage,
    decimal TotalLtv,
    decimal AverageLtv,
    double AverageChurnRisk,
    string RecommendedAction,
    string ColorBadge
);

public record RetentionOverviewKpisDto(
    double ChurnRiskRatePercent,
    int TotalCustomersEvaluated,
    int AtRiskAccountsCount,
    int CriticalAccountsCount,
    decimal TotalAtRiskLtv,
    decimal RecoveredRevenue,
    int ActiveWinbackPlaybooksCount,
    int TotalWinbacksDispatched,
    double WinbackConversionRatePercent
);

public record WinbackCampaignDto(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    WinbackPlaybookType PlaybookType,
    WinbackChannel Channel,
    WinbackStatus Status,
    string OfferDetails,
    DateTime DispatchedAtUtc,
    DateTime? OpenedAtUtc,
    DateTime? ConvertedAtUtc,
    DateTime ExpiresAtUtc,
    decimal? RecoveredRevenue,
    string? AssignedAgent,
    string? Notes
);

public record EvaluateCustomerRequest(
    Guid? CustomerId,
    Guid? TenantId,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Company,
    int RecencyDays,
    int FrequencyOrders,
    decimal MonetaryTotalLtv,
    int UnresolvedTickets,
    int NegativeCsatCount
);

public record TriggerWinbackRequest(
    Guid TenantId,
    Guid CustomerId,
    WinbackPlaybookType? PlaybookType = null,
    WinbackChannel? Channel = null,
    string? CustomOffer = null,
    string? AssignedAgent = null,
    string? Notes = null
);

public record RecordConversionRequest(
    decimal ConvertedOrderAmount,
    string? OrderReference = null,
    string? ConversionNotes = null
);

public record DaemonScanResultDto(
    Guid TenantId,
    int TotalAccountsScanned,
    int BreachedAccountsCount,
    int PlaybooksDispatchedCount,
    List<WinbackCampaignDto> DispatchedPlaybooks,
    DateTime ScannedAtUtc
);

#endregion

#region Service Contract

public interface ICustomerRetentionDaemonService
{
    RfmQuintiles CalculateRfmQuintiles(int recencyDays, int frequencyOrders, decimal monetarySpend);
    RfmSegment ClassifyRfmSegment(int recencyScore, int frequencyScore, int monetaryScore);
    double CalculateChurnProbability(int recencyScore, int frequencyScore, int monetaryScore, int unresolvedTickets, int negativeCsatCount);
    ChurnRiskTier ClassifyRiskTier(double churnProbability);

    Task<CustomerRfmProfile> EvaluateCustomerAsync(Guid tenantId, EvaluateCustomerRequest request, CancellationToken ct = default);
    Task<List<CustomerRfmProfile>> GetCustomerProfilesAsync(Guid tenantId, ChurnRiskTier? filterTier = null, RfmSegment? filterSegment = null, CancellationToken ct = default);
    Task<CustomerRfmProfile?> GetCustomerProfileByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);

    Task<RetentionOverviewKpisDto> GetOverviewKpisAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<RfmCohortDistributionDto>> GetRfmCohortsAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<CustomerRfmProfile>> GetAtRiskCustomersAsync(Guid tenantId, ChurnRiskTier? minTier = null, CancellationToken ct = default);

    Task<DaemonScanResultDto> RunPreemptiveWinbackDaemonScanAsync(Guid tenantId, double churnThreshold = 0.50, CancellationToken ct = default);
    Task<WinbackCampaignDto> TriggerWinbackPlaybookAsync(Guid tenantId, TriggerWinbackRequest request, CancellationToken ct = default);
    Task<List<WinbackCampaignDto>> GetWinbackCampaignsAsync(Guid tenantId, WinbackStatus? status = null, CancellationToken ct = default);
    Task<WinbackCampaignDto> RecordWinbackConversionAsync(Guid tenantId, Guid campaignId, RecordConversionRequest request, CancellationToken ct = default);
    Task<WinbackCampaignDto> UpdateWinbackStatusAsync(Guid tenantId, Guid campaignId, WinbackStatus status, CancellationToken ct = default);
}

#endregion

#region Implementation

public class CustomerRetentionDaemonService : ICustomerRetentionDaemonService
{
    private readonly ILogger<CustomerRetentionDaemonService> _logger;

    // Logistic Regression calibrated parameters
    // P(churn) = 1 / (1 + exp(-(beta_0 - beta_R * R - beta_F * F - beta_M * M + gamma * UnresolvedTickets + delta * NegativeCsatCount)))
    private const double Beta0 = 1.85;  // Baseline intercept
    private const double BetaR = 0.65;  // Recency weight
    private const double BetaF = 0.45;  // Frequency weight
    private const double BetaM = 0.35;  // Monetary weight
    private const double Gamma = 0.85;  // Unresolved tickets weight
    private const double Delta = 1.10;  // Negative CSAT count weight

    // In-memory multi-tenant state store
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CustomerRfmProfile>> _profilesByTenant = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, WinbackCampaignDto>> _campaignsByTenant = new();

    public static CustomerRetentionDaemonService Default { get; } = new(NullLogger<CustomerRetentionDaemonService>.Instance);

    public CustomerRetentionDaemonService(ILogger<CustomerRetentionDaemonService>? logger = null)
    {
        _logger = logger ?? NullLogger<CustomerRetentionDaemonService>.Instance;
    }

    #region RFM & Churn Math

    /// <summary>
    /// Computes RFM quintiles (1 to 5) based on standard industry benchmark cutoffs.
    /// Lower Recency days = higher score (5 = most recent).
    /// Higher Frequency & Monetary = higher score (5 = most valuable).
    /// </summary>
    public RfmQuintiles CalculateRfmQuintiles(int recencyDays, int frequencyOrders, decimal monetarySpend)
    {
        // Recency Score (Days since last interaction / order)
        int r = recencyDays switch
        {
            <= 14 => 5,
            <= 30 => 4,
            <= 60 => 3,
            <= 120 => 2,
            _ => 1
        };

        // Frequency Score (Total completed orders / key actions)
        int f = frequencyOrders switch
        {
            >= 10 => 5,
            >= 6 => 4,
            >= 3 => 3,
            >= 2 => 2,
            _ => 1
        };

        // Monetary Score (LTV Spend)
        int m = monetarySpend switch
        {
            >= 2000m => 5,
            >= 1000m => 4,
            >= 500m => 3,
            >= 200m => 2,
            _ => 1
        };

        return new RfmQuintiles(r, f, m);
    }

    /// <summary>
    /// Classifies an account into standard RFM Segments:
    /// Champions, Loyal Customers, Potential Loyalists, At Risk, Hibernating, About to Sleep, Lost.
    /// </summary>
    public RfmSegment ClassifyRfmSegment(int recencyScore, int frequencyScore, int monetaryScore)
    {
        // 1. Champions: Top across all dimensions
        if (recencyScore >= 4 && frequencyScore >= 4 && monetaryScore >= 4)
            return RfmSegment.Champions;

        // 2. Loyal Customers: Consistent regular buyers with good recency and frequency
        if (recencyScore >= 3 && frequencyScore >= 3 && monetaryScore >= 3)
            return RfmSegment.LoyalCustomers;

        // 3. Potential Loyalists: Recent buyers with low-to-medium frequency
        if (recencyScore >= 4 && frequencyScore <= 3)
            return RfmSegment.PotentialLoyalists;

        // 4. At Risk: Previously high frequency or high spenders who have stopped returning (Low R)
        if (recencyScore <= 2 && (frequencyScore >= 3 || monetaryScore >= 3))
            return RfmSegment.AtRisk;

        // 5. Lost: Lowest across the board
        if (recencyScore == 1 && frequencyScore == 1 && monetaryScore == 1)
            return RfmSegment.Lost;

        // 6. Hibernating: Long inactive past repeat buyers (R <= 2, F in 1..2, M in 1..2 with repeat purchase F >= 2 or M >= 2)
        if (recencyScore <= 2 && frequencyScore <= 2 && monetaryScore <= 2 && (frequencyScore >= 2 || monetaryScore >= 2))
            return RfmSegment.Hibernating;

        // 7. About to Sleep: Below average recency, minimal past frequency
        if ((recencyScore == 2 || recencyScore == 3) && frequencyScore <= 2 && monetaryScore <= 2)
            return RfmSegment.AboutToSleep;

        // Deterministic fallback
        if (recencyScore <= 2)
            return (frequencyScore >= 3 || monetaryScore >= 3) ? RfmSegment.AtRisk : RfmSegment.Hibernating;

        return (frequencyScore >= 3) ? RfmSegment.LoyalCustomers : RfmSegment.PotentialLoyalists;
    }

    /// <summary>
    /// Calculates the calibrated Logistic Churn Probability score P(churn) [0.0000 .. 1.0000].
    /// P(churn) = 1 / (1 + exp(-(beta_0 - beta_R * R - beta_F * F - beta_M * M + gamma * UnresolvedTickets + delta * NegativeCsatCount)))
    /// </summary>
    public double CalculateChurnProbability(int recencyScore, int frequencyScore, int monetaryScore, int unresolvedTickets, int negativeCsatCount)
    {
        double z = Beta0
                   - (BetaR * recencyScore)
                   - (BetaF * frequencyScore)
                   - (BetaM * monetaryScore)
                   + (Gamma * Math.Max(0, unresolvedTickets))
                   + (Delta * Math.Max(0, negativeCsatCount));

        // Safeguard against floating point overflow in exp(-z)
        if (z < -35.0) return 0.0001;
        if (z > 35.0) return 0.9999;

        double prob = 1.0 / (1.0 + Math.Exp(-z));
        return Math.Round(Math.Clamp(prob, 0.0001, 0.9999), 4);
    }

    /// <summary>
    /// Classifies churn probability into 4 risk tiers:
    /// Low (< 0.25), Moderate (0.25 - 0.49), AtRisk (0.50 - 0.74), Critical (>= 0.75).
    /// </summary>
    public ChurnRiskTier ClassifyRiskTier(double churnProbability)
    {
        return churnProbability switch
        {
            < 0.25 => ChurnRiskTier.Low,
            < 0.50 => ChurnRiskTier.Moderate,
            < 0.75 => ChurnRiskTier.AtRisk,
            _ => ChurnRiskTier.Critical
        };
    }

    #endregion

    #region Evaluation & Query APIs

    public Task<CustomerRfmProfile> EvaluateCustomerAsync(Guid tenantId, EvaluateCustomerRequest request, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());

        var quintiles = CalculateRfmQuintiles(request.RecencyDays, request.FrequencyOrders, request.MonetaryTotalLtv);
        var segment = ClassifyRfmSegment(quintiles.RecencyScore, quintiles.FrequencyScore, quintiles.MonetaryScore);
        var churnProb = CalculateChurnProbability(
            quintiles.RecencyScore,
            quintiles.FrequencyScore,
            quintiles.MonetaryScore,
            request.UnresolvedTickets,
            request.NegativeCsatCount);
        var riskTier = ClassifyRiskTier(churnProb);

        var custId = request.CustomerId ?? Guid.NewGuid();

        // Check if an existing profile had an active playbook
        tenantProfiles.TryGetValue(custId, out var existing);

        var profile = new CustomerRfmProfile
        {
            CustomerId = custId,
            TenantId = tenantId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? (existing?.CustomerName ?? "Customer " + custId.ToString()[..8]) : request.CustomerName,
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? (existing?.CustomerEmail ?? $"client_{custId.ToString()[..6]}@business.com") : request.CustomerEmail,
            CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? (existing?.CustomerPhone ?? "+216 98 000 000") : request.CustomerPhone,
            Company = string.IsNullOrWhiteSpace(request.Company) ? (existing?.Company ?? "Enterprise Client") : request.Company,
            RecencyDays = request.RecencyDays,
            FrequencyOrders = request.FrequencyOrders,
            MonetaryTotalLtv = request.MonetaryTotalLtv,
            RecencyScore = quintiles.RecencyScore,
            FrequencyScore = quintiles.FrequencyScore,
            MonetaryScore = quintiles.MonetaryScore,
            RfmCell = quintiles.Cell,
            Segment = segment,
            UnresolvedTickets = Math.Max(0, request.UnresolvedTickets),
            NegativeCsatCount = Math.Max(0, request.NegativeCsatCount),
            ChurnProbability = churnProb,
            RiskTier = riskTier,
            LastEvaluatedAtUtc = DateTime.UtcNow,
            LastPurchaseDateUtc = DateTime.UtcNow.AddDays(-request.RecencyDays),
            ActivePlaybook = existing?.ActivePlaybook
        };

        tenantProfiles[custId] = profile;
        _logger.LogInformation("Evaluated customer {CustomerId} RFM: {Cell}, Churn: {Prob}, Tier: {Tier}",
            custId, profile.RfmCell, profile.ChurnProbability, profile.RiskTier);

        return Task.FromResult(profile);
    }

    public Task<List<CustomerRfmProfile>> GetCustomerProfilesAsync(Guid tenantId, ChurnRiskTier? filterTier = null, RfmSegment? filterSegment = null, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());

        var query = tenantProfiles.Values.AsEnumerable();

        if (filterTier.HasValue)
            query = query.Where(p => p.RiskTier == filterTier.Value);

        if (filterSegment.HasValue)
            query = query.Where(p => p.Segment == filterSegment.Value);

        var list = query.OrderByDescending(p => p.ChurnProbability).ToList();
        return Task.FromResult(list);
    }

    public Task<CustomerRfmProfile?> GetCustomerProfileByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());
        tenantProfiles.TryGetValue(customerId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<List<CustomerRfmProfile>> GetAtRiskCustomersAsync(Guid tenantId, ChurnRiskTier? minTier = null, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());

        var threshold = minTier ?? ChurnRiskTier.AtRisk;
        var list = tenantProfiles.Values
            .Where(p => p.RiskTier == ChurnRiskTier.AtRisk || p.RiskTier == ChurnRiskTier.Critical || (threshold == ChurnRiskTier.Moderate && p.RiskTier == ChurnRiskTier.Moderate))
            .OrderByDescending(p => p.ChurnProbability)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<RetentionOverviewKpisDto> GetOverviewKpisAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());
        var tenantCampaigns = _campaignsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WinbackCampaignDto>());

        var profiles = tenantProfiles.Values.ToList();
        var campaigns = tenantCampaigns.Values.ToList();

        int totalCount = profiles.Count;
        int atRiskCount = profiles.Count(p => p.RiskTier == ChurnRiskTier.AtRisk);
        int criticalCount = profiles.Count(p => p.RiskTier == ChurnRiskTier.Critical);
        int totalAtRiskOrCritical = atRiskCount + criticalCount;

        double churnRiskRate = totalCount > 0
            ? Math.Round((double)totalAtRiskOrCritical / totalCount * 100.0, 1)
            : 0.0;

        decimal totalAtRiskLtv = profiles
            .Where(p => p.RiskTier == ChurnRiskTier.AtRisk || p.RiskTier == ChurnRiskTier.Critical)
            .Sum(p => p.MonetaryTotalLtv);

        decimal recoveredRevenue = campaigns
            .Where(c => c.Status == WinbackStatus.Converted && c.RecoveredRevenue.HasValue)
            .Sum(c => c.RecoveredRevenue!.Value);

        int activePlaybooks = campaigns.Count(c => c.Status == WinbackStatus.Dispatched || c.Status == WinbackStatus.Opened || c.Status == WinbackStatus.Pending);
        int totalDispatched = campaigns.Count(c => c.Status != WinbackStatus.Pending);
        int convertedCount = campaigns.Count(c => c.Status == WinbackStatus.Converted);

        double conversionRate = totalDispatched > 0
            ? Math.Round((double)convertedCount / totalDispatched * 100.0, 1)
            : 0.0;

        var kpi = new RetentionOverviewKpisDto(
            ChurnRiskRatePercent: churnRiskRate,
            TotalCustomersEvaluated: totalCount,
            AtRiskAccountsCount: atRiskCount,
            CriticalAccountsCount: criticalCount,
            TotalAtRiskLtv: totalAtRiskLtv,
            RecoveredRevenue: recoveredRevenue,
            ActiveWinbackPlaybooksCount: activePlaybooks,
            TotalWinbacksDispatched: totalDispatched,
            WinbackConversionRatePercent: conversionRate
        );

        return Task.FromResult(kpi);
    }

    public Task<List<RfmCohortDistributionDto>> GetRfmCohortsAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());
        var profiles = tenantProfiles.Values.ToList();
        int total = profiles.Count;

        var allSegments = Enum.GetValues<RfmSegment>();
        var result = new List<RfmCohortDistributionDto>();

        foreach (var seg in allSegments)
        {
            var cohort = profiles.Where(p => p.Segment == seg).ToList();
            int count = cohort.Count;
            double pct = total > 0 ? Math.Round((double)count / total * 100.0, 1) : 0.0;
            decimal totalLtv = cohort.Sum(p => p.MonetaryTotalLtv);
            decimal avgLtv = count > 0 ? Math.Round(totalLtv / count, 2) : 0m;
            double avgChurn = count > 0 ? Math.Round(cohort.Average(p => p.ChurnProbability), 3) : 0.0;

            var (label, action, color) = GetSegmentMetadata(seg);

            result.Add(new RfmCohortDistributionDto(
                Segment: seg,
                SegmentLabel: label,
                CustomerCount: count,
                Percentage: pct,
                TotalLtv: totalLtv,
                AverageLtv: avgLtv,
                AverageChurnRisk: avgChurn,
                RecommendedAction: action,
                ColorBadge: color
            ));
        }

        return Task.FromResult(result);
    }

    #endregion

    #region Winback Daemon & Playbooks

    /// <summary>
    /// Scans accounts with P(churn) >= churnThreshold (default 0.50).
    /// If an account does not have an active winback campaign, automatically dispatches
    /// an appropriate retention playbook.
    /// </summary>
    public Task<DaemonScanResultDto> RunPreemptiveWinbackDaemonScanAsync(Guid tenantId, double churnThreshold = 0.50, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());
        var tenantCampaigns = _campaignsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WinbackCampaignDto>());

        var allProfiles = tenantProfiles.Values.ToList();
        var breachedProfiles = allProfiles.Where(p => p.ChurnProbability >= churnThreshold).ToList();

        var dispatchedList = new List<WinbackCampaignDto>();

        foreach (var profile in breachedProfiles)
        {
            // Check if account already has an active or recent campaign
            bool hasActive = tenantCampaigns.Values.Any(c =>
                c.CustomerId == profile.CustomerId &&
                (c.Status == WinbackStatus.Dispatched || c.Status == WinbackStatus.Opened || c.Status == WinbackStatus.Pending));

            if (hasActive) continue;

            // Choose optimal playbook
            var (playbook, channel, offer, notes) = SelectOptimalPlaybook(profile);

            var campaign = new WinbackCampaignDto(
                Id: Guid.NewGuid(),
                TenantId: tenantId,
                CustomerId: profile.CustomerId,
                CustomerName: profile.CustomerName,
                CustomerEmail: profile.CustomerEmail,
                PlaybookType: playbook,
                Channel: channel,
                Status: WinbackStatus.Dispatched,
                OfferDetails: offer,
                DispatchedAtUtc: DateTime.UtcNow,
                OpenedAtUtc: null,
                ConvertedAtUtc: null,
                ExpiresAtUtc: DateTime.UtcNow.AddDays(30),
                RecoveredRevenue: null,
                AssignedAgent: playbook == WinbackPlaybookType.SuccessManagerReachout ? "Executive AE (Auto-Assigned)" : null,
                Notes: notes
            );

            tenantCampaigns[campaign.Id] = campaign;
            tenantProfiles[profile.CustomerId] = profile with { ActivePlaybook = campaign };
            dispatchedList.Add(campaign);

            _logger.LogInformation("Preemptive Winback triggered for customer {CustomerId} ({Playbook}) via {Channel}",
                profile.CustomerId, playbook, channel);
        }

        var result = new DaemonScanResultDto(
            TenantId: tenantId,
            TotalAccountsScanned: allProfiles.Count,
            BreachedAccountsCount: breachedProfiles.Count,
            PlaybooksDispatchedCount: dispatchedList.Count,
            DispatchedPlaybooks: dispatchedList,
            ScannedAtUtc: DateTime.UtcNow
        );

        return Task.FromResult(result);
    }

    public Task<WinbackCampaignDto> TriggerWinbackPlaybookAsync(Guid tenantId, TriggerWinbackRequest request, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());
        var tenantCampaigns = _campaignsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WinbackCampaignDto>());

        if (!tenantProfiles.TryGetValue(request.CustomerId, out var profile))
        {
            // Create minimal profile if not found
            profile = new CustomerRfmProfile
            {
                CustomerId = request.CustomerId,
                TenantId = tenantId,
                CustomerName = "Target Customer",
                CustomerEmail = "customer@enterprise.com",
                RecencyDays = 45,
                FrequencyOrders = 4,
                MonetaryTotalLtv = 1200m,
                ChurnProbability = 0.65,
                RiskTier = ChurnRiskTier.AtRisk
            };
            tenantProfiles[request.CustomerId] = profile;
        }

        var (autoPlaybook, autoChannel, autoOffer, autoNotes) = SelectOptimalPlaybook(profile);

        var playbook = request.PlaybookType ?? autoPlaybook;
        var channel = request.Channel ?? autoChannel;
        var offer = !string.IsNullOrWhiteSpace(request.CustomOffer) ? request.CustomOffer : autoOffer;
        var agent = request.AssignedAgent ?? (playbook == WinbackPlaybookType.SuccessManagerReachout ? "Dedicated Account Rep" : null);

        var campaign = new WinbackCampaignDto(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: profile.CustomerId,
            CustomerName: profile.CustomerName,
            CustomerEmail: profile.CustomerEmail,
            PlaybookType: playbook,
            Channel: channel,
            Status: WinbackStatus.Dispatched,
            OfferDetails: offer,
            DispatchedAtUtc: DateTime.UtcNow,
            OpenedAtUtc: null,
            ConvertedAtUtc: null,
            ExpiresAtUtc: DateTime.UtcNow.AddDays(30),
            RecoveredRevenue: null,
            AssignedAgent: agent,
            Notes: request.Notes ?? autoNotes
        );

        tenantCampaigns[campaign.Id] = campaign;
        tenantProfiles[profile.CustomerId] = profile with { ActivePlaybook = campaign };

        _logger.LogInformation("Manually triggered winback playbook {Playbook} for customer {CustomerId}", playbook, profile.CustomerId);

        return Task.FromResult(campaign);
    }

    public Task<List<WinbackCampaignDto>> GetWinbackCampaignsAsync(Guid tenantId, WinbackStatus? status = null, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantCampaigns = _campaignsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WinbackCampaignDto>());

        var query = tenantCampaigns.Values.AsEnumerable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var list = query.OrderByDescending(c => c.DispatchedAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<WinbackCampaignDto> RecordWinbackConversionAsync(Guid tenantId, Guid campaignId, RecordConversionRequest request, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantCampaigns = _campaignsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WinbackCampaignDto>());
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());

        if (!tenantCampaigns.TryGetValue(campaignId, out var campaign))
        {
            throw new KeyNotFoundException($"Winback campaign '{campaignId}' not found for tenant '{tenantId}'.");
        }

        if (request.ConvertedOrderAmount <= 0)
        {
            throw new ArgumentException("Converted order amount must be greater than zero.", nameof(request.ConvertedOrderAmount));
        }

        var converted = campaign with
        {
            Status = WinbackStatus.Converted,
            ConvertedAtUtc = DateTime.UtcNow,
            RecoveredRevenue = request.ConvertedOrderAmount,
            Notes = string.IsNullOrWhiteSpace(request.ConversionNotes)
                ? (campaign.Notes + $" | Re-engaged with order: {request.OrderReference ?? "REF-CONV"}")
                : request.ConversionNotes
        };

        tenantCampaigns[campaignId] = converted;

        // Also update customer profile to reflect reactivation (Recency resets, LTV increases)
        if (tenantProfiles.TryGetValue(campaign.CustomerId, out var profile))
        {
            int updatedFrequency = profile.FrequencyOrders + 1;
            decimal updatedLtv = profile.MonetaryTotalLtv + request.ConvertedOrderAmount;
            int updatedRecencyDays = 1; // Purchased today

            var quintiles = CalculateRfmQuintiles(updatedRecencyDays, updatedFrequency, updatedLtv);
            var segment = ClassifyRfmSegment(quintiles.RecencyScore, quintiles.FrequencyScore, quintiles.MonetaryScore);
            var churnProb = CalculateChurnProbability(
                quintiles.RecencyScore,
                quintiles.FrequencyScore,
                quintiles.MonetaryScore,
                Math.Max(0, profile.UnresolvedTickets - 1),
                0 // Dissatisfaction cleared on conversion
            );
            var riskTier = ClassifyRiskTier(churnProb);

            tenantProfiles[profile.CustomerId] = profile with
            {
                RecencyDays = updatedRecencyDays,
                FrequencyOrders = updatedFrequency,
                MonetaryTotalLtv = updatedLtv,
                RecencyScore = quintiles.RecencyScore,
                FrequencyScore = quintiles.FrequencyScore,
                MonetaryScore = quintiles.MonetaryScore,
                RfmCell = quintiles.Cell,
                Segment = segment,
                NegativeCsatCount = 0,
                ChurnProbability = churnProb,
                RiskTier = riskTier,
                LastPurchaseDateUtc = DateTime.UtcNow,
                LastEvaluatedAtUtc = DateTime.UtcNow,
                ActivePlaybook = converted
            };
        }

        _logger.LogInformation("Recorded Winback Conversion for campaign {CampaignId}. Recovered Revenue: {Revenue:C}",
            campaignId, request.ConvertedOrderAmount);

        return Task.FromResult(converted);
    }

    public Task<WinbackCampaignDto> UpdateWinbackStatusAsync(Guid tenantId, Guid campaignId, WinbackStatus status, CancellationToken ct = default)
    {
        EnsureTenantSeeded(tenantId);
        var tenantCampaigns = _campaignsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WinbackCampaignDto>());
        var tenantProfiles = _profilesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerRfmProfile>());

        if (!tenantCampaigns.TryGetValue(campaignId, out var campaign))
        {
            throw new KeyNotFoundException($"Winback campaign '{campaignId}' not found for tenant '{tenantId}'.");
        }

        var updated = campaign with
        {
            Status = status,
            OpenedAtUtc = status == WinbackStatus.Opened && !campaign.OpenedAtUtc.HasValue ? DateTime.UtcNow : campaign.OpenedAtUtc
        };

        tenantCampaigns[campaignId] = updated;

        if (tenantProfiles.TryGetValue(campaign.CustomerId, out var profile) && profile.ActivePlaybook?.Id == campaignId)
        {
            tenantProfiles[campaign.CustomerId] = profile with { ActivePlaybook = updated };
        }

        return Task.FromResult(updated);
    }

    #endregion

    #region Internal Helpers & Seed Data

    private static (WinbackPlaybookType Playbook, WinbackChannel Channel, string Offer, string Notes) SelectOptimalPlaybook(CustomerRfmProfile profile)
    {
        // 1. Negative CSAT -> Send 1-click Executive Pulse Survey to resolve complaints
        if (profile.NegativeCsatCount > 0)
        {
            return (
                WinbackPlaybookType.ExecutivePulseSurvey,
                WinbackChannel.WhatsApp,
                "Executive Pulse: 'Help Us Improve' + $25 Credit Voucher",
                $"Customer reported {profile.NegativeCsatCount} negative CSAT rating(s). Direct micro-survey dispatched."
            );
        }

        // 2. High LTV or Critical Risk with unresolved tickets -> Personal Account Executive outreach
        if (profile.RiskTier == ChurnRiskTier.Critical || profile.UnresolvedTickets >= 2 || profile.MonetaryScore >= 4)
        {
            return (
                WinbackPlaybookType.SuccessManagerReachout,
                WinbackChannel.PhoneCall,
                "VIP Concierge Check-in & Priority Resolution SLA Guarantee",
                $"High-priority AE consultation assigned. Account LTV: {profile.MonetaryTotalLtv:C}, Unresolved tickets: {profile.UnresolvedTickets}."
            );
        }

        // 3. Default At-Risk Winback -> VIP Discount Offer (WhatsApp or Email)
        return (
            WinbackPlaybookType.VipDiscountOffer,
            WinbackChannel.WhatsApp,
            "Exclusive 20% Off Reactivation Voucher (Code: WINBACK20-EXCLUSIVE)",
            "Automated promotional winback discount triggered by RFM recency decay."
        );
    }

    private static (string Label, string RecommendedAction, string ColorBadge) GetSegmentMetadata(RfmSegment segment) => segment switch
    {
        RfmSegment.Champions => (
            "Champions",
            "Reward with exclusive VIP early access, loyalty perks, and advocate referrals.",
            "text-emerald-400 bg-emerald-500/10 border-emerald-500/20"
        ),
        RfmSegment.LoyalCustomers => (
            "Loyal Customers",
            "Upsell premium tiers, cross-sell related categories, and solicit product reviews.",
            "text-cyan-400 bg-cyan-500/10 border-cyan-500/20"
        ),
        RfmSegment.PotentialLoyalists => (
            "Potential Loyalists",
            "Offer membership programs or second-purchase incentives to build buying habit.",
            "text-blue-400 bg-blue-500/10 border-blue-500/20"
        ),
        RfmSegment.AtRisk => (
            "At Risk Customers",
            "Urgent: send personalized high-value winback coupons, phone calls, and check-ins.",
            "text-amber-400 bg-amber-500/10 border-amber-500/20"
        ),
        RfmSegment.AboutToSleep => (
            "About to Sleep",
            "Share popular new products and reactivation discounts before they enter hibernation.",
            "text-orange-400 bg-orange-500/10 border-orange-500/20"
        ),
        RfmSegment.Hibernating => (
            "Hibernating",
            "Offer relevant discounts on previous purchase categories; re-qualify interest.",
            "text-rose-400 bg-rose-500/10 border-rose-500/20"
        ),
        RfmSegment.Lost => (
            "Lost Customers",
            "Run automated winback campaigns or exit surveys to learn why they departed.",
            "text-slate-400 bg-slate-500/10 border-slate-500/20"
        ),
        _ => ("Unknown", "Monitor account behavior.", "text-slate-400 bg-slate-500/10 border-slate-500/20")
    };

    private void EnsureTenantSeeded(Guid tenantId)
    {
        if (_profilesByTenant.ContainsKey(tenantId)) return;

        var profileMap = new ConcurrentDictionary<Guid, CustomerRfmProfile>();
        var campaignMap = new ConcurrentDictionary<Guid, WinbackCampaignDto>();

        // Seed 8 realistic accounts covering diverse RFM segments and churn risk tiers
        var mockCustomers = new[]
        {
            // Champions: High R, High F, High M (Low Churn)
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111101"), Name = "Acme Global Enterprise", Email = "procurement@acmeglobal.com", Phone = "+216 71 800 100", Company = "Acme Corp", RDays = 6, FCount = 24, Spend = 14500m, Tickets = 0, Csat = 0 },
            // Loyal Customers: Good R, Good F, Good M (Low Churn)
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111102"), Name = "Atlas Logistics SARL", Email = "contact@atlas-logistics.tn", Phone = "+216 70 330 440", Company = "Atlas Logistics", RDays = 22, FCount = 9, Spend = 3800m, Tickets = 0, Csat = 0 },
            // Potential Loyalist: Recent first/second purchase
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111103"), Name = "Sofia Ben Amor", Email = "sofia.benamor@gmail.com", Phone = "+216 98 123 456", Company = "Ben Amor Retail", RDays = 12, FCount = 3, Spend = 650m, Tickets = 0, Csat = 0 },
            // At Risk: High historical spend, but inactive for 75 days! (AtRisk churn tier)
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111104"), Name = "Global Retailers Ltd", Email = "supply@globalretailers.com", Phone = "+216 71 990 011", Company = "Global Retailers", RDays = 78, FCount = 7, Spend = 4200m, Tickets = 1, Csat = 0 },
            // At Risk: Unresolved ticket + negative CSAT breach! (Critical churn tier)
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111105"), Name = "Carthage Distribution", Email = "direction@carthage-distrib.com", Phone = "+216 72 450 670", Company = "Carthage Distribution", RDays = 95, FCount = 8, Spend = 5600m, Tickets = 2, Csat = 2 },
            // About to Sleep: Inactive 45 days, moderate past buyer
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111106"), Name = "Moncef Karray", Email = "moncef.k@startup.tn", Phone = "+216 97 888 777", Company = "Karray Tech", RDays = 55, FCount = 2, Spend = 340m, Tickets = 0, Csat = 0 },
            // Hibernating: Inactive 140 days, low frequency
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111107"), Name = "BioPharma Maghreb", Email = "ops@biopharma-maghreb.com", Phone = "+216 73 220 110", Company = "BioPharma Maghreb", RDays = 145, FCount = 2, Spend = 450m, Tickets = 1, Csat = 1 },
            // Lost: Inactive 210 days, 1 order, poor experience
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111108"), Name = "MedTech Solutions", Email = "admin@medtech-sol.tn", Phone = "+216 78 500 200", Company = "MedTech Solutions", RDays = 210, FCount = 1, Spend = 120m, Tickets = 3, Csat = 2 }
        };

        foreach (var m in mockCustomers)
        {
            var quintiles = CalculateRfmQuintiles(m.RDays, m.FCount, m.Spend);
            var segment = ClassifyRfmSegment(quintiles.RecencyScore, quintiles.FrequencyScore, quintiles.MonetaryScore);
            var churn = CalculateChurnProbability(quintiles.RecencyScore, quintiles.FrequencyScore, quintiles.MonetaryScore, m.Tickets, m.Csat);
            var tier = ClassifyRiskTier(churn);

            profileMap[m.Id] = new CustomerRfmProfile
            {
                CustomerId = m.Id,
                TenantId = tenantId,
                CustomerName = m.Name,
                CustomerEmail = m.Email,
                CustomerPhone = m.Phone,
                Company = m.Company,
                RecencyDays = m.RDays,
                FrequencyOrders = m.FCount,
                MonetaryTotalLtv = m.Spend,
                RecencyScore = quintiles.RecencyScore,
                FrequencyScore = quintiles.FrequencyScore,
                MonetaryScore = quintiles.MonetaryScore,
                RfmCell = quintiles.Cell,
                Segment = segment,
                UnresolvedTickets = m.Tickets,
                NegativeCsatCount = m.Csat,
                ChurnProbability = churn,
                RiskTier = tier,
                LastPurchaseDateUtc = DateTime.UtcNow.AddDays(-m.RDays),
                LastEvaluatedAtUtc = DateTime.UtcNow
            };
        }

        // Historic Winback Campaigns: Converted, Dispatched, Opened
        var histCampaign1 = new WinbackCampaignDto(
            Id: Guid.Parse("22222222-2222-2222-2222-222222222201"),
            TenantId: tenantId,
            CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111102"),
            CustomerName: "Atlas Logistics SARL",
            CustomerEmail: "contact@atlas-logistics.tn",
            PlaybookType: WinbackPlaybookType.VipDiscountOffer,
            Channel: WinbackChannel.WhatsApp,
            Status: WinbackStatus.Converted,
            OfferDetails: "20% VIP Re-engagement Coupon (VIP20-ATLAS)",
            DispatchedAtUtc: DateTime.UtcNow.AddDays(-18),
            OpenedAtUtc: DateTime.UtcNow.AddDays(-17),
            ConvertedAtUtc: DateTime.UtcNow.AddDays(-14),
            ExpiresAtUtc: DateTime.UtcNow.AddDays(12),
            RecoveredRevenue: 2450.00m,
            AssignedAgent: "Automated Bot",
            Notes: "Client redeemed coupon within 4 days for bulk replenishments."
        );

        var histCampaign2 = new WinbackCampaignDto(
            Id: Guid.Parse("22222222-2222-2222-2222-222222222202"),
            TenantId: tenantId,
            CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111104"),
            CustomerName: "Global Retailers Ltd",
            CustomerEmail: "supply@globalretailers.com",
            PlaybookType: WinbackPlaybookType.SuccessManagerReachout,
            Channel: WinbackChannel.PhoneCall,
            Status: WinbackStatus.Dispatched,
            OfferDetails: "Enterprise Concierge SLA Check-in & Priority Account Review",
            DispatchedAtUtc: DateTime.UtcNow.AddDays(-3),
            OpenedAtUtc: null,
            ConvertedAtUtc: null,
            ExpiresAtUtc: DateTime.UtcNow.AddDays(27),
            RecoveredRevenue: null,
            AssignedAgent: "Karim Trabelsi (Senior AE)",
            Notes: "Outreach scheduled for tomorrow morning."
        );

        var histCampaign3 = new WinbackCampaignDto(
            Id: Guid.Parse("22222222-2222-2222-2222-222222222203"),
            TenantId: tenantId,
            CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111105"),
            CustomerName: "Carthage Distribution",
            CustomerEmail: "direction@carthage-distrib.com",
            PlaybookType: WinbackPlaybookType.ExecutivePulseSurvey,
            Channel: WinbackChannel.WhatsApp,
            Status: WinbackStatus.Opened,
            OfferDetails: "1-Click Service Dissatisfaction Micro-Survey + $50 Compensation Voucher",
            DispatchedAtUtc: DateTime.UtcNow.AddDays(-6),
            OpenedAtUtc: DateTime.UtcNow.AddDays(-5),
            ConvertedAtUtc: null,
            ExpiresAtUtc: DateTime.UtcNow.AddDays(24),
            RecoveredRevenue: null,
            AssignedAgent: "Customer Experience Team",
            Notes: "Survey link opened via WhatsApp; awaiting completion."
        );

        campaignMap[histCampaign1.Id] = histCampaign1;
        campaignMap[histCampaign2.Id] = histCampaign2;
        campaignMap[histCampaign3.Id] = histCampaign3;

        // Attach active campaign references to customer profiles
        if (profileMap.TryGetValue(histCampaign2.CustomerId, out var p4))
            profileMap[histCampaign2.CustomerId] = p4 with { ActivePlaybook = histCampaign2 };

        if (profileMap.TryGetValue(histCampaign3.CustomerId, out var p5))
            profileMap[histCampaign3.CustomerId] = p5 with { ActivePlaybook = histCampaign3 };

        _profilesByTenant[tenantId] = profileMap;
        _campaignsByTenant[tenantId] = campaignMap;
    }

    #endregion
}

#endregion
