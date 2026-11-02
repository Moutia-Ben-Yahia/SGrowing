using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.DealCopilot;

public interface IDealCopilotEngine
{
    List<PipelineDeal> GetPipelineDeals(string tenantId);
    PipelineDeal? GetDeal(string dealId);
    InterventionActionResult ExecuteIntervention(InterventionActionRequest request);
    double CalculateAggregatePipelineHealth(string tenantId);
}

public class DealCopilotEngine : IDealCopilotEngine
{
    private readonly ConcurrentDictionary<string, PipelineDeal> _deals = new();

    public DealCopilotEngine()
    {
        SeedDefaultDeals();
    }

    private void SeedDefaultDeals()
    {
        var d1 = new PipelineDeal(
            DealId: "deal_quantum_corp",
            AccountName: "Quantum Logistics International",
            Amount: 185000.0,
            Stage: "Negotiation",
            DaysInactive: 12,
            WinProbabilityPercent: 45.0,
            RiskLevel: "SevereRisk",
            RiskFactors: new List<DealRiskFactor>
            {
                new("Decision-Maker Ghosting", DealRiskSeverity.High, "No email reply or calendar response in 12 days"),
                new("Competitor Evaluation", DealRiskSeverity.Medium, "HubSpot Enterprise cited during last demo")
            },
            PrescribedAction: "Draft multi-agent executive re-engagement memo highlighting air-gapped security advantages",
            AssignedAe: "Jessica Taylor"
        );

        var d2 = new PipelineDeal(
            DealId: "deal_acme_retail",
            AccountName: "Acme Global Retail Systems",
            Amount: 94000.0,
            Stage: "Proposal",
            DaysInactive: 3,
            WinProbabilityPercent: 82.0,
            RiskLevel: "Healthy",
            RiskFactors: new List<DealRiskFactor>
            {
                new("Security Signoff Pending", DealRiskSeverity.Low, "SOC2 Type II documentation requested")
            },
            PrescribedAction: "Transmit verified cryptographic PKCS#7 security packet",
            AssignedAe: "Marcus Vance"
        );

        var d3 = new PipelineDeal(
            DealId: "deal_fintech_hyper",
            AccountName: "HyperPay Global Remittances",
            Amount: 320000.0,
            Stage: "Demo",
            DaysInactive: 7,
            WinProbabilityPercent: 60.0,
            RiskLevel: "ModerateRisk",
            RiskFactors: new List<DealRiskFactor>
            {
                new("Single-Threaded Champion", DealRiskSeverity.High, "Only engaging with VP Engineering, finance VP not involved")
            },
            PrescribedAction: "Schedule multi-stakeholder economic impact ROI session",
            AssignedAe: "Jessica Taylor"
        );

        _deals[d1.DealId] = d1;
        _deals[d2.DealId] = d2;
        _deals[d3.DealId] = d3;
    }

    public List<PipelineDeal> GetPipelineDeals(string tenantId)
    {
        return _deals.Values.OrderByDescending(d => d.Amount).ToList();
    }

    public PipelineDeal? GetDeal(string dealId)
    {
        _deals.TryGetValue(dealId, out var deal);
        return deal;
    }

    public InterventionActionResult ExecuteIntervention(InterventionActionRequest request)
    {
        if (!_deals.TryGetValue(request.DealId, out var deal))
        {
            throw new KeyNotFoundException($"Deal '{request.DealId}' not found.");
        }

        var updatedProbability = Math.Min(95.0, deal.WinProbabilityPercent + 20.0);
        var updated = deal with
        {
            WinProbabilityPercent = updatedProbability,
            RiskLevel = "Healthy",
            DaysInactive = 0
        };

        _deals[request.DealId] = updated;

        return new InterventionActionResult(
            DealId: request.DealId,
            Success: true,
            NewWinProbabilityPercent: updatedProbability,
            ExecutionMessage: $"AI Copilot executed '{request.ActionType}' for {deal.AccountName}. Win probability increased to {updatedProbability}%."
        );
    }

    public double CalculateAggregatePipelineHealth(string tenantId)
    {
        var deals = GetPipelineDeals(tenantId);
        if (!deals.Any()) return 100.0;
        return Math.Round(deals.Average(d => d.WinProbabilityPercent), 1);
    }
}
