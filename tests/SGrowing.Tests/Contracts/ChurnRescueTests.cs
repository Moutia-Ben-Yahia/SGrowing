using SGrowing.Api.Modules.Contracts.ChurnRescue;
using Xunit;

namespace SGrowing.Tests.Contracts;

public sealed class ChurnRescueTests
{
    private readonly ChurnRescueEngine _engine = new();

    [Fact]
    public async Task GetRiskRadar_CalculatesPropensityScoreAndSortsBySeverity()
    {
        var radar = await _engine.GetRiskRadarAsync();

        Assert.NotNull(radar);
        Assert.NotEmpty(radar);
        Assert.Contains(radar, r => r.AccountId == "acc_globex_corp");

        var highestRisk = radar.First();
        Assert.True(highestRisk.ChurnPropensityScore >= 50);
        Assert.NotEmpty(highestRisk.PrimaryRiskDrivers);
        Assert.NotNull(highestRisk.RecommendedPlaybook);
    }

    [Fact]
    public async Task AssessAccountRisk_IdentifiesSpecificRiskDrivers()
    {
        var assessment = await _engine.AssessAccountRiskAsync("acc_globex_corp");

        Assert.NotNull(assessment);
        Assert.Equal("Globex Logistics Systems", assessment.AccountName);
        Assert.Equal(124000m, assessment.AnnualRecurringRevenue);
        Assert.Contains(assessment.PrimaryRiskDrivers, d => d.Contains("Usage declined"));
        Assert.Contains(assessment.PrimaryRiskDrivers, d => d.Contains("champion"));
    }

    [Fact]
    public async Task ExecutePlaybook_GeneratesEscrowAgreementUrlAndActionSummary()
    {
        var request = new TriggerPlaybookRequest(
            AccountId: "acc_globex_corp",
            PlaybookType: "DiscountLock",
            CustomDiscountPercent: 20
        );

        var result = await _engine.ExecutePlaybookAsync(request);

        Assert.NotNull(result);
        Assert.Equal("acc_globex_corp", result.AccountId);
        Assert.Equal("DiscountLock", result.PlaybookType);
        Assert.Contains("20% 2-year price-lock", result.ActionSummary);
        Assert.Contains("escrow_acc_globex_corp", result.EscrowAgreementUrl);
    }

    [Fact]
    public async Task GenerateRenewalAgreement_CalculatesDiscountAndPriceLock()
    {
        var request = new GenerateRenewalAgreementRequest(
            AccountId: "acc_globex_corp",
            AnnualContractValue: 100000m,
            DiscountPercent: 15,
            LockYears: 2
        );

        var agreement = await _engine.GenerateRenewalAgreementAsync(request);

        Assert.NotNull(agreement);
        Assert.Equal(85000m, agreement.AnnualContractValue); // 100,000 * 0.85
        Assert.Equal(15, agreement.DiscountAppliedPercent);
        Assert.Equal(2, agreement.PriceLockYears);
        Assert.Equal("PendingSignature", agreement.SignatureStatus);
    }
}
