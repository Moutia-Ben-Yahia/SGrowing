namespace SGrowing.Api.Modules.SupplyChain.DisruptionSimulator;

/// <summary>
/// Classification of catastrophic supply chain disruption events.
/// </summary>
public enum DisruptionEventType
{
    SupplierFacilityShutdown = 1,
    PortCustomsStrike = 2,
    MaritimeChokePointClosure = 3,  // Suez / Strait of Gibraltar / Bab el-Mandeb
    RawMaterialShortage = 4,
    ExtremeWeatherCurfew = 5
}

/// <summary>
/// Operational tier of a supply chain entity.
/// </summary>
public enum SupplyNodeType
{
    RawMaterialSupplier = 1,
    SubassemblyManufacturer = 2,
    TransshipmentPort = 3,
    CentralWarehouseHub = 4,
    RegionalFulfillmentCenter = 5
}

/// <summary>
/// Node in the multi-tier supply chain topology.
/// </summary>
public sealed record SupplyChainNode(
    string NodeId,
    string Name,
    SupplyNodeType NodeType,
    string CountryIso,
    double DailyCapacityUnits,
    double LeadTimeDaysMean,
    double LeadTimeDaysStdDev,
    double AnnualFailureProbability
);

/// <summary>
/// Directed link connecting two supply chain nodes.
/// </summary>
public sealed record SupplyChainLink(
    string LinkId,
    string FromNodeId,
    string ToNodeId,
    double TransitDaysMean,
    double TransitDaysStdDev,
    decimal FreightCostPerUnitDzd,
    bool IsSingleSourceDependency
);

/// <summary>
/// Configuration for a Monte Carlo disruption stress test.
/// </summary>
public sealed record MonteCarloStressTestScenario(
    string ScenarioId,
    string TargetSku,
    int IterationCount,
    int SimulationHorizonDays,
    double DailyDemandMean,
    double DailyDemandStdDev,
    decimal UnitSellingPriceDzd,
    decimal UnitHoldingCostAnnualDzd,
    IReadOnlyList<DisruptionEventType> InjectedDisruptions
);

/// <summary>
/// Empirical results from a Monte Carlo disruption simulation.
/// </summary>
public sealed record MonteCarloSimulationResult(
    string ScenarioId,
    string TargetSku,
    int IterationsCompleted,
    double MeanFulfillmentRatio,
    double P95FulfillmentRatio,
    double P99FulfillmentRatio,
    decimal ValueAtRisk95Dzd,
    double StockoutProbability,
    IReadOnlyList<string> CriticalBottlenecks,
    DateTime SimulatedAtUtc
);

/// <summary>
/// Vulnerability score for a specific node in the supply chain topology.
/// </summary>
public sealed record NodeVulnerabilityScore(
    string NodeId,
    string NodeName,
    SupplyNodeType NodeType,
    double BetweennessCentrality,
    bool IsSingleSource,
    double VulnerabilityIndex, // 0.0 (resilient) to 1.0 (critical vulnerability)
    string RiskTier
);

/// <summary>
/// Calculated optimal safety stock buffer and reorder point recommendation.
/// </summary>
public sealed record SafetyStockOptimizationResult(
    string ProductSku,
    double DailyDemandMean,
    double LeadTimeDaysMean,
    int RecommendedSafetyStockUnits,
    int RecommendedReorderPointUnits,
    decimal AnnualHoldingCostDzd,
    double TargetServiceLevelPercent
);

/// <summary>
/// Request to run Monte Carlo stress test simulation.
/// </summary>
public sealed record RunSimulationRequest(
    MonteCarloStressTestScenario Scenario,
    IReadOnlyList<SupplyChainNode> Nodes,
    IReadOnlyList<SupplyChainLink> Links
);

/// <summary>
/// Request to calculate safety stock buffers.
/// </summary>
public sealed record CalculateSafetyStockRequest(
    string ProductSku,
    double DailyDemandMean,
    double DailyDemandStdDev,
    double LeadTimeDaysMean,
    double LeadTimeDaysStdDev,
    decimal UnitHoldingCostAnnualDzd,
    double TargetServiceLevelPercent = 95.0
);
