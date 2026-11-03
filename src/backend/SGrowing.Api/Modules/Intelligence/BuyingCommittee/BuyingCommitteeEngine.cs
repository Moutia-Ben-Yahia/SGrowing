using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Intelligence.BuyingCommittee;

public interface IBuyingCommitteeEngine
{
    BuyingCommitteeMap MapBuyingCommittee(MapCommitteeRequest request);
    BuyingCommitteeMap? GetCommitteeByDomain(string companyDomain);
    List<RecommendedPlaybook> GeneratePlaybooks(PersonaRole role);
}

public class BuyingCommitteeEngine : IBuyingCommitteeEngine
{
    private readonly ConcurrentDictionary<string, BuyingCommitteeMap> _cache = new(StringComparer.OrdinalIgnoreCase);

    public BuyingCommitteeEngine()
    {
        // Seed initial enterprise account
        var initialMembers = new List<CommitteeMember>
        {
            new("mem_1", "Sarah Jenkins", "VP of Sales Operations", "sjenkins@acmecorp.com", "https://linkedin.com/in/sjenkins", PersonaRole.Champion, 92.5, true),
            new("mem_2", "Michael Vance", "Chief Financial Officer", "mvance@acmecorp.com", "https://linkedin.com/in/mvance", PersonaRole.EconomicBuyer, 88.0, true),
            new("mem_3", "David Chen", "Head of Enterprise Architecture", "dchen@acmecorp.com", "https://linkedin.com/in/dchen", PersonaRole.TechnicalEvaluator, 76.0, false)
        };

        MapBuyingCommittee(new MapCommitteeRequest("acmecorp.com", "Acme Corporation", initialMembers));
    }

    public BuyingCommitteeMap MapBuyingCommittee(MapCommitteeRequest request)
    {
        var members = request.CandidateMembers ?? new List<CommitteeMember>();
        var presentRoles = members.Select(m => m.Role).ToHashSet();

        var gaps = new List<PersonaGapAlert>();

        if (!presentRoles.Contains(PersonaRole.EconomicBuyer))
        {
            gaps.Add(new PersonaGapAlert(PersonaRole.EconomicBuyer, "Critical", "No Economic Buyer identified. Budget authorization blocked."));
        }

        if (!presentRoles.Contains(PersonaRole.SecurityCiso))
        {
            gaps.Add(new PersonaGapAlert(PersonaRole.SecurityCiso, "High", "Missing CISO / InfoSec signoff for enterprise data ingress."));
        }

        if (!presentRoles.Contains(PersonaRole.LegalProcurement))
        {
            gaps.Add(new PersonaGapAlert(PersonaRole.LegalProcurement, "Medium", "Procurement contact missing. MSA redlining may be delayed."));
        }

        // Consensus score calculation
        double consensusPercentage = 0.0;
        if (members.Count > 0)
        {
            var approvedCount = members.Count(m => m.HasApproved);
            var totalEngagement = members.Average(m => m.EngagementScore);
            consensusPercentage = Math.Round(((approvedCount / (double)members.Count) * 60.0) + (totalEngagement * 0.4), 1);
        }

        var readiness = DealReadiness.HighRiskIncomplete;
        if (consensusPercentage >= 80.0 && gaps.Count == 0)
        {
            readiness = DealReadiness.ConsensusReadyToClose;
        }
        else if (consensusPercentage >= 50.0)
        {
            readiness = DealReadiness.ModerateProgress;
        }

        var playbooks = new List<RecommendedPlaybook>();
        foreach (var member in members)
        {
            playbooks.AddRange(GeneratePlaybooks(member.Role));
        }

        var result = new BuyingCommitteeMap(
            request.CompanyDomain,
            request.CompanyName,
            members,
            consensusPercentage,
            readiness,
            gaps,
            playbooks.DistinctBy(p => p.SuggestedChannel + p.CollateralTitle).ToList()
        );

        _cache[request.CompanyDomain] = result;
        return result;
    }

    public BuyingCommitteeMap? GetCommitteeByDomain(string companyDomain)
    {
        _cache.TryGetValue(companyDomain, out var map);
        return map;
    }

    public List<RecommendedPlaybook> GeneratePlaybooks(PersonaRole role)
    {
        return role switch
        {
            PersonaRole.Champion => new List<RecommendedPlaybook>
            {
                new(role, "Internal Business Case Builder & ROI Deck", "Slack / Email", "Highlight 3.4x productivity multiplier and automated board reporting.")
            },
            PersonaRole.EconomicBuyer => new List<RecommendedPlaybook>
            {
                new(role, "3-Year Total Cost of Ownership (TCO) Calculator", "Executive Email", "Emphasize payback within 4.2 months and 28% annual headcount savings.")
            },
            PersonaRole.TechnicalEvaluator => new List<RecommendedPlaybook>
            {
                new(role, "Enterprise Architecture Specs & OpenAPI 3.1 Schemas", "Technical Briefing", "Focus on sub-5ms caching, zero data leakage, and sovereign VPC deployment.")
            },
            PersonaRole.SecurityCiso => new List<RecommendedPlaybook>
            {
                new(role, "SOC2 Type II & ISO 27001 Compliance Package", "Security Portal", "Detail zero-trust RBAC, TLS 1.3 in-transit, and AES-256 at-rest encryption.")
            },
            PersonaRole.LegalProcurement => new List<RecommendedPlaybook>
            {
                new(role, "Standard Enterprise SaaS Agreement & DPA", "Procurement Email", "Pre-signed GDPR/CCPA Data Processing Addendum with mutual indemnification.")
            },
            _ => new List<RecommendedPlaybook>
            {
                new(role, "End-User Interactive Sandbox Walkthrough", "In-App Guide", "Frictionless self-service workflow creation in under 90 seconds.")
            }
        };
    }
}
