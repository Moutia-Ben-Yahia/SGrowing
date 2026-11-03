using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.DealAutopsy;

public interface IDealAutopsyEngine
{
    DealAutopsyOverview GetOverview();
    DealAutopsyRecord AnalyzeLostDeal(string opportunityId, string accountName, double dealValue, string salesRepName, string notesOrTranscripts);
    WinRateRecoveryPlaybook GenerateRecoveryPlaybook(string opportunityId, LossReasonCategory category);
}

public sealed class DealAutopsyEngine : IDealAutopsyEngine
{
    private readonly ConcurrentDictionary<string, DealAutopsyRecord> _autopsies = new();
    private readonly ConcurrentDictionary<string, WinRateRecoveryPlaybook> _playbooks = new();

    public DealAutopsyEngine()
    {
        SeedInitialState();
    }

    private void SeedInitialState()
    {
        var signals1 = new List<LossFactorSignal>
        {
            new("SIG-001", "Pricing Concession Refusal", LossReasonCategory.PricingAndTcoMisalignment, 0.85, "Buyer stated competitor offered 40% upfront multi-year discount."),
            new("SIG-002", "Security Review Latency", LossReasonCategory.SecurityAndComplianceBlocker, 0.65, "SOC2 Type II trust package review delayed legal by 6 weeks.")
        };

        var auto1 = new DealAutopsyRecord(
            "AUTO-001",
            "OPP-FIN-8812",
            "Metropolitan Capital Union",
            145000.0,
            "Alexandra Hayes",
            LossReasonCategory.PricingAndTcoMisalignment,
            0.94,
            signals1,
            DateTime.UtcNow.AddDays(-6)
        );
        _autopsies.TryAdd(auto1.AutopsyId, auto1);

        var play1 = new WinRateRecoveryPlaybook(
            "PLAY-001",
            auto1.OpportunityId,
            "Milestone-Gated Payback Guarantee & Executive Re-briefing",
            45,
            "Quantified cashflow break-even analysis within 90 days with deferred invoicing.",
            0.68
        );
        _playbooks.TryAdd(play1.PlaybookId, play1);
    }

    public DealAutopsyOverview GetOverview()
    {
        var autopsies = _autopsies.Values.OrderByDescending(a => a.ClosedLostAtUtc).Take(10).ToList();
        var playbooks = _playbooks.Values.Take(10).ToList();

        return new DealAutopsyOverview(
            TotalAutopsiesCompletedQuarter: 48,
            TotalLostPipelineValueUsd: 6240000.0,
            WinRateRecoveryPotentialPct: 34.2,
            RecentAutopsies: autopsies,
            ActivePlaybooks: playbooks
        );
    }

    public DealAutopsyRecord AnalyzeLostDeal(string opportunityId, string accountName, double dealValue, string salesRepName, string notesOrTranscripts)
    {
        var lower = notesOrTranscripts.ToLowerInvariant();
        LossReasonCategory primary;
        string signalName;
        string excerpt;

        if (lower.Contains("price") || lower.Contains("budget") || lower.Contains("cost") || lower.Contains("expensive"))
        {
            primary = LossReasonCategory.PricingAndTcoMisalignment;
            signalName = "Budget & Pricing Pushback";
            excerpt = "Customer leadership cited CFO capex restrictions and pricing misalignment.";
        }
        else if (lower.Contains("competitor") || lower.Contains("salesforce") || lower.Contains("hubspot"))
        {
            primary = LossReasonCategory.CompetitorDisplacement;
            signalName = "Incumbent Vendor Lock-in";
            excerpt = "Account decided to remain with existing vendor ecosystem.";
        }
        else if (lower.Contains("security") || lower.Contains("compliance") || lower.Contains("legal"))
        {
            primary = LossReasonCategory.SecurityAndComplianceBlocker;
            signalName = "InfoSec & Compliance Roadblock";
            excerpt = "InfoSec committee rejected multi-tenant public cloud data tier.";
        }
        else
        {
            primary = LossReasonCategory.BudgetFreezeOrNoDecision;
            signalName = "Project Indefinitely Shelved";
            excerpt = "Executive sponsor left organization leading to deal stagnation.";
        }

        var signals = new List<LossFactorSignal>
        {
            new($"SIG-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", signalName, primary, 0.90, excerpt)
        };

        var record = new DealAutopsyRecord(
            $"AUTO-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            opportunityId,
            accountName,
            dealValue,
            salesRepName,
            primary,
            0.92,
            signals,
            DateTime.UtcNow
        );

        _autopsies.TryAdd(record.AutopsyId, record);
        return record;
    }

    public WinRateRecoveryPlaybook GenerateRecoveryPlaybook(string opportunityId, LossReasonCategory category)
    {
        string strategy = category switch
        {
            LossReasonCategory.PricingAndTcoMisalignment => "Introduce Consumption-Based Tier with 0 Upfront Capex",
            LossReasonCategory.SecurityAndComplianceBlocker => "Provide Dedicated Sovereign Cluster Attestation & FedRAMP Alignment",
            LossReasonCategory.CompetitorDisplacement => "Deploy Competitive Replacement Matrix with Migration Subsidy",
            _ => "Executive Sponsor CAB Invitation & 90-Day Resurfacing Checkpoint"
        };

        var playbook = new WinRateRecoveryPlaybook(
            $"PLAY-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            opportunityId,
            strategy,
            ReEngagementCooldownDays: 60,
            ProposedValueAngle: "Demonstrate immediate cash efficiency and autonomous operational ROI.",
            ProbabilityOfPipelineRevival: 0.65
        );

        _playbooks.TryAdd(playbook.PlaybookId, playbook);
        return playbook;
    }
}
