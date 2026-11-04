using System.Collections.Generic;
using SGrowing.Api.Modules.Intelligence.BuyingCommittee;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class BuyingCommitteeTests
{
    private readonly BuyingCommitteeEngine _engine = new();

    [Fact]
    public void SeededAccount_ReturnsValidHierarchyAndGaps()
    {
        var result = _engine.GetCommitteeByDomain("acmecorp.com");

        Assert.NotNull(result);
        Assert.Equal("Acme Corporation", result.CompanyName);
        Assert.Equal(3, result.Members.Count);
        Assert.True(result.ConsensusPercentage > 50.0);
        Assert.Contains(result.IdentifiedGaps, g => g.MissingRole == PersonaRole.SecurityCiso);
    }

    [Fact]
    public void FullCommitteeWithAllApprovals_ReachesConsensusReadyToClose()
    {
        var fullMembers = new List<CommitteeMember>
        {
            new("1", "Alice", "VP Sales", "a@test.com", "link", PersonaRole.Champion, 95.0, true),
            new("2", "Bob", "CFO", "b@test.com", "link", PersonaRole.EconomicBuyer, 90.0, true),
            new("3", "Charlie", "CTO", "c@test.com", "link", PersonaRole.TechnicalEvaluator, 92.0, true),
            new("4", "Diana", "CISO", "d@test.com", "link", PersonaRole.SecurityCiso, 88.0, true),
            new("5", "Evan", "Legal Counsel", "e@test.com", "link", PersonaRole.LegalProcurement, 85.0, true)
        };

        var request = new MapCommitteeRequest("enterprise.io", "Enterprise IO", fullMembers);
        var result = _engine.MapBuyingCommittee(request);

        Assert.Equal(DealReadiness.ConsensusReadyToClose, result.Readiness);
        Assert.Empty(result.IdentifiedGaps);
        Assert.True(result.ConsensusPercentage >= 80.0);
    }

    [Fact]
    public void MapBuyingCommittee_IdentifiesCriticalAndHighGaps()
    {
        // Only Champion present (Missing EconomicBuyer, SecurityCiso, LegalProcurement)
        var members = new List<CommitteeMember>
        {
            new("1", "Sarah", "Sales Lead", "s@test.com", "link", PersonaRole.Champion, 80.0, true)
        };

        var result = _engine.MapBuyingCommittee(new MapCommitteeRequest("startup.com", "Startup Inc", members));

        Assert.Equal(3, result.IdentifiedGaps.Count);
        Assert.Contains(result.IdentifiedGaps, g => g.MissingRole == PersonaRole.EconomicBuyer && g.RiskLevel == "Critical");
        Assert.Contains(result.IdentifiedGaps, g => g.MissingRole == PersonaRole.SecurityCiso && g.RiskLevel == "High");
        Assert.Contains(result.IdentifiedGaps, g => g.MissingRole == PersonaRole.LegalProcurement && g.RiskLevel == "Medium");
    }

    [Fact]
    public void MapBuyingCommittee_WithZeroMembers_ReturnsHighRiskIncomplete()
    {
        var result = _engine.MapBuyingCommittee(new MapCommitteeRequest("empty.com", "Empty Corp", new List<CommitteeMember>()));

        Assert.Equal(DealReadiness.HighRiskIncomplete, result.Readiness);
        Assert.Equal(0.0, result.ConsensusPercentage);
    }

    [Fact]
    public void GetCommitteeByDomain_ReturnsNullForUnmappedDomain()
    {
        var result = _engine.GetCommitteeByDomain("unmapped-domain-99.org");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(PersonaRole.Champion, "ROI Deck")]
    [InlineData(PersonaRole.EconomicBuyer, "TCO")]
    [InlineData(PersonaRole.TechnicalEvaluator, "OpenAPI")]
    [InlineData(PersonaRole.SecurityCiso, "SOC2")]
    [InlineData(PersonaRole.LegalProcurement, "DPA")]
    public void GeneratePlaybooks_ReturnsRoleSpecificGuidance(PersonaRole role, string expectedTitleSubstring)
    {
        var playbooks = _engine.GeneratePlaybooks(role);

        Assert.NotEmpty(playbooks);
        Assert.Contains(playbooks, p => p.CollateralTitle.Contains(expectedTitleSubstring));
    }
}
