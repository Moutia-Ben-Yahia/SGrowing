using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Intelligence.AccountExpansion;

public interface IAccountExpansionEngine
{
    List<ExpansionOpportunity> GetOpportunities();
    ExpansionOpportunity? GetOpportunity(string opportunityId);
    ExpansionOpportunity GenerateQuote(GenerateExpansionQuoteRequest request);
    ExpansionOpportunity IngestUtilization(IngestUtilizationSignalRequest request);
}

public class AccountExpansionEngine : IAccountExpansionEngine
{
    private readonly ConcurrentDictionary<string, ExpansionOpportunity> _opportunities = new(StringComparer.OrdinalIgnoreCase);

    public AccountExpansionEngine()
    {
        // Seed high-propensity expansion opportunities
        var o1 = new ExpansionOpportunity(
            OpportunityId: "opp_exp_101",
            TenantId: "tenant_vertex_bio",
            AccountName: "Vertex BioPharma Labs",
            ExpansionPropensityScore: 94,
            CurrentArrUsd: 120000m,
            ProjectedExpansionArrUsd: 65000m,
            RecommendedTierOrAddon: "Global Enterprise Multi-Region Shard Mesh + 100 Agent Seats",
            SaturatedMetrics: new()
            {
                new("User Seats", 48, 50, 96.0),
                new("Monthly AI Copilot Tokens", 48000000, 50000000, 96.0)
            },
            IsQuoteGenerated: false
        );

        var o2 = new ExpansionOpportunity(
            OpportunityId: "opp_exp_102",
            TenantId: "tenant_nordic_fintech",
            AccountName: "Nordic Fintech AB",
            ExpansionPropensityScore: 88,
            CurrentArrUsd: 85000m,
            ProjectedExpansionArrUsd: 40000m,
            RecommendedTierOrAddon: "Dedicated Zero-Trust Ephemeral Vault & HSM Keyring",
            SaturatedMetrics: new()
            {
                new("Vault Rotations / Day", 920, 1000, 92.0)
            },
            IsQuoteGenerated: false
        );

        var o3 = new ExpansionOpportunity(
            OpportunityId: "opp_exp_103",
            TenantId: "tenant_omni_logistics",
            AccountName: "Omni Logistics Global",
            ExpansionPropensityScore: 78,
            CurrentArrUsd: 60000m,
            ProjectedExpansionArrUsd: 25000m,
            RecommendedTierOrAddon: "Omnichannel DPA Dynamic Product Catalog Syndication",
            SaturatedMetrics: new()
            {
                new("Catalog SKUs", 8500, 10000, 85.0)
            },
            IsQuoteGenerated: false
        );

        _opportunities[o1.OpportunityId] = o1;
        _opportunities[o2.OpportunityId] = o2;
        _opportunities[o3.OpportunityId] = o3;
    }

    public List<ExpansionOpportunity> GetOpportunities()
    {
        return _opportunities.Values
            .OrderByDescending(o => o.ExpansionPropensityScore)
            .ToList();
    }

    public ExpansionOpportunity? GetOpportunity(string opportunityId)
    {
        _opportunities.TryGetValue(opportunityId, out var opp);
        return opp;
    }

    public ExpansionOpportunity GenerateQuote(GenerateExpansionQuoteRequest request)
    {
        if (!_opportunities.TryGetValue(request.OpportunityId, out var existing))
        {
            throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");
        }

        decimal discount = Math.Clamp(request.DiscountPercentage, 0m, 30m);
        decimal discountedExpansion = Math.Round(existing.ProjectedExpansionArrUsd * (1m - (discount / 100m)), 2);

        var updated = existing with
        {
            ProjectedExpansionArrUsd = discountedExpansion,
            IsQuoteGenerated = true
        };

        _opportunities[request.OpportunityId] = updated;
        return updated;
    }

    public ExpansionOpportunity IngestUtilization(IngestUtilizationSignalRequest request)
    {
        var opp = _opportunities.Values.FirstOrDefault(o => o.TenantId.Equals(request.TenantId, StringComparison.OrdinalIgnoreCase));
        if (opp is null)
        {
            opp = new ExpansionOpportunity(
                OpportunityId: $"opp_exp_{DateTime.UtcNow.Ticks % 10000}",
                TenantId: request.TenantId,
                AccountName: $"Enterprise {request.TenantId}",
                ExpansionPropensityScore: 80,
                CurrentArrUsd: 50000m,
                ProjectedExpansionArrUsd: 20000m,
                RecommendedTierOrAddon: "Capacity Burst Tier Expansion",
                SaturatedMetrics: new() { new(request.MetricName, request.UnitsAdded, 1000, 95.0) },
                IsQuoteGenerated: false
            );
            _opportunities[opp.OpportunityId] = opp;
            return opp;
        }

        var metrics = opp.SaturatedMetrics.Select(m =>
        {
            if (m.MetricName.Equals(request.MetricName, StringComparison.OrdinalIgnoreCase))
            {
                int newUsed = m.CurrentUsed + request.UnitsAdded;
                double pct = Math.Round((double)newUsed / m.AllocatedLimit * 100.0, 1);
                return m with { CurrentUsed = newUsed, UtilizationPct = pct };
            }
            return m;
        }).ToList();

        var updated = opp with
        {
            SaturatedMetrics = metrics,
            ExpansionPropensityScore = Math.Min(99, opp.ExpansionPropensityScore + 4)
        };

        _opportunities[opp.OpportunityId] = updated;
        return updated;
    }
}
