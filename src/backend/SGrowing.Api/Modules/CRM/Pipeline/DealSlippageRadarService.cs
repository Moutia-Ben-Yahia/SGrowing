using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.CRM.Pipeline;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OpportunityStage
{
    Prospecting = 1,
    Qualification = 2,
    NeedsAnalysis = 3,
    ProposalQuote = 4,
    NegotiationReview = 5,
    ClosedWon = 6,
    ClosedLost = 7
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlippageRiskLevel
{
    LowHealthy,
    ModerateWatch,
    HighSlipping,
    CriticalStalled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GateRequirementType
{
    DecisionMakerIdentified,
    B2bPriceBookSelected,
    MarginThresholdMet,
    ContractEsignInitiated,
    SignedContractAttached
}

#endregion

#region Models & DTOs

public record StageTransitionHistoryEntry(
    OpportunityStage FromStage,
    OpportunityStage ToStage,
    DateTime TransitionedAtUtc,
    int DaysSpent,
    List<GateRequirementType> CompletedGates
);

public record StageGateRule(
    Guid RuleId,
    Guid TenantId,
    OpportunityStage FromStage,
    OpportunityStage ToStage,
    List<GateRequirementType> RequiredGateRequirements,
    int MinimumStageDurationDays,
    bool EnforceStrictBlock
);

public record OpportunityDealRecord
{
    public Guid DealId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;
    public decimal Amount { get; set; }
    public string Currency { get; init; } = "USD";
    public DateTime CloseDateUtc { get; set; }
    public DateTime OriginalCloseDateUtc { get; init; }
    public int CloseDatePushCount { get; set; }
    public int DaysInCurrentStage { get; set; }
    public DateTime LastCustomerContactAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? AssignedRepId { get; init; }
    public string AssignedRepName { get; init; } = string.Empty;
    public string? EconomicBuyerName { get; set; }
    public bool HasSignedContract { get; set; }
    public decimal GrossMarginPercent { get; set; } = 30.0m;
    public int SlippageScore { get; set; }
    public SlippageRiskLevel RiskLevel { get; set; } = SlippageRiskLevel.LowHealthy;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public List<StageTransitionHistoryEntry> StageHistory { get; init; } = new();
}

public record DealHealthAnalysis(
    Guid DealId,
    string Title,
    OpportunityStage Stage,
    decimal Amount,
    int SlippageScore,
    SlippageRiskLevel RiskLevel,
    double DecayedWinProbability,
    decimal WeightedAmount,
    List<string> RiskFactors,
    bool CanAdvanceToNextStage,
    List<GateRequirementType> MissingGateRequirements
);

public record PipelineSlippageSummary(
    Guid TenantId,
    decimal TotalPipelineAmount,
    decimal TotalWeightedPipeline,
    decimal AtRiskPipelineAmount,
    int TotalDealsCount,
    int SlippedDealsCount,
    double AverageSlippageScore,
    List<DealHealthAnalysis> AnalyzedDeals
);

public record CreateDealRequest(
    Guid TenantId,
    string Title,
    string AccountName,
    decimal Amount,
    DateTime CloseDateUtc,
    string AssignedRepName,
    string? EconomicBuyerName = null,
    decimal GrossMarginPercent = 30.0m
);

public record UpdateDealStageRequest(
    Guid TenantId,
    Guid DealId,
    OpportunityStage TargetStage,
    string? Actor = "SalesLeader"
);

public record PushCloseDateRequest(
    Guid TenantId,
    Guid DealId,
    DateTime NewCloseDateUtc,
    string Reason
);

public record DefineStageGateRuleRequest(
    Guid TenantId,
    OpportunityStage FromStage,
    OpportunityStage ToStage,
    List<GateRequirementType> RequiredGateRequirements,
    int MinimumStageDurationDays = 0,
    bool EnforceStrictBlock = true
);

#endregion

public interface IDealSlippageRadarService
{
    Task<OpportunityDealRecord> CreateDealAsync(CreateDealRequest request, CancellationToken ct = default);
    Task<OpportunityDealRecord?> GetDealByIdAsync(Guid tenantId, Guid dealId, CancellationToken ct = default);
    Task<List<OpportunityDealRecord>> ListDealsAsync(Guid tenantId, OpportunityStage? stageFilter = null, CancellationToken ct = default);

    Task<StageGateRule> DefineStageGateRuleAsync(DefineStageGateRuleRequest request, CancellationToken ct = default);
    Task<List<StageGateRule>> GetStageGateRulesAsync(Guid tenantId, CancellationToken ct = default);

    Task<DealHealthAnalysis> AnalyzeDealHealthAsync(Guid tenantId, Guid dealId, CancellationToken ct = default);
    Task<OpportunityDealRecord> AdvanceDealStageAsync(UpdateDealStageRequest request, CancellationToken ct = default);
    Task<OpportunityDealRecord> PushCloseDateAsync(PushCloseDateRequest request, CancellationToken ct = default);

    Task<PipelineSlippageSummary> GetPipelineSlippageSummaryAsync(Guid tenantId, CancellationToken ct = default);
}

public class DealSlippageRadarService : IDealSlippageRadarService
{
    private readonly ConcurrentDictionary<Guid, OpportunityDealRecord> _deals = new();
    private readonly ConcurrentDictionary<Guid, StageGateRule> _stageGateRules = new();

    public DealSlippageRadarService()
    {
        // Seed standard enterprise stage gate defaults
        SeedDefaultStageGates(Guid.Empty);
    }

    private void SeedDefaultStageGates(Guid tenantId)
    {
        // Rule: NeedsAnalysis -> ProposalQuote requires DecisionMakerIdentified
        var rule1 = new StageGateRule(
            Guid.NewGuid(), tenantId,
            OpportunityStage.NeedsAnalysis, OpportunityStage.ProposalQuote,
            new List<GateRequirementType> { GateRequirementType.DecisionMakerIdentified },
            0, true
        );
        // Rule: ProposalQuote -> NegotiationReview requires MarginThresholdMet (>=20%)
        var rule2 = new StageGateRule(
            Guid.NewGuid(), tenantId,
            OpportunityStage.ProposalQuote, OpportunityStage.NegotiationReview,
            new List<GateRequirementType> { GateRequirementType.MarginThresholdMet },
            0, true
        );
        // Rule: NegotiationReview -> ClosedWon requires SignedContractAttached
        var rule3 = new StageGateRule(
            Guid.NewGuid(), tenantId,
            OpportunityStage.NegotiationReview, OpportunityStage.ClosedWon,
            new List<GateRequirementType> { GateRequirementType.SignedContractAttached },
            0, true
        );

        _stageGateRules[rule1.RuleId] = rule1;
        _stageGateRules[rule2.RuleId] = rule2;
        _stageGateRules[rule3.RuleId] = rule3;
    }

    public Task<OpportunityDealRecord> CreateDealAsync(CreateDealRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Title is required.", nameof(request));
        if (request.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(request));

        var deal = new OpportunityDealRecord
        {
            TenantId = request.TenantId,
            Title = request.Title.Trim(),
            AccountName = request.AccountName?.Trim() ?? "New Account",
            Stage = OpportunityStage.Prospecting,
            Amount = request.Amount,
            CloseDateUtc = request.CloseDateUtc,
            OriginalCloseDateUtc = request.CloseDateUtc,
            CloseDatePushCount = 0,
            DaysInCurrentStage = 0,
            LastCustomerContactAtUtc = DateTime.UtcNow,
            AssignedRepName = request.AssignedRepName?.Trim() ?? "Unassigned",
            EconomicBuyerName = request.EconomicBuyerName?.Trim(),
            GrossMarginPercent = request.GrossMarginPercent > 0 ? request.GrossMarginPercent : 30.0m,
            SlippageScore = 0,
            RiskLevel = SlippageRiskLevel.LowHealthy
        };

        _deals[deal.DealId] = deal;
        return Task.FromResult(deal);
    }

    public Task<OpportunityDealRecord?> GetDealByIdAsync(Guid tenantId, Guid dealId, CancellationToken ct = default)
    {
        if (_deals.TryGetValue(dealId, out var deal) && deal.TenantId == tenantId)
        {
            return Task.FromResult<OpportunityDealRecord?>(deal);
        }
        return Task.FromResult<OpportunityDealRecord?>(null);
    }

    public Task<List<OpportunityDealRecord>> ListDealsAsync(Guid tenantId, OpportunityStage? stageFilter = null, CancellationToken ct = default)
    {
        var query = _deals.Values.Where(d => d.TenantId == tenantId);
        if (stageFilter.HasValue)
        {
            query = query.Where(d => d.Stage == stageFilter.Value);
        }
        var list = query.OrderByDescending(d => d.Amount).ToList();
        return Task.FromResult(list);
    }

    public Task<StageGateRule> DefineStageGateRuleAsync(DefineStageGateRuleRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));

        var rule = new StageGateRule(
            Guid.NewGuid(),
            request.TenantId,
            request.FromStage,
            request.ToStage,
            request.RequiredGateRequirements ?? new List<GateRequirementType>(),
            request.MinimumStageDurationDays,
            request.EnforceStrictBlock
        );

        _stageGateRules[rule.RuleId] = rule;
        return Task.FromResult(rule);
    }

    public Task<List<StageGateRule>> GetStageGateRulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _stageGateRules.Values
            .Where(r => r.TenantId == tenantId || r.TenantId == Guid.Empty)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<DealHealthAnalysis> AnalyzeDealHealthAsync(Guid tenantId, Guid dealId, CancellationToken ct = default)
    {
        if (!_deals.TryGetValue(dealId, out var deal) || deal.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Deal {dealId} not found.");
        }

        int score = 0;
        var riskFactors = new List<string>();

        // 1. Stage Velocity Penalty
        if (deal.DaysInCurrentStage > 30)
        {
            score += 35;
            riskFactors.Add($"Stagnant in {deal.Stage} for {deal.DaysInCurrentStage} days (>30d benchmark).");
        }
        else if (deal.DaysInCurrentStage > 15)
        {
            score += 15;
            riskFactors.Add($"Moderate dwell in current stage ({deal.DaysInCurrentStage} days).");
        }

        // 2. Close Date Pushes
        if (deal.CloseDatePushCount >= 3)
        {
            score += 35;
            riskFactors.Add($"Close date pushed {deal.CloseDatePushCount} times.");
        }
        else if (deal.CloseDatePushCount >= 1)
        {
            score += 15;
            riskFactors.Add($"Close date pushed {deal.CloseDatePushCount} times.");
        }

        // 3. Inactive Customer Contact
        var daysSinceContact = (DateTime.UtcNow - deal.LastCustomerContactAtUtc).TotalDays;
        if (daysSinceContact > 14)
        {
            score += 25;
            riskFactors.Add($"Zero customer interaction in {Math.Floor(daysSinceContact)} days.");
        }

        // 4. Single-Threaded Risk
        if (string.IsNullOrWhiteSpace(deal.EconomicBuyerName) && deal.Stage >= OpportunityStage.NeedsAnalysis)
        {
            score += 20;
            riskFactors.Add("Single-threaded risk: Economic Decision Maker unconfirmed.");
        }

        score = Math.Min(100, score);
        deal.SlippageScore = score;

        deal.RiskLevel = score switch
        {
            <= 25 => SlippageRiskLevel.LowHealthy,
            <= 50 => SlippageRiskLevel.ModerateWatch,
            <= 75 => SlippageRiskLevel.HighSlipping,
            _ => SlippageRiskLevel.CriticalStalled
        };

        // Base stage probability
        double baseProb = deal.Stage switch
        {
            OpportunityStage.Prospecting => 0.10,
            OpportunityStage.Qualification => 0.25,
            OpportunityStage.NeedsAnalysis => 0.40,
            OpportunityStage.ProposalQuote => 0.65,
            OpportunityStage.NegotiationReview => 0.85,
            OpportunityStage.ClosedWon => 1.0,
            OpportunityStage.ClosedLost => 0.0,
            _ => 0.20
        };

        // Discount probability based on slippage score
        double decayFactor = 1.0 - (score * 0.006); // 50 score -> 0.70x factor
        double decayedProb = Math.Max(0.05, Math.Round(baseProb * decayFactor, 2));
        decimal weightedAmount = Math.Round(deal.Amount * (decimal)decayedProb, 2);

        // Check next stage gate
        var nextStage = (OpportunityStage)((int)deal.Stage + 1);
        var missingGates = EvaluateMissingGates(tenantId, deal, nextStage);
        bool canAdvance = missingGates.Count == 0;

        return Task.FromResult(new DealHealthAnalysis(
            deal.DealId,
            deal.Title,
            deal.Stage,
            deal.Amount,
            score,
            deal.RiskLevel,
            decayedProb,
            weightedAmount,
            riskFactors,
            canAdvance,
            missingGates
        ));
    }

    public async Task<OpportunityDealRecord> AdvanceDealStageAsync(UpdateDealStageRequest request, CancellationToken ct = default)
    {
        if (!_deals.TryGetValue(request.DealId, out var deal) || deal.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Deal {request.DealId} not found.");
        }

        if (deal.Stage == OpportunityStage.ClosedWon || deal.Stage == OpportunityStage.ClosedLost)
        {
            throw new InvalidOperationException("Cannot advance a closed deal.");
        }

        // Validate Stage Gates
        var missingGates = EvaluateMissingGates(request.TenantId, deal, request.TargetStage);
        if (missingGates.Count > 0)
        {
            throw new InvalidOperationException($"Stage-Gate Governance Block: Cannot advance to {request.TargetStage}. Missing requirements: {string.Join(", ", missingGates)}.");
        }

        var oldStage = deal.Stage;
        deal.Stage = request.TargetStage;
        deal.DaysInCurrentStage = 0;
        deal.LastCustomerContactAtUtc = DateTime.UtcNow;

        deal.StageHistory.Add(new StageTransitionHistoryEntry(
            oldStage,
            request.TargetStage,
            DateTime.UtcNow,
            deal.DaysInCurrentStage,
            missingGates
        ));

        // Re-analyze
        await AnalyzeDealHealthAsync(request.TenantId, deal.DealId, ct);
        return deal;
    }

    public Task<OpportunityDealRecord> PushCloseDateAsync(PushCloseDateRequest request, CancellationToken ct = default)
    {
        if (!_deals.TryGetValue(request.DealId, out var deal) || deal.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Deal {request.DealId} not found.");
        }

        deal.CloseDateUtc = request.NewCloseDateUtc;
        deal.CloseDatePushCount++;

        return Task.FromResult(deal);
    }

    public async Task<PipelineSlippageSummary> GetPipelineSlippageSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenantDeals = _deals.Values.Where(d => d.TenantId == tenantId).ToList();

        var analyses = new List<DealHealthAnalysis>();
        foreach (var deal in tenantDeals)
        {
            analyses.Add(await AnalyzeDealHealthAsync(tenantId, deal.DealId, ct));
        }

        var totalAmount = tenantDeals.Sum(d => d.Amount);
        var totalWeighted = analyses.Sum(a => a.WeightedAmount);
        var atRiskAmount = analyses
            .Where(a => a.RiskLevel == SlippageRiskLevel.HighSlipping || a.RiskLevel == SlippageRiskLevel.CriticalStalled)
            .Sum(a => a.Amount);

        var slippedCount = analyses.Count(a => a.SlippageScore >= 50);
        double avgScore = analyses.Count > 0 ? analyses.Average(a => a.SlippageScore) : 0.0;

        return new PipelineSlippageSummary(
            tenantId,
            totalAmount,
            totalWeighted,
            atRiskAmount,
            tenantDeals.Count,
            slippedCount,
            Math.Round(avgScore, 1),
            analyses
        );
    }

    private List<GateRequirementType> EvaluateMissingGates(Guid tenantId, OpportunityDealRecord deal, OpportunityStage targetStage)
    {
        var rules = _stageGateRules.Values
            .Where(r => (r.TenantId == tenantId || r.TenantId == Guid.Empty) &&
                        r.FromStage == deal.Stage && r.ToStage == targetStage && r.EnforceStrictBlock)
            .ToList();

        var missing = new List<GateRequirementType>();

        foreach (var rule in rules)
        {
            foreach (var gate in rule.RequiredGateRequirements)
            {
                bool satisfied = gate switch
                {
                    GateRequirementType.DecisionMakerIdentified => !string.IsNullOrWhiteSpace(deal.EconomicBuyerName),
                    GateRequirementType.MarginThresholdMet => deal.GrossMarginPercent >= 20.0m,
                    GateRequirementType.SignedContractAttached => deal.HasSignedContract,
                    _ => true
                };

                if (!satisfied)
                {
                    missing.Add(gate);
                }
            }
        }

        return missing;
    }
}
