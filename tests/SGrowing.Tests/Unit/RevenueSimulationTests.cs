using FluentAssertions;
using SGrowing.Api.Modules.Analytics.RevenueSimulation;
using Xunit;

namespace SGrowing.Tests.Unit;

public class RevenueSimulationTests
{
    private readonly RevenueSimulationService _service;

    public RevenueSimulationTests()
    {
        _service = new RevenueSimulationService();
    }

    [Fact]
    public void GetOpportunities_DefaultTenant_ReturnsSeededEnterpriseDeals()
    {
        // Act
        var deals = _service.GetOpportunities(RevenueSimulationService.DefaultTenantId);

        // Assert
        deals.Should().NotBeNull();
        deals.Count.Should().Be(10);
        deals.Should().Contain(d => d.DealName == "Acme Corp Global ERP Modernization");
        deals.Should().Contain(d => d.DealName == "CyberShield Threat Hunting Expansion" && d.Stage == PipelineStage.ClosedWon);
        deals.Sum(d => d.Amount).Should().Be(3560000);
    }

    [Fact]
    public void GetOpportunities_NonDefaultTenant_ReturnsEmptyInitially()
    {
        // Arrange
        var customTenantId = Guid.NewGuid();

        // Act
        var deals = _service.GetOpportunities(customTenantId);

        // Assert
        deals.Should().BeEmpty();
    }

    [Fact]
    public void AddOpportunity_AddsDealSuccessfully_IsRetrievableWithCalculatedBounds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new AddOpportunityInputRequest(
            DealName: "Cloud Infrastructure Upsell",
            AccountName: "SaaS Dynamics Corp",
            Amount: 100000,
            Stage: PipelineStage.ProposalPresented,
            Category: ForecastCategory.BestCase,
            WinProbability: 0.70,
            TargetCloseDate: DateTime.UtcNow.AddDays(30),
            DealAgeDays: 40,
            MinEstimatedValue: 90000,
            MostLikelyEstimatedValue: 100000,
            MaxEstimatedValue: 120000,
            Distribution: SimulationDistributionModel.Triangular
        );

        // Act
        var added = _service.AddOpportunity(tenantId, request);
        var deals = _service.GetOpportunities(tenantId);

        // Assert
        added.Should().NotBeNull();
        added.Id.Should().StartWith("opp-");
        added.Amount.Should().Be(100000);
        added.MinEstimatedValue.Should().Be(90000);
        added.MaxEstimatedValue.Should().Be(120000);
        deals.Should().ContainSingle(d => d.Id == added.Id);
    }

    [Fact]
    public void RunSimulation_DefaultTenant_CalculatesAccuratePercentilesP10P50P90()
    {
        // Arrange
        var req = new RunMonteCarloSimulationRequest(
            Iterations: 2000,
            QuotaTarget: 1500000,
            DefaultDistribution: SimulationDistributionModel.Triangular,
            MarketVolatilityIndex: 1.0
        );

        // Act
        var result = _service.RunSimulation(RevenueSimulationService.DefaultTenantId, req);

        // Assert
        result.Should().NotBeNull();
        result.Iterations.Should().Be(2000);
        result.P10.Should().BeGreaterThan(0);
        result.P50.Should().BeGreaterThan(result.P10);
        result.P90.Should().BeGreaterThan(result.P50);
        result.Mean.Should().BeInRange(result.P10, result.P90);
        result.StandardDeviation.Should().BeGreaterThan(0);
        result.ExpectedValue.Should().BeGreaterThan(0);
        result.PipelineTotalValue.Should().Be(3560000);
    }

    [Fact]
    public void RunSimulation_CustomOpportunities_OverridesTenantDeals()
    {
        // Arrange
        var customDeals = new List<OpportunitySimulationInput>
        {
            new(
                Id: "custom-1",
                DealName: "Guaranteed Deal",
                AccountName: "Acme Secure",
                Amount: 50000,
                Stage: PipelineStage.ClosedWon,
                Category: ForecastCategory.Closed,
                WinProbability: 1.0,
                TargetCloseDate: DateTime.UtcNow,
                DealAgeDays: 10,
                SlippageRiskScore: 0.0,
                HistoricalStageConversionRate: 1.0,
                MinEstimatedValue: 50000,
                MostLikelyEstimatedValue: 50000,
                MaxEstimatedValue: 50000,
                Distribution: SimulationDistributionModel.Triangular
            )
        };

        var req = new RunMonteCarloSimulationRequest(
            Iterations: 500,
            QuotaTarget: 40000,
            CustomOpportunities: customDeals
        );

        // Act
        var result = _service.RunSimulation(RevenueSimulationService.DefaultTenantId, req);

        // Assert
        result.PipelineTotalValue.Should().Be(50000);
        result.P10.Should().Be(50000);
        result.P50.Should().Be(50000);
        result.P90.Should().Be(50000);
        result.QuotaAttainmentProbability.Should().Be(100.0);
    }

    [Fact]
    public void RunSimulation_QuotaAttainment_ComputesProbabilityCorrectly()
    {
        // Act - Low quota (virtually 100% attainment)
        var lowQuotaRun = _service.RunSimulation(RevenueSimulationService.DefaultTenantId, new RunMonteCarloSimulationRequest(
            Iterations: 1000,
            QuotaTarget: 100000 // CyberShield ClosedWon alone is $140k
        ));

        // Act - Unattainable quota (0% attainment)
        var highQuotaRun = _service.RunSimulation(RevenueSimulationService.DefaultTenantId, new RunMonteCarloSimulationRequest(
            Iterations: 1000,
            QuotaTarget: 50000000 // $50M when pipeline is $3.56M
        ));

        // Assert
        lowQuotaRun.QuotaAttainmentProbability.Should().Be(100.0);
        highQuotaRun.QuotaAttainmentProbability.Should().Be(0.0);
    }

    [Fact]
    public void RunSimulation_GeneratesHistogramBinsAndPercentiles()
    {
        // Arrange
        var req = new RunMonteCarloSimulationRequest(Iterations: 1500);

        // Act
        var result = _service.RunSimulation(RevenueSimulationService.DefaultTenantId, req);

        // Assert
        result.PercentileDistribution.Should().HaveCount(10);
        result.HistogramBins.Should().HaveCount(10);
        result.HistogramBins.Sum(b => b.Count).Should().Be(1500);
        result.HistogramBins.Sum(b => b.DensityPercent).Should().BeInRange(99.0, 101.0);
    }

    [Fact]
    public void GetSlippageAlerts_DefaultTenant_IdentifiesHighRiskAndStalledDeals()
    {
        // Act
        var alerts = _service.GetSlippageAlerts(RevenueSimulationService.DefaultTenantId);

        // Assert
        alerts.Should().NotBeNullOrEmpty();
        alerts.Should().Contain(a => a.RiskLevel == "Critical" || a.RiskLevel == "High");
        alerts.Should().AllSatisfy(a =>
        {
            a.OpportunityId.Should().NotBeNullOrWhiteSpace();
            a.RiskReason.Should().NotBeNullOrWhiteSpace();
            a.RecommendedIntervention.Should().NotBeNullOrWhiteSpace();
            a.SlippageProbability.Should().BeInRange(0.0, 1.0);
        });
        // Deal 006 (GovTech Sovereign Cloud, 110 days age, slippage 0.74) must be in alerts
        alerts.Should().Contain(a => a.OpportunityId == "opp-006" && a.RiskLevel == "Critical");
    }

    [Fact]
    public void GetOverviewReport_DefaultTenant_AggregatesPipelineAndStageBreakdown()
    {
        // Act
        var overview = _service.GetOverviewReport(RevenueSimulationService.DefaultTenantId, quotaTarget: 1750000);

        // Assert
        overview.Should().NotBeNull();
        overview.TenantId.Should().Be(RevenueSimulationService.DefaultTenantId.ToString());
        overview.TotalPipelineValue.Should().Be(3560000);
        overview.WeightedPipelineValue.Should().BeGreaterThan(1000000);
        overview.TotalOpportunitiesCount.Should().Be(10);
        overview.BaselineSimulation.Should().NotBeNull();
        overview.TopOpportunities.Should().NotBeEmpty();
        overview.CriticalSlippageAlerts.Should().NotBeEmpty();
        overview.StageBreakdown.Should().ContainKey(PipelineStage.ContractNegotiation);
        overview.CategoryBreakdown.Should().ContainKey(ForecastCategory.Commit);
    }

    [Theory]
    [InlineData(SimulationDistributionModel.Normal, 0.9)]
    [InlineData(SimulationDistributionModel.LogNormal, 1.1)]
    [InlineData(SimulationDistributionModel.Triangular, 1.25)]
    public void RunSimulation_DifferentDistributionModels_ExecutesWithoutError(SimulationDistributionModel model, double volatility)
    {
        // Arrange
        var req = new RunMonteCarloSimulationRequest(
            Iterations: 1000,
            QuotaTarget: 1500000,
            DefaultDistribution: model,
            MarketVolatilityIndex: volatility
        );

        // Act
        var result = _service.RunSimulation(RevenueSimulationService.DefaultTenantId, req);

        // Assert
        result.Should().NotBeNull();
        result.P50.Should().BeGreaterThan(0);
        result.Mean.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TenantIsolation_MutatingOneTenant_DoesNotAffectOtherTenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _service.AddOpportunity(tenantA, new AddOpportunityInputRequest(
            DealName: "Tenant A Secret Deal",
            AccountName: "Alpha Org",
            Amount: 500000,
            Stage: PipelineStage.ProposalPresented,
            Category: ForecastCategory.BestCase,
            WinProbability: 0.8,
            TargetCloseDate: DateTime.UtcNow.AddDays(30)
        ));

        // Act
        var dealsA = _service.GetOpportunities(tenantA);
        var dealsB = _service.GetOpportunities(tenantB);
        var dealsDefault = _service.GetOpportunities(RevenueSimulationService.DefaultTenantId);

        // Assert
        dealsA.Should().ContainSingle(d => d.DealName == "Tenant A Secret Deal");
        dealsB.Should().BeEmpty();
        dealsDefault.Should().NotContain(d => d.DealName == "Tenant A Secret Deal");
    }
}
