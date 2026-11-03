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
    public void GeneratePlaybooks_ReturnsRoleSpecificGuidance()
    {
        var cisoPlaybooks = _engine.GeneratePlaybooks(PersonaRole.SecurityCiso);

        Assert.NotEmpty(cisoPlaybooks);
        Assert.Contains(cisoPlaybooks, p => p.CollateralTitle.Contains("SOC2"));
    }
}
