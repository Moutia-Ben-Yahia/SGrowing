using FluentAssertions;
using SGrowing.Api.Modules.CRM.Pipeline;
using Xunit;

namespace SGrowing.Tests.CRM;

public class DealSlippageRadarServiceTests
{
    private readonly DealSlippageRadarService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task CreateDealAsync_ShouldInitializeDealWithHealthyBaseline()
    {
        var req = new CreateDealRequest(
            _tenantId,
            "Enterprise CRM License (100 Seats)",
            "Acme Corp",
            120_000m,
            DateTime.UtcNow.AddDays(45),
            "Sarah Closer",
            "John Doe CFO",
            GrossMarginPercent: 35.0m
        );

        var deal = await _service.CreateDealAsync(req);

        deal.Should().NotBeNull();
        deal.DealId.Should().NotBeEmpty();
        deal.Amount.Should().Be(120_000m);
        deal.Stage.Should().Be(OpportunityStage.Prospecting);
        deal.CloseDatePushCount.Should().Be(0);
        deal.SlippageScore.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeDealHealthAsync_ShouldDetectSlippage_WhenStagnantAndPushed()
    {
        var deal = await _service.CreateDealAsync(new CreateDealRequest(
            _tenantId, "Stalled Cloud Deal", "Slow Enterprise", 50_000m, DateTime.UtcNow.AddDays(10), "Sarah"
        ));

        // Simulate stagnancy and close date push
        deal.DaysInCurrentStage = 35; // >30 days adds 35 pts
        deal.CloseDatePushCount = 3;  // >=3 pushes adds 35 pts
        deal.LastCustomerContactAtUtc = DateTime.UtcNow.AddDays(-20); // >14 days adds 25 pts

        var analysis = await _service.AnalyzeDealHealthAsync(_tenantId, deal.DealId);

        analysis.SlippageScore.Should().BeGreaterThanOrEqualTo(75);
        analysis.RiskLevel.Should().Be(SlippageRiskLevel.CriticalStalled);
        analysis.RiskFactors.Should().HaveCountGreaterThanOrEqualTo(3);
        analysis.DecayedWinProbability.Should().BeLessThan(0.10);
    }

    [Fact]
    public async Task AdvanceDealStageAsync_ShouldBlockAdvancement_WhenDecisionMakerMissing()
    {
        var deal = await _service.CreateDealAsync(new CreateDealRequest(
            _tenantId, "Needs Proposal", "Target Account", 80_000m, DateTime.UtcNow.AddDays(20), "Rep", EconomicBuyerName: null
        ));
        deal.Stage = OpportunityStage.NeedsAnalysis;

        // Try to advance to ProposalQuote without an economic buyer
        var req = new UpdateDealStageRequest(_tenantId, deal.DealId, OpportunityStage.ProposalQuote);

        var act = () => _service.AdvanceDealStageAsync(req);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DecisionMakerIdentified*");
    }

    [Fact]
    public async Task AdvanceDealStageAsync_ShouldBlockAdvancement_WhenMarginThresholdNotMet()
    {
        var deal = await _service.CreateDealAsync(new CreateDealRequest(
            _tenantId, "Discounted Quote", "Price Sensitive Client", 60_000m, DateTime.UtcNow.AddDays(30), "Rep",
            EconomicBuyerName: "CFO Jane", GrossMarginPercent: 12.0m // < 20%
        ));
        deal.Stage = OpportunityStage.ProposalQuote;

        // Try to advance to NegotiationReview
        var req = new UpdateDealStageRequest(_tenantId, deal.DealId, OpportunityStage.NegotiationReview);

        var act = () => _service.AdvanceDealStageAsync(req);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MarginThresholdMet*");
    }

    [Fact]
    public async Task AdvanceDealStageAsync_ShouldBlockClosedWon_WithoutSignedContract()
    {
        var deal = await _service.CreateDealAsync(new CreateDealRequest(
            _tenantId, "Final Stage Deal", "Client", 100_000m, DateTime.UtcNow.AddDays(5), "Rep",
            EconomicBuyerName: "CEO Jack", GrossMarginPercent: 28.0m
        ));
        deal.Stage = OpportunityStage.NegotiationReview;
        deal.HasSignedContract = false;

        var req = new UpdateDealStageRequest(_tenantId, deal.DealId, OpportunityStage.ClosedWon);

        var act = () => _service.AdvanceDealStageAsync(req);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SignedContractAttached*");
    }

    [Fact]
    public async Task AdvanceDealStageAsync_ShouldSucceed_WhenAllGatesAreSatisfied()
    {
        var deal = await _service.CreateDealAsync(new CreateDealRequest(
            _tenantId, "Ready Deal", "Client", 100_000m, DateTime.UtcNow.AddDays(5), "Rep",
            EconomicBuyerName: "CEO Jack", GrossMarginPercent: 28.0m
        ));
        deal.Stage = OpportunityStage.NegotiationReview;
        deal.HasSignedContract = true; // Satisfied!

        var req = new UpdateDealStageRequest(_tenantId, deal.DealId, OpportunityStage.ClosedWon);
        var updated = await _service.AdvanceDealStageAsync(req);

        updated.Stage.Should().Be(OpportunityStage.ClosedWon);
        updated.StageHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task PushCloseDateAsync_ShouldIncrementPushCounter()
    {
        var deal = await _service.CreateDealAsync(new CreateDealRequest(
            _tenantId, "Pushed Deal", "Client", 40_000m, DateTime.UtcNow.AddDays(10), "Rep"
        ));

        var req = new PushCloseDateRequest(_tenantId, deal.DealId, DateTime.UtcNow.AddDays(30), "Budget cycle delayed to Q3");
        var updated = await _service.PushCloseDateAsync(req);

        updated.CloseDatePushCount.Should().Be(1);
        updated.CloseDateUtc.Should().BeAfter(DateTime.UtcNow.AddDays(25));
    }

    [Fact]
    public async Task GetPipelineSlippageSummaryAsync_ShouldAggregateMetrics()
    {
        var d1 = await _service.CreateDealAsync(new CreateDealRequest(_tenantId, "Deal A", "Acc A", 100_000m, DateTime.UtcNow.AddDays(15), "Rep"));
        var d2 = await _service.CreateDealAsync(new CreateDealRequest(_tenantId, "Deal B", "Acc B", 200_000m, DateTime.UtcNow.AddDays(15), "Rep"));

        d2.DaysInCurrentStage = 40; // Stagnant

        var summary = await _service.GetPipelineSlippageSummaryAsync(_tenantId);

        summary.TotalPipelineAmount.Should().Be(300_000m);
        summary.TotalDealsCount.Should().Be(2);
        summary.AnalyzedDeals.Should().HaveCount(2);
    }
}
