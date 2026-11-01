using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.SupplyChain.Replenishment;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

/// <summary>
/// Comprehensive test suite for Phase 21: Autonomous Supply Chain Demand Forecasting,
/// Auto-Replenishment &amp; JIT Cross-Docking.
/// </summary>
public class SupplyChainAndJitTests
{
    private readonly SupplyDemandForecastingEngine _forecastingEngine = new();
    private readonly SupplierAutoReplenishmentService _replenishmentService;
    private readonly CrossDockingAllocationService _crossDockService = new();

    public SupplyChainAndJitTests()
    {
        _replenishmentService = new SupplierAutoReplenishmentService(_forecastingEngine);
    }

    #region 1. Holt-Winters Triple Exponential Smoothing Forecasting Tests

    [Fact]
    public void ForecastDemand_WithEmptyHistory_ReturnsZeroSeries()
    {
        var result = _forecastingEngine.ForecastDemand("SKU-EMPTY", [], forecastHorizonDays: 7);

        result.ProjectedDemandSeries.Should().HaveCount(7);
        result.ProjectedDemandSeries.Should().AllBeEquivalentTo(0m);
        result.Trend.Should().Be(0m);
    }

    [Fact]
    public void ForecastDemand_WithSingleDataPoint_ExtrapolatesConstantSeries()
    {
        var result = _forecastingEngine.ForecastDemand("SKU-SINGLE", [42m], forecastHorizonDays: 5);

        result.ProjectedDemandSeries.Should().HaveCount(5);
        result.ProjectedDemandSeries.Should().AllBeEquivalentTo(42m);
        result.CurrentLevel.Should().Be(42m);
    }

    [Fact]
    public void ForecastDemand_WithLinearUpwardTrend_DetectsPositiveTrend()
    {
        // 14 days of steadily increasing demand: 10, 12, 14, 16...
        List<decimal> demand = [10m, 12m, 14m, 16m, 18m, 20m, 22m, 24m, 26m, 28m, 30m, 32m, 34m, 36m];

        var result = _forecastingEngine.ForecastDemand(
            sku: "SKU-UPWARD",
            historicalDemand: demand,
            forecastHorizonDays: 7,
            period: 7,
            alpha: 0.3m,
            beta: 0.2m,
            gamma: 0.1m);

        result.Trend.Should().BeGreaterThan(0m);
        result.ProjectedDemandSeries.Should().HaveCount(7);
        // Each future forecast should exceed the last observed point (36m)
        result.ProjectedDemandSeries[0].Should().BeGreaterThan(30m);
        result.ProjectedDemandSeries.Last().Should().BeGreaterThan(result.ProjectedDemandSeries.First());
    }

    [Fact]
    public void ForecastDemand_WithDownwardTrend_ConstrainsDemandToNonNegative()
    {
        // Sharp decline in demand: 50, 40, 30, 20, 10, 5, 2...
        List<decimal> declining = [50m, 40m, 30m, 20m, 10m, 5m, 2m, 1m, 1m, 0m, 0m, 0m, 0m, 0m];

        var result = _forecastingEngine.ForecastDemand(
            sku: "SKU-DECLINE",
            historicalDemand: declining,
            forecastHorizonDays: 10,
            period: 7);

        result.Trend.Should().BeLessThanOrEqualTo(0m);
        result.ProjectedDemandSeries.Should().HaveCount(10);
        // Demand can never be negative
        result.ProjectedDemandSeries.Should().OnlyContain(v => v >= 0m);
    }

    [Fact]
    public void ForecastDemand_WithWeeklySeasonality_Period7_PreservesSeasonalPeaks()
    {
        // 3 cycles (21 days) with recurring weekend spikes on day 6 and 7 (e.g., Fri & Sat)
        // Pattern: [10, 10, 10, 10, 12, 35, 40]
        List<decimal> weeklySeries =
        [
            10m, 10m, 10m, 10m, 12m, 35m, 40m,
            11m, 10m,  9m, 11m, 13m, 36m, 39m,
            10m, 11m, 10m, 10m, 12m, 34m, 41m
        ];

        var result = _forecastingEngine.ForecastDemand(
            sku: "SKU-WEEKLY",
            historicalDemand: weeklySeries,
            forecastHorizonDays: 7,
            period: 7,
            alpha: 0.25m,
            beta: 0.1m,
            gamma: 0.35m);

        result.SeasonalityFactors.Should().HaveCount(7);
        // Days 6 and 7 (index 5 and 6) should show strong positive seasonal factors
        result.SeasonalityFactors[5].Should().BeGreaterThan(result.SeasonalityFactors[0]);
        result.SeasonalityFactors[6].Should().BeGreaterThan(result.SeasonalityFactors[1]);

        // In 7-day forecast, projected weekend values (days 6 & 7) should peak
        result.ProjectedDemandSeries[5].Should().BeGreaterThan(result.ProjectedDemandSeries[0]);
        result.ProjectedDemandSeries[6].Should().BeGreaterThan(result.ProjectedDemandSeries[1]);
    }

    [Fact]
    public void ForecastDemand_WithMonthlySeasonality_Period30_ComputesProjections()
    {
        // 60 days of daily data (2 full 30-day periods)
        var monthlyData = new List<decimal>(60);
        for (int i = 0; i < 60; i++)
        {
            // Monthly payday spike on day 1 and 30
            decimal seasonal = (i % 30 == 0 || i % 30 == 29) ? 50m : 15m;
            monthlyData.Add(seasonal);
        }

        var result = _forecastingEngine.ForecastDemand(
            sku: "SKU-MONTHLY",
            historicalDemand: monthlyData,
            forecastHorizonDays: 14,
            period: 30);

        result.Period.Should().Be(30);
        result.ProjectedDemandSeries.Should().HaveCount(14);
        result.SeasonalityFactors.Should().HaveCount(30);
    }

    [Fact]
    public void ForecastDemand_ShortHistoryLessThanPeriod_FallsBackToDoubleExponential()
    {
        // 4 points of history, period is 7
        List<decimal> shortHistory = [100m, 110m, 120m, 130m];

        var result = _forecastingEngine.ForecastDemand(
            sku: "SKU-SHORT",
            historicalDemand: shortHistory,
            forecastHorizonDays: 5,
            period: 7);

        result.ProjectedDemandSeries.Should().HaveCount(5);
        result.Trend.Should().BeGreaterThan(0m);
        // Trend is approximately 10 per day
        result.ProjectedDemandSeries[0].Should().BeGreaterThan(130m);
    }

    [Fact]
    public void ForecastDemand_WithClampParameters_AcceptsAndClampsAlphaBetaGamma()
    {
        List<decimal> series = [10m, 15m, 20m, 25m, 30m, 35m, 40m, 45m];

        var result = _forecastingEngine.ForecastDemand(
            sku: "SKU-CLAMP",
            historicalDemand: series,
            forecastHorizonDays: 7,
            period: 7,
            alpha: 5.0m, // should clamp to 1.0
            beta: -2.0m, // should clamp to 0.01
            gamma: 99.0m); // should clamp to 1.0

        result.Alpha.Should().Be(1.0m);
        result.Beta.Should().Be(0.01m);
        result.Gamma.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateDemandStatistics_CalculatesMeanAndSampleStdDevAccurately()
    {
        // Data: [10, 12, 23, 23, 16, 23, 21, 16] (N=8)
        // Sum = 144, Mean = 18.00
        // Variance: ( (10-18)^2 + (12-18)^2 + (23-18)^2*3 + (16-18)^2*2 + (21-18)^2 ) / 7
        // = (64 + 36 + 25*3 + 4*2 + 9) / 7 = (100 + 75 + 8 + 9) / 7 = 192 / 7 = 27.4286
        // StdDev = sqrt(27.4286) = 5.2372
        List<decimal> series = [10m, 12m, 23m, 23m, 16m, 23m, 21m, 16m];

        var (mean, stdDev) = _forecastingEngine.CalculateDemandStatistics(series);

        mean.Should().Be(18.0m);
        stdDev.Should().BeApproximately(5.2372m, 0.001m);
    }

    [Fact]
    public void AnalyzeSkuDemand_ExecutesFullPipelineAndReturnsConsistentMetrics()
    {
        List<decimal> demand = [20m, 22m, 21m, 25m, 24m, 28m, 30m, 22m, 23m, 22m, 26m, 25m, 29m, 31m];

        var request = new CalculateForecastRequest(
            Sku: "SKU-INTEGRATED",
            HistoricalDemand: demand,
            ForecastHorizonDays: 7,
            Period: 7,
            LeadTimeDays: 10m,
            LeadTimeStdDev: 2m,
            UnitHoldingCost: 4m,
            OrderSetupCost: 100m,
            Confidence: ServiceLevelConfidence.Sla99);

        var response = _forecastingEngine.AnalyzeSkuDemand(request);

        response.AverageDailyDemand.Should().BeGreaterThan(20m);
        response.DemandStdDev.Should().BeGreaterThan(0m);
        response.DynamicSafetyStock.Should().BeGreaterThan(0m);
        response.DynamicReorderPoint.Should().BeGreaterThan(response.AverageDailyDemand * 10m);
        response.EconomicOrderQuantity.Should().BeGreaterThan(0m);
        response.Forecast.ProjectedDemandSeries.Should().HaveCount(7);
    }

    [Fact]
    public void ForecastDemand_ZeroOrNegativeHorizon_DefaultsToSevenDays()
    {
        var result = _forecastingEngine.ForecastDemand("SKU-HORIZON", [10m, 20m, 30m], forecastHorizonDays: -5);
        result.ForecastHorizonDays.Should().Be(7);
        result.ProjectedDemandSeries.Should().HaveCount(7);
    }

    #endregion

    #region 2. Safety Stock, Reorder Point, and Wilson EOQ Mathematics

    [Fact]
    public void CalculateSafetyStock_WithSla99_UsesZ233Correctly()
    {
        // d_bar = 50, sigma_d = 5, L_bar = 9, sigma_L = 0 (constant lead time)
        // Combined Variance = 9 * 5^2 + 50^2 * 0 = 9 * 25 = 225
        // sqrt(225) = 15
        // SS = 2.33 * 15 = 34.95
        decimal avgDemand = 50m;
        decimal demandStdDev = 5m;
        decimal leadTimeDays = 9m;
        decimal leadTimeStdDev = 0m;

        decimal ss = _forecastingEngine.CalculateSafetyStock(
            avgDemand, demandStdDev, leadTimeDays, leadTimeStdDev, ServiceLevelConfidence.Sla99);

        ss.Should().Be(34.95m);
    }

    [Fact]
    public void CalculateSafetyStock_WithSla95_UsesZ164Correctly()
    {
        // d_bar = 50, sigma_d = 5, L_bar = 9, sigma_L = 0
        // sqrt(225) = 15
        // SS = 1.64 * 15 = 24.60
        decimal avgDemand = 50m;
        decimal demandStdDev = 5m;
        decimal leadTimeDays = 9m;
        decimal leadTimeStdDev = 0m;

        decimal ss = _forecastingEngine.CalculateSafetyStock(
            avgDemand, demandStdDev, leadTimeDays, leadTimeStdDev, ServiceLevelConfidence.Sla95);

        ss.Should().Be(24.60m);
    }

    [Fact]
    public void CalculateSafetyStock_WithZeroLeadTimeVariation_ReducesToDemandVariance()
    {
        // L_bar = 4, sigma_d = 10, sigma_L = 0
        // Combined variance = 4 * 10^2 + d_bar^2 * 0 = 400 => sqrt(400) = 20
        // SS = 2.33 * 20 = 46.60
        decimal ss = _forecastingEngine.CalculateSafetyStock(
            averageDailyDemand: 100m,
            demandStdDev: 10m,
            leadTimeDays: 4m,
            leadTimeStdDev: 0m,
            confidence: ServiceLevelConfidence.Sla99);

        ss.Should().Be(46.60m);
    }

    [Fact]
    public void CalculateSafetyStock_WithZeroDemandVariation_ReducesToLeadTimeVariance()
    {
        // sigma_d = 0, d_bar = 30, sigma_L = 2
        // Combined variance = L_bar * 0 + 30^2 * 2^2 = 900 * 4 = 3600 => sqrt(3600) = 60
        // SS = 2.33 * 60 = 139.80
        decimal ss = _forecastingEngine.CalculateSafetyStock(
            averageDailyDemand: 30m,
            demandStdDev: 0m,
            leadTimeDays: 7m,
            leadTimeStdDev: 2m,
            confidence: ServiceLevelConfidence.Sla99);

        ss.Should().Be(139.80m);
    }

    [Fact]
    public void CalculateSafetyStock_WithBothDemandAndLeadTimeVariance_CombinesVariancesCorrectly()
    {
        // d_bar = 20, sigma_d = 4, L_bar = 9, sigma_L = 1
        // term1 = 9 * 4^2 = 9 * 16 = 144
        // term2 = 20^2 * 1^2 = 400 * 1 = 400
        // Combined variance = 144 + 400 = 544
        // sqrt(544) ≈ 23.3238
        // SS = 2.33 * 23.3238 = 54.34
        decimal ss = _forecastingEngine.CalculateSafetyStock(
            averageDailyDemand: 20m,
            demandStdDev: 4m,
            leadTimeDays: 9m,
            leadTimeStdDev: 1m,
            confidence: ServiceLevelConfidence.Sla99);

        ss.Should().BeApproximately(54.34m, 0.05m);
    }

    [Fact]
    public void CalculateSafetyStock_WithZeroInputs_ReturnsZero()
    {
        decimal ss = _forecastingEngine.CalculateSafetyStock(0m, 0m, 0m, 0m);
        ss.Should().Be(0m);
    }

    [Fact]
    public void CalculateReorderPoint_SumsLeadTimeDemandAndSafetyStock()
    {
        // Lead time demand: 25 units/day * 6 days = 150
        // Safety Stock = 35.5
        // ROP = 150 + 35.5 = 185.5
        decimal avgDemand = 25m;
        decimal leadTime = 6m;
        decimal safetyStock = 35.5m;

        decimal rop = _forecastingEngine.CalculateReorderPoint(avgDemand, leadTime, safetyStock);

        rop.Should().Be(185.5m);
    }

    [Fact]
    public void CalculateReorderPoint_WithZeroLeadTime_EqualsSafetyStock()
    {
        decimal rop = _forecastingEngine.CalculateReorderPoint(100m, 0m, 40m);
        rop.Should().Be(40m);
    }

    [Fact]
    public void CalculateEoq_WithStandardWilsonParameters_ComputesExactEconomicOrderQuantity()
    {
        // Annual demand D = 10,000 units
        // Order setup cost S = $50
        // Unit holding cost H = $4/unit/year
        // EOQ = sqrt( (2 * 10000 * 50) / 4 ) = sqrt( 1,000,000 / 4 ) = sqrt(250,000) = 500
        decimal annualDemand = 10_000m;
        decimal setupCost = 50m;
        decimal holdingCost = 4m;

        decimal eoq = _forecastingEngine.CalculateEoq(annualDemand, setupCost, holdingCost);

        eoq.Should().Be(500.00m);
    }

    [Fact]
    public void CalculateEoq_WithZeroHoldingCostOrZeroDemand_SafeguardsAgainstDivisionByZero()
    {
        decimal zeroH = _forecastingEngine.CalculateEoq(1000m, 50m, 0m);
        zeroH.Should().Be(0m);

        decimal zeroD = _forecastingEngine.CalculateEoq(0m, 50m, 5m);
        zeroD.Should().Be(0m);
    }

    [Fact]
    public void CalculateEoqFromDailyDemand_AnnualizesDailyDemandAndComputesEoq()
    {
        // Daily demand = 20 units/day => Annual demand = 20 * 365 = 7300
        // S = 80, H = 2
        // EOQ = sqrt( (2 * 7300 * 80) / 2 ) = sqrt( 584,000 ) ≈ 764.1989
        decimal eoq = _forecastingEngine.CalculateEoqFromDailyDemand(20m, 80m, 2m);

        eoq.Should().BeApproximately(764.20m, 0.05m);
    }

    #endregion

    #region 3. Supplier Auto-Replenishment & Purchase Order Proposals

    [Fact]
    public async Task EvaluateWarehouseStock_WhenStockPlusOnOrderBelowRop_TriggersPoProposal()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-TRIGGER-1",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 7m,
            LeadTimeStdDev: 1m,
            CurrentStock: 40m,
            OnOrderStock: 10m, // Position = 50
            SafetyStock: 25m,
            ReorderPoint: 95m,  // Position 50 <= ROP 95 => Triggers!
            UnitHoldingCost: 3m,
            OrderSetupCost: 50m,
            EconomicOrderQuantity: 200m,
            UnitCost: 15m);

        var response = await _replenishmentService.EvaluateWarehouseStockAsync(
            warehouseId, [record]);

        response.TotalSkusEvaluated.Should().Be(1);
        response.ReplenishmentTriggeredCount.Should().Be(1);
        response.GeneratedProposals.Should().HaveCount(1);

        var proposal = response.GeneratedProposals[0];
        proposal.Sku.Should().Be("SKU-TRIGGER-1");
        proposal.Status.Should().Be(PoStatus.Proposed);
        proposal.TotalEstimatedCost.Should().Be(proposal.Quantity * 15m);
    }

    [Fact]
    public async Task EvaluateWarehouseStock_WhenStockPlusOnOrderAboveRop_DoesNotTriggerPoProposal()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-NO-TRIGGER",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 7m,
            LeadTimeStdDev: 1m,
            CurrentStock: 80m,
            OnOrderStock: 30m, // Position = 110
            SafetyStock: 25m,
            ReorderPoint: 95m,  // Position 110 > ROP 95 => Safe!
            UnitHoldingCost: 3m,
            OrderSetupCost: 50m,
            EconomicOrderQuantity: 200m);

        var response = await _replenishmentService.EvaluateWarehouseStockAsync(
            warehouseId, [record]);

        response.ReplenishmentTriggeredCount.Should().Be(0);
        response.GeneratedProposals.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateWarehouseStock_WhenStockPlusOnOrderExactlyEqualsRop_TriggersPoProposal()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-EXACT-ROP",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 5m,
            LeadTimeStdDev: 1m,
            CurrentStock: 50m,
            OnOrderStock: 0m,   // Position = 50
            SafetyStock: 15m,
            ReorderPoint: 50m,  // Position 50 <= ROP 50 => Boundary triggers!
            UnitHoldingCost: 2m,
            OrderSetupCost: 30m,
            EconomicOrderQuantity: 100m);

        var response = await _replenishmentService.EvaluateWarehouseStockAsync(
            warehouseId, [record]);

        response.ReplenishmentTriggeredCount.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateWarehouseStock_WhenStockIsZeroOrNegative_AssignsUrgentPriorityForStockout()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-STOCKOUT",
            WarehouseId: warehouseId,
            AverageDailyDemand: 20m,
            DemandStdDev: 3m,
            LeadTimeDays: 7m,
            LeadTimeStdDev: 1m,
            CurrentStock: 0m, // Stockout!
            OnOrderStock: 0m,
            SafetyStock: 30m,
            ReorderPoint: 170m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 40m,
            EconomicOrderQuantity: 300m);

        var response = await _replenishmentService.EvaluateWarehouseStockAsync(
            warehouseId, [record]);

        var proposal = response.GeneratedProposals.Single();
        proposal.Priority.Should().Be(PoPriority.Urgent);
        proposal.Reason.Should().Contain("Stockout breach");
    }

    [Fact]
    public async Task EvaluateWarehouseStock_WhenStockBelowSafetyStock_AssignsUrgentPriority()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-SAFETY-BREACH",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 7m,
            LeadTimeStdDev: 1m,
            CurrentStock: 15m, // CurrentStock (15) <= SafetyStock (30)
            OnOrderStock: 0m,
            SafetyStock: 30m,
            ReorderPoint: 100m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 40m,
            EconomicOrderQuantity: 150m);

        var response = await _replenishmentService.EvaluateWarehouseStockAsync(
            warehouseId, [record]);

        var proposal = response.GeneratedProposals.Single();
        proposal.Priority.Should().Be(PoPriority.Urgent);
        proposal.Reason.Should().Contain("Safety stock breached");
    }

    [Fact]
    public async Task EvaluateWarehouseStock_WhenStockAboveSafetyStockButBelowRop_AssignsNormalPriority()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-NORMAL-PRIORITY",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 7m,
            LeadTimeStdDev: 1m,
            CurrentStock: 45m, // > SafetyStock (20), but <= ROP (90)
            OnOrderStock: 10m, // Position = 55 <= 90
            SafetyStock: 20m,
            ReorderPoint: 90m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 40m,
            EconomicOrderQuantity: 100m);

        var response = await _replenishmentService.EvaluateWarehouseStockAsync(
            warehouseId, [record]);

        var proposal = response.GeneratedProposals.Single();
        proposal.Priority.Should().Be(PoPriority.Normal);
    }

    [Fact]
    public async Task EvaluateWarehouseStock_SizesQuantityToMaximumOfEoqAndDeficit()
    {
        var warehouseId = Guid.NewGuid();

        // Case 1: Deficit is small (40), EOQ is large (200) => proposal size should be 200
        var recordEoqLarger = new SkuVelocityRecord(
            Sku: "SKU-EOQ-WINS",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 7m,
            LeadTimeStdDev: 1m,
            CurrentStock: 80m,
            OnOrderStock: 0m,
            SafetyStock: 20m,
            ReorderPoint: 90m, // Target = 110, Position = 80 => Deficit = 30
            UnitHoldingCost: 1m,
            OrderSetupCost: 100m,
            EconomicOrderQuantity: 250m);

        var res1 = await _replenishmentService.EvaluateWarehouseStockAsync(warehouseId, [recordEoqLarger]);
        res1.GeneratedProposals.Single().Quantity.Should().Be(250m);

        // Case 2: Deep stock deficit (e.g. 500) larger than EOQ (100) => proposal size should be deficit
        var recordDeficitLarger = new SkuVelocityRecord(
            Sku: "SKU-DEFICIT-WINS",
            WarehouseId: warehouseId,
            AverageDailyDemand: 50m,
            DemandStdDev: 5m,
            LeadTimeDays: 10m,
            LeadTimeStdDev: 2m,
            CurrentStock: 0m,
            OnOrderStock: 0m,
            SafetyStock: 100m,
            ReorderPoint: 500m, // Target = 600, Position = 0 => Deficit = 600
            UnitHoldingCost: 10m,
            OrderSetupCost: 20m,
            EconomicOrderQuantity: 120m);

        var res2 = await _replenishmentService.EvaluateWarehouseStockAsync(warehouseId, [recordDeficitLarger]);
        res2.GeneratedProposals.Single().Quantity.Should().Be(600m);
    }

    [Fact]
    public async Task DispatchPurchaseOrder_TransitionsStatusToDispatchedAndGeneratesEdi850Payload()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-EDI-TEST",
            WarehouseId: warehouseId,
            AverageDailyDemand: 15m,
            DemandStdDev: 3m,
            LeadTimeDays: 5m,
            LeadTimeStdDev: 1m,
            CurrentStock: 10m,
            OnOrderStock: 0m,
            SafetyStock: 20m,
            ReorderPoint: 95m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 50m,
            EconomicOrderQuantity: 180m,
            UnitCost: 35m);

        var evalResponse = await _replenishmentService.EvaluateWarehouseStockAsync(warehouseId, [record]);
        var proposal = evalResponse.GeneratedProposals.Single();

        var dispatchResponse = await _replenishmentService.DispatchPurchaseOrderAsync(proposal.ProposalId);

        dispatchResponse.Success.Should().BeTrue();
        dispatchResponse.Proposal.Status.Should().Be(PoStatus.Dispatched);
        dispatchResponse.Proposal.DispatchedAtUtc.Should().NotBeNull();
        dispatchResponse.TransmissionPayload.Should().Contain("ANSI-X12-850");
        dispatchResponse.TransmissionPayload.Should().Contain(proposal.Sku);
        dispatchResponse.TransmissionPayload.Should().Contain(proposal.ProposalId.ToString("N")[..8].ToUpperInvariant());
    }

    [Fact]
    public async Task DispatchPurchaseOrder_WhenAlreadyDispatched_ReturnsFailureResponse()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-DOUBLE-DISPATCH",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 5m,
            LeadTimeStdDev: 1m,
            CurrentStock: 0m,
            OnOrderStock: 0m,
            SafetyStock: 20m,
            ReorderPoint: 70m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 40m,
            EconomicOrderQuantity: 100m);

        var eval = await _replenishmentService.EvaluateWarehouseStockAsync(warehouseId, [record]);
        var proposalId = eval.GeneratedProposals.Single().ProposalId;

        // First dispatch succeeds
        await _replenishmentService.DispatchPurchaseOrderAsync(proposalId);

        // Second dispatch fails gracefully
        var second = await _replenishmentService.DispatchPurchaseOrderAsync(proposalId);
        second.Success.Should().BeFalse();
        second.Message.Should().Contain("Cannot dispatch");
    }

    [Fact]
    public async Task DispatchPurchaseOrder_WhenNonExistentProposal_ThrowsKeyNotFoundException()
    {
        var nonExistentId = Guid.NewGuid();
        var act = async () => await _replenishmentService.DispatchPurchaseOrderAsync(nonExistentId);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AcknowledgePurchaseOrder_UpdatesStatusToAcknowledgedAndIncrementsOnOrderStock()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-ACK-TEST",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 5m,
            LeadTimeStdDev: 1m,
            CurrentStock: 20m,
            OnOrderStock: 0m,
            SafetyStock: 20m,
            ReorderPoint: 70m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 40m,
            EconomicOrderQuantity: 150m);

        var eval = await _replenishmentService.EvaluateWarehouseStockAsync(warehouseId, [record]);
        var proposal = eval.GeneratedProposals.Single();

        // Must dispatch before acknowledging
        await _replenishmentService.DispatchPurchaseOrderAsync(proposal.ProposalId);

        var edd = DateTime.UtcNow.AddDays(7);
        var ackResponse = await _replenishmentService.AcknowledgePurchaseOrderAsync(
            proposal.ProposalId, edd, "Order accepted by factory");

        ackResponse.Success.Should().BeTrue();
        ackResponse.Proposal.Status.Should().Be(PoStatus.Acknowledged);
        ackResponse.Proposal.EstimatedDeliveryDate.Should().Be(edd);

        // Verify that warehouse catalog on-order stock was automatically incremented
        var catalogSkus = _replenishmentService.GetWarehouseSkus(warehouseId);
        var updatedRecord = catalogSkus.Single(s => s.Sku == "SKU-ACK-TEST");
        updatedRecord.OnOrderStock.Should().Be(proposal.Quantity);
    }

    [Fact]
    public async Task AcknowledgePurchaseOrder_WhenProposalInProposedStatus_ReturnsFailure()
    {
        var warehouseId = Guid.NewGuid();
        var record = new SkuVelocityRecord(
            Sku: "SKU-ACK-INVALID",
            WarehouseId: warehouseId,
            AverageDailyDemand: 10m,
            DemandStdDev: 2m,
            LeadTimeDays: 5m,
            LeadTimeStdDev: 1m,
            CurrentStock: 20m,
            OnOrderStock: 0m,
            SafetyStock: 20m,
            ReorderPoint: 70m,
            UnitHoldingCost: 2m,
            OrderSetupCost: 40m,
            EconomicOrderQuantity: 100m);

        var eval = await _replenishmentService.EvaluateWarehouseStockAsync(warehouseId, [record]);
        var proposal = eval.GeneratedProposals.Single();

        // Attempt acknowledging without dispatching first
        var response = await _replenishmentService.AcknowledgePurchaseOrderAsync(
            proposal.ProposalId, DateTime.UtcNow.AddDays(3));

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Expected 'Dispatched'");
    }

    #endregion

    #region 4. JIT Cross-Docking & Outbound Courier Staging

    [Fact]
    public void AllocateInboundReceipt_WithExactBackorderMatch_AllocatesFullQuantityDirectToStaging()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-CROSSDOCK-EXACT",
            ReceivedQuantity: 100m,
            InboundDockBay: "BAY-IN-01",
            ReceivedAtUtc: DateTime.UtcNow);

        var backorder1 = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Customer Alpha",
            Sku: "SKU-CROSSDOCK-EXACT",
            BackorderedQuantity: 60m,
            TargetCarrier: "Yalidine",
            PriorityLevel: 1);

        var backorder2 = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Customer Beta",
            Sku: "SKU-CROSSDOCK-EXACT",
            BackorderedQuantity: 40m,
            TargetCarrier: "Yalidine",
            PriorityLevel: 2);

        var response = _crossDockService.AllocateInboundReceipt(receipt, [backorder1, backorder2]);

        response.TotalReceived.Should().Be(100m);
        response.TotalAllocated.Should().Be(100m);
        response.RemainingToPutaway.Should().Be(0m);
        response.Allocations.Should().HaveCount(2);

        // Verification of direct staging (no putaway)
        response.Allocations.Should().OnlyContain(a => a.Status == CrossDockStatus.Staged);
        response.StagingManifests.Should().NotBeEmpty();
    }

    [Fact]
    public void AllocateInboundReceipt_WhenReceiptExceedsBackorders_RoutesRemainderToPutaway()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-PUTAWAY-SURPLUS",
            ReceivedQuantity: 150m,
            InboundDockBay: "BAY-IN-02",
            ReceivedAtUtc: DateTime.UtcNow);

        var backorder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Customer Gamma",
            Sku: "SKU-PUTAWAY-SURPLUS",
            BackorderedQuantity: 90m,
            TargetCarrier: "ZR_EXPRESS",
            PriorityLevel: 1);

        var response = _crossDockService.AllocateInboundReceipt(receipt, [backorder]);

        response.TotalReceived.Should().Be(150m);
        response.TotalAllocated.Should().Be(90m);
        // 60 units must be put away into regular warehouse storage bins
        response.RemainingToPutaway.Should().Be(60m);
    }

    [Fact]
    public void AllocateInboundReceipt_WhenReceiptIsLessThanBackorders_PerformsPartialFulfillment()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-PARTIAL",
            ReceivedQuantity: 50m, // Only 50 units arrived
            InboundDockBay: "BAY-IN-03",
            ReceivedAtUtc: DateTime.UtcNow);

        var order = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Customer Delta",
            Sku: "SKU-PARTIAL",
            BackorderedQuantity: 120m, // Wants 120 units
            TargetCarrier: "DHL",
            PriorityLevel: 1);

        var response = _crossDockService.AllocateInboundReceipt(receipt, [order]);

        response.TotalReceived.Should().Be(50m);
        response.TotalAllocated.Should().Be(50m);
        response.RemainingToPutaway.Should().Be(0m);

        var allocation = response.Allocations.Single();
        allocation.AllocatedQuantity.Should().Be(50m);

        // Check that remaining 70 units stay in the backorder queue
        var remainingQueue = _crossDockService.GetPendingBackorders("SKU-PARTIAL");
        remainingQueue.Single().BackorderedQuantity.Should().Be(70m);
    }

    [Fact]
    public void AllocateInboundReceipt_RespectsPriorityHierarchy_FulfillsPriority1BeforePriority2()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-PRIORITY-TEST",
            ReceivedQuantity: 80m,
            InboundDockBay: "BAY-IN-04",
            ReceivedAtUtc: DateTime.UtcNow);

        // VIP Order (Priority 1) arrived recently
        var vipOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "VIP Enterprise Client",
            Sku: "SKU-PRIORITY-TEST",
            BackorderedQuantity: 50m,
            TargetCarrier: "FedEx",
            PriorityLevel: 1,
            OrderDateUtc: DateTime.UtcNow);

        // Standard Order (Priority 3) arrived earlier
        var standardOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Standard Buyer",
            Sku: "SKU-PRIORITY-TEST",
            BackorderedQuantity: 50m,
            TargetCarrier: "FedEx",
            PriorityLevel: 3,
            OrderDateUtc: DateTime.UtcNow.AddDays(-5));

        var response = _crossDockService.AllocateInboundReceipt(receipt, [standardOrder, vipOrder]);

        response.TotalAllocated.Should().Be(80m);
        response.Allocations.Should().HaveCount(2);

        // First allocation must be the VIP order for full 50
        response.Allocations[0].OutboundOrderId.Should().Be(vipOrder.OrderId);
        response.Allocations[0].AllocatedQuantity.Should().Be(50m);

        // Second allocation gets remainder (30)
        response.Allocations[1].OutboundOrderId.Should().Be(standardOrder.OrderId);
        response.Allocations[1].AllocatedQuantity.Should().Be(30m);
    }

    [Fact]
    public void AllocateInboundReceipt_ForSamePriority_EnforcesFifoOrderDateMatching()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-FIFO-TEST",
            ReceivedQuantity: 60m,
            InboundDockBay: "BAY-IN-05",
            ReceivedAtUtc: DateTime.UtcNow);

        var earlierOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Older Order",
            Sku: "SKU-FIFO-TEST",
            BackorderedQuantity: 40m,
            TargetCarrier: "Yalidine",
            PriorityLevel: 2,
            OrderDateUtc: DateTime.UtcNow.AddDays(-3));

        var laterOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Later Order",
            Sku: "SKU-FIFO-TEST",
            BackorderedQuantity: 40m,
            TargetCarrier: "Yalidine",
            PriorityLevel: 2,
            OrderDateUtc: DateTime.UtcNow.AddDays(-1));

        var response = _crossDockService.AllocateInboundReceipt(receipt, [laterOrder, earlierOrder]);

        // Older order must be serviced first
        response.Allocations[0].OutboundOrderId.Should().Be(earlierOrder.OrderId);
        response.Allocations[0].AllocatedQuantity.Should().Be(40m);

        response.Allocations[1].OutboundOrderId.Should().Be(laterOrder.OrderId);
        response.Allocations[1].AllocatedQuantity.Should().Be(20m);
    }

    [Fact]
    public void AllocateInboundReceipt_AssignsCorrectDockBayForCarrier()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-BAY-ROUTING",
            ReceivedQuantity: 100m,
            InboundDockBay: "BAY-IN-01",
            ReceivedAtUtc: DateTime.UtcNow);

        var yalidineOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Yalidine Client",
            Sku: "SKU-BAY-ROUTING",
            BackorderedQuantity: 50m,
            TargetCarrier: "Yalidine");

        var response = _crossDockService.AllocateInboundReceipt(receipt, [yalidineOrder]);

        var alloc = response.Allocations.Single();
        alloc.DockBay.Should().Be("BAY-OUT-YALIDINE-01");
    }

    [Fact]
    public void AllocateInboundReceipt_GeneratesCarrierGroupedStagingManifestsWithWaybills()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-MULTI-CARRIER",
            ReceivedQuantity: 120m,
            InboundDockBay: "BAY-IN-01",
            ReceivedAtUtc: DateTime.UtcNow);

        var dhlOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Global Express",
            Sku: "SKU-MULTI-CARRIER",
            BackorderedQuantity: 50m,
            TargetCarrier: "DHL",
            PriorityLevel: 1);

        var zrOrder = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Domestic Fast",
            Sku: "SKU-MULTI-CARRIER",
            BackorderedQuantity: 50m,
            TargetCarrier: "ZR_EXPRESS",
            PriorityLevel: 1);

        var response = _crossDockService.AllocateInboundReceipt(receipt, [dhlOrder, zrOrder]);

        response.StagingManifests.Should().HaveCount(2);
        response.StagingManifests.Should().Contain(m => m.TargetCarrier == "DHL" && m.TotalAllocatedUnits == 50m);
        response.StagingManifests.Should().Contain(m => m.TargetCarrier == "ZR_EXPRESS" && m.TotalAllocatedUnits == 50m);

        foreach (var manifest in response.StagingManifests)
        {
            manifest.CourierWaybillReference.Should().StartWith("MANIFEST-");
            manifest.Allocations.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void UpdateAllocationStatus_UpdatesStatusFromStagedToLoaded()
    {
        var receipt = new InboundReceipt(
            InboundPoId: Guid.NewGuid(),
            Sku: "SKU-STATUS-TRANSITION",
            ReceivedQuantity: 30m,
            InboundDockBay: "BAY-IN-01",
            ReceivedAtUtc: DateTime.UtcNow);

        var order = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(),
            CustomerName: "Stage Transition Customer",
            Sku: "SKU-STATUS-TRANSITION",
            BackorderedQuantity: 30m,
            TargetCarrier: "Yalidine");

        var response = _crossDockService.AllocateInboundReceipt(receipt, [order]);
        var alloc = response.Allocations.Single();
        alloc.Status.Should().Be(CrossDockStatus.Staged);

        // Driver loads the cargo into the van
        var updated = _crossDockService.UpdateAllocationStatus(alloc.AllocationId, CrossDockStatus.Loaded);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(CrossDockStatus.Loaded);
    }

    [Fact]
    public void GetPendingBackorders_FilteredBySku_ReturnsOnlyMatchingWaitingOrders()
    {
        var order1 = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(), CustomerName: "Buyer 1", Sku: "SKU-TARGET", BackorderedQuantity: 10m, TargetCarrier: "DHL");
        var order2 = new CustomerBackorderOrder(
            OrderId: Guid.NewGuid(), CustomerName: "Buyer 2", Sku: "SKU-OTHER", BackorderedQuantity: 20m, TargetCarrier: "DHL");

        _crossDockService.EnqueueBackorders([order1, order2]);

        var filtered = _crossDockService.GetPendingBackorders("SKU-TARGET");
        filtered.Should().HaveCount(1);
        filtered.Single().Sku.Should().Be("SKU-TARGET");
    }

    #endregion

    #region 5. Dependency Injection Registration & Endpoint Architecture

    [Fact]
    public void AddSupplyChainReplenishmentServices_RegistersAllCoreServicesAsSingletons_WithoutCallingBuildServiceProvider()
    {
        // Explicit requirement: Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSupplyChainReplenishmentServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISupplyDemandForecastingEngine) &&
            d.ImplementationType == typeof(SupplyDemandForecastingEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISupplierAutoReplenishmentService) &&
            d.ImplementationType == typeof(SupplierAutoReplenishmentService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICrossDockingAllocationService) &&
            d.ImplementationType == typeof(CrossDockingAllocationService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void SupplyChainEngineHealth_ContainsExpectedCapabilityMetadata()
    {
        var health = new SupplyChainEngineHealth(
            Status: "Operational",
            Version: "1.0.0-phase21",
            TimestampUtc: DateTime.UtcNow,
            Capabilities: new Dictionary<string, string>
            {
                ["Forecasting"] = "Holt-Winters Triple Exponential Smoothing (Additive)",
                ["SafetyStockModel"] = "Dual-Variability Normal SLA (d_bar, sigma_d, L_bar, sigma_L)",
                ["Replenishment"] = "ROP + SafetyStock Deficit / Wilson EOQ Dynamic Sizing",
                ["CrossDocking"] = "JIT Inbound-to-Outbound Dock Transfer & Staging Manifests"
            });

        health.Status.Should().Be("Operational");
        health.Capabilities.Should().ContainKey("Forecasting");
        health.Capabilities.Should().ContainKey("SafetyStockModel");
        health.Capabilities.Should().ContainKey("Replenishment");
        health.Capabilities.Should().ContainKey("CrossDocking");
    }

    #endregion
}
