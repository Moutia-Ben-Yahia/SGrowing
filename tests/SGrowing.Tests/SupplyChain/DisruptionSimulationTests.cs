using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.SupplyChain.DisruptionSimulator;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

public sealed class DisruptionSimulationTests
{
    private readonly MonteCarloDisruptionSimulator _simulator = new();
    private readonly ChokePointVulnerabilityIndex _vulnerabilityEvaluator = new();
    private readonly StrategicStockBufferOptimizer _bufferOptimizer = new();

    private (List<SupplyChainNode> Nodes, List<SupplyChainLink> Links) CreateSampleNetwork()
    {
        var nodes = new List<SupplyChainNode>
        {
            new("NODE-SUP-CHINA", "Shenzhen Microchip Fab", SupplyNodeType.RawMaterialSupplier, "CN", 1000.0, 20.0, 4.0, 0.05),
            new("NODE-PORT-ALG", "Port of Algiers Terminal", SupplyNodeType.TransshipmentPort, "DZ", 5000.0, 3.0, 1.0, 0.12),
            new("NODE-WH-BLIDA", "Blida Central Distribution Hub", SupplyNodeType.CentralWarehouseHub, "DZ", 2000.0, 2.0, 0.5, 0.02)
        };

        var links = new List<SupplyChainLink>
        {
            new("LINK-SEA-CN-DZ", "NODE-SUP-CHINA", "NODE-PORT-ALG", 25.0, 5.0, 120m, IsSingleSourceDependency: true),
            new("LINK-RD-ALG-BLI", "NODE-PORT-ALG", "NODE-WH-BLIDA", 1.5, 0.5, 15m, IsSingleSourceDependency: false)
        };

        return (nodes, links);
    }

    #region Monte Carlo Disruption Simulation Tests

    [Fact]
    public void RunSimulation_NominalScenario_AchievesHighFulfillment()
    {
        var (nodes, links) = CreateSampleNetwork();
        var scenario = new MonteCarloStressTestScenario(
            ScenarioId: "SCEN-NOMINAL",
            TargetSku: "SKU-CHIP-99",
            IterationCount: 200,
            SimulationHorizonDays: 60,
            DailyDemandMean: 50.0,
            DailyDemandStdDev: 5.0,
            UnitSellingPriceDzd: 1500m,
            UnitHoldingCostAnnualDzd: 80m,
            InjectedDisruptions: [] // No injected catastrophic events
        );

        var result = _simulator.RunSimulation(new RunSimulationRequest(scenario, nodes, links));

        result.Should().NotBeNull();
        result.IterationsCompleted.Should().Be(200);
        result.MeanFulfillmentRatio.Should().BeGreaterThan(0.75);
        result.CriticalBottlenecks.Should().NotBeEmpty();
    }

    [Fact]
    public void RunSimulation_CatastrophicDisruptionInjected_ReflectsStockoutRiskAndVaR()
    {
        var (nodes, links) = CreateSampleNetwork();
        var scenario = new MonteCarloStressTestScenario(
            ScenarioId: "SCEN-BLACK-SWAN",
            TargetSku: "SKU-CHIP-99",
            IterationCount: 300,
            SimulationHorizonDays: 90,
            DailyDemandMean: 50.0,
            DailyDemandStdDev: 10.0,
            UnitSellingPriceDzd: 2500m,
            UnitHoldingCostAnnualDzd: 120m,
            InjectedDisruptions: [DisruptionEventType.MaritimeChokePointClosure, DisruptionEventType.PortCustomsStrike]
        );

        var result = _simulator.RunSimulation(new RunSimulationRequest(scenario, nodes, links));

        result.Should().NotBeNull();
        result.StockoutProbability.Should().BeGreaterThan(0.0);
        result.ValueAtRisk95Dzd.Should().BeGreaterThan(0m);
    }

    #endregion

    #region Choke Point & Vulnerability Index Tests

    [Fact]
    public void EvaluateNetworkVulnerabilities_SingleSourceNode_AssignedHighRiskOrChokePoint()
    {
        var (nodes, links) = CreateSampleNetwork();

        var scores = _vulnerabilityEvaluator.EvaluateNetworkVulnerabilities(nodes, links);

        scores.Should().HaveCount(3);
        // Shenzhen sole-source supplier should have high vulnerability
        var chinaNode = scores.First(s => s.NodeId == "NODE-SUP-CHINA");
        chinaNode.IsSingleSource.Should().BeTrue();
        chinaNode.VulnerabilityIndex.Should().BeGreaterThan(0.40);
        chinaNode.RiskTier.Should().BeOneOf("HighRisk", "CriticalChokePoint");
    }

    [Fact]
    public void EvaluateNetworkVulnerabilities_EmptyNetwork_ReturnsEmpty()
    {
        var scores = _vulnerabilityEvaluator.EvaluateNetworkVulnerabilities([], []);
        scores.Should().BeEmpty();
    }

    #endregion

    #region Strategic Safety Stock Buffer Optimizer Tests

    [Fact]
    public void CalculateSafetyStock_HigherServiceLevel_RequiresLargerBuffer()
    {
        var req90 = new CalculateSafetyStockRequest("SKU-OIL-1", DailyDemandMean: 100, DailyDemandStdDev: 15, LeadTimeDaysMean: 10, LeadTimeDaysStdDev: 2, UnitHoldingCostAnnualDzd: 50m, TargetServiceLevelPercent: 90.0);
        var req99 = new CalculateSafetyStockRequest("SKU-OIL-1", DailyDemandMean: 100, DailyDemandStdDev: 15, LeadTimeDaysMean: 10, LeadTimeDaysStdDev: 2, UnitHoldingCostAnnualDzd: 50m, TargetServiceLevelPercent: 99.0);

        var res90 = _bufferOptimizer.CalculateSafetyStock(req90);
        var res99 = _bufferOptimizer.CalculateSafetyStock(req99);

        res99.RecommendedSafetyStockUnits.Should().BeGreaterThan(res90.RecommendedSafetyStockUnits);
        res99.AnnualHoldingCostDzd.Should().BeGreaterThan(res90.AnnualHoldingCostDzd);
        res99.RecommendedReorderPointUnits.Should().BeGreaterThan(res90.RecommendedReorderPointUnits);
    }

    [Fact]
    public void CalculateSafetyStock_LeadTimeVolatility_SignificantlyIncreasesBuffer()
    {
        // Zero lead time variance vs high lead time variance
        var stableLeadTime = new CalculateSafetyStockRequest("SKU-A", 50, 10, 14, 0.0, 30m, 95.0);
        var volatileLeadTime = new CalculateSafetyStockRequest("SKU-A", 50, 10, 14, 5.0, 30m, 95.0);

        var resStable = _bufferOptimizer.CalculateSafetyStock(stableLeadTime);
        var resVolatile = _bufferOptimizer.CalculateSafetyStock(volatileLeadTime);

        resVolatile.RecommendedSafetyStockUnits.Should().BeGreaterThan(resStable.RecommendedSafetyStockUnits);
    }

    [Fact]
    public void CalculateSafetyStock_InvalidInputs_ThrowsArgumentException()
    {
        var act = () => _bufferOptimizer.CalculateSafetyStock(new CalculateSafetyStockRequest("SKU-BAD", -5, 1, 10, 1, 10m));
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddDisruptionSimulationServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddDisruptionSimulationServices();

        services.Should().Contain(d => d.ServiceType == typeof(MonteCarloDisruptionSimulator) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(ChokePointVulnerabilityIndex) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(StrategicStockBufferOptimizer) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
