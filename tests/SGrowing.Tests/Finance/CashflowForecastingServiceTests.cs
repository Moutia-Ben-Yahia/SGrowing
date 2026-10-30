using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Finance;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Finance;

public class CashflowForecastingServiceTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CashflowForecastingTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void CalculateWorkingCapitalMetrics_ShouldCalculateDsoDpoDioAndCccAccurately()
    {
        // Arrange
        var service = new CashflowForecastingService();
        decimal receivables = 62900.0m;
        decimal payables = 56000.0m;
        decimal inventory = 65000.0m;
        decimal creditSales = 214000.0m;
        decimal cogs = 132000.0m;
        decimal courierFloat = 36800.0m;
        decimal overdueReceivables = 10300.0m;
        decimal cashReserve = 42500.0m;

        // Act
        var metrics = service.CalculateWorkingCapitalMetrics(
            receivables, payables, inventory, creditSales, cogs, courierFloat, overdueReceivables, cashReserve);

        // Assert
        // DSO = (62900 / 214000) * 90 = 26.45... -> 26.5 or 26.4
        metrics.DSO.Should().BeApproximately(26.5m, 0.5m);
        // DPO = (56000 / 132000) * 90 = 38.18... -> 38.2
        metrics.DPO.Should().BeApproximately(38.2m, 0.5m);
        // DIO = (65000 / 132000) * 90 = 44.31... -> 44.3
        metrics.DIO.Should().BeApproximately(44.3m, 0.5m);
        // CCC = DIO + DSO - DPO
        metrics.CashConversionCycle.Should().Be(metrics.DIO + metrics.DSO - metrics.DPO);

        // Trapped Liquidity Ratio
        metrics.TrappedLiquidityRatio.Should().BeGreaterThan(0m);
        metrics.TrappedLiquidityRatio.Should().BeLessThan(100m);
    }

    [Fact]
    public void CalculateWorkingCapitalMetrics_ShouldHandleZeroOrNegativeValuesGracefully()
    {
        // Arrange
        var service = new CashflowForecastingService();

        // Act
        var metrics = service.CalculateWorkingCapitalMetrics(
            receivables: 0m,
            payables: 0m,
            inventory: 0m,
            creditSales: 0m,
            cogs: 0m,
            courierFloat: 0m,
            overdueReceivables: 0m,
            cashReserve: 0m);

        // Assert - No division by zero exceptions, clean zero return
        metrics.DSO.Should().Be(0m);
        metrics.DPO.Should().Be(0m);
        metrics.DIO.Should().Be(0m);
        metrics.CashConversionCycle.Should().Be(0m);
        metrics.TrappedLiquidityRatio.Should().Be(0m);
    }

    [Fact]
    public void CalculateWeibullClearanceSchedule_ShouldConserveTotalAmountAndPeakNearMeanTurnaround()
    {
        // Arrange
        var service = new CashflowForecastingService();
        decimal netAmount = 14036.00m;
        double meanTurnaround = 5.0; // 5 days (e.g. Aramex)

        // Act
        var schedule = service.CalculateWeibullClearanceSchedule(netAmount, meanTurnaround, 14, 2.0);

        // Assert
        schedule.Should().HaveCount(14);
        var totalCleared = schedule.Sum(s => s.ExpectedAmount);
        totalCleared.Should().Be(netAmount, "Sum of daily cleared amounts must exactly match the net amount to the cent");

        // The peak day should be around day 4, 5, or 6
        var peakShare = schedule.OrderByDescending(s => s.ExpectedAmount).First();
        peakShare.DayOffset.Should().BeInRange(4, 6, "Weibull peak should align with courier average turnaround");
    }

    [Fact]
    public void CalculateWeibullClearanceSchedule_FasterCourierClearsEarlierThanSlowerCourier()
    {
        // Arrange
        var service = new CashflowForecastingService();
        decimal amount = 10000m;

        // Act: DHL turnaround 3 days vs Local Courier turnaround 8 days
        var dhlSchedule = service.CalculateWeibullClearanceSchedule(amount, 3.0, 14);
        var localSchedule = service.CalculateWeibullClearanceSchedule(amount, 8.0, 14);

        // Assert
        var dhlDay3Cumulative = dhlSchedule.Where(s => s.DayOffset <= 3).Sum(s => s.ExpectedAmount);
        var localDay3Cumulative = localSchedule.Where(s => s.DayOffset <= 3).Sum(s => s.ExpectedAmount);

        dhlDay3Cumulative.Should().BeGreaterThan(localDay3Cumulative, "DHL should clear substantially more capital by day 3 than local courier");

        var dhlPeakDay = dhlSchedule.OrderByDescending(s => s.ExpectedAmount).First().DayOffset;
        var localPeakDay = localSchedule.OrderByDescending(s => s.ExpectedAmount).First().DayOffset;
        dhlPeakDay.Should().BeLessThan(localPeakDay);
    }

    [Fact]
    public void CalculateWeibullClearanceSchedule_ShouldHandleZeroOrNegativeAmount()
    {
        // Arrange
        var service = new CashflowForecastingService();

        // Act
        var schedule = service.CalculateWeibullClearanceSchedule(0m, 5.0, 14);

        // Assert
        schedule.Should().HaveCount(14);
        schedule.Sum(s => s.ExpectedAmount).Should().Be(0m);
    }

    [Fact]
    public async Task GetReceivablesPayablesAging_ShouldCategorizeInvoicesIntoCorrectAgingBuckets()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act
        var aging = await service.GetReceivablesPayablesAgingAsync(tenantId);

        // Assert
        aging.DetailedReceivables.Should().NotBeEmpty();
        aging.DetailedPayables.Should().NotBeEmpty();
        aging.ReceivablesBuckets.Should().HaveCount(4);

        var currentBucket = aging.ReceivablesBuckets.First(b => b.BucketName.Contains("Current"));
        currentBucket.Amount.Should().BeGreaterThan(0m);
        currentBucket.CollectionProbability.Should().Be(0.95m);

        var overdue60Bucket = aging.ReceivablesBuckets.First(b => b.BucketName.Contains("60+"));
        overdue60Bucket.CollectionProbability.Should().Be(0.30m);
    }

    [Fact]
    public async Task GetReceivablesPayablesAging_ShouldApplyCollectionProbabilitiesAndCalculateNetTradePosition()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act
        var aging = await service.GetReceivablesPayablesAgingAsync(tenantId);

        // Assert
        aging.TotalReceivables.Should().Be(aging.DetailedReceivables.Sum(r => r.Amount));
        aging.TotalPayables.Should().Be(aging.DetailedPayables.Sum(p => p.Amount));
        aging.NetWorkingCapitalTradePosition.Should().Be(aging.TotalReceivables - aging.TotalPayables);

        foreach (var rec in aging.DetailedReceivables)
        {
            rec.ExpectedRecoveryAmount.Should().Be(rec.Amount * rec.CollectionProbability);
        }
    }

    [Fact]
    public async Task GenerateProjection_ShouldSimulate90DayCashTrajectoryAccurately()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act
        var points = await service.GenerateProjectionAsync(tenantId, 90, 15000m);

        // Assert
        points.Should().HaveCount(90);
        points[0].DayIndex.Should().Be(1);
        points[0].OpeningBalance.Should().Be(CashflowForecastingService.DefaultStartingReserve);

        // Continuity check: Point(n).ClosingBalance == Point(n+1).OpeningBalance
        for (int i = 0; i < points.Count - 1; i++)
        {
            points[i].ClosingBalance.Should().Be(points[i + 1].OpeningBalance, $"Day {points[i].DayIndex} closing must match Day {points[i + 1].DayIndex} opening");
            points[i].NetDailyFlow.Should().Be(points[i].TotalInflow - points[i].TotalOutflow);
        }
    }

    [Fact]
    public async Task GenerateProjection_ShouldReflectCustomHorizonDays()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act & Assert 30 days
        var points30 = await service.GenerateProjectionAsync(tenantId, 30);
        points30.Should().HaveCount(30);

        // Act & Assert 60 days
        var points60 = await service.GenerateProjectionAsync(tenantId, 60);
        points60.Should().HaveCount(60);
    }

    [Fact]
    public async Task GenerateProjection_ShouldDetectDeficitDaysWhenCashFallsBelowSafetyThreshold()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();
        decimal highSafetyThreshold = 60000.0m; // Very high threshold guarantees deficits

        // Act
        var points = await service.GenerateProjectionAsync(tenantId, 90, highSafetyThreshold);

        // Assert
        var deficitPoints = points.Where(p => p.IsDeficit).ToList();
        deficitPoints.Should().NotBeEmpty("A threshold of 60,000 should flag deficit days when starting balance is 42,500");
        deficitPoints.All(p => p.ClosingBalance < highSafetyThreshold).Should().BeTrue();
        deficitPoints.All(p => p.DeficitGap > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetSummary_ShouldReturnHighLevelFloatCashPositionAndDeficitSeverity()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act
        var summary = await service.GetSummaryAsync(tenantId, 15000m);

        // Assert
        summary.CurrentLiquidReserve.Should().Be(CashflowForecastingService.DefaultStartingReserve);
        summary.TotalInTransitCourierFloat.Should().BeGreaterThan(0m);
        summary.TotalAccountsReceivable.Should().BeGreaterThan(0m);
        summary.TotalAccountsPayable.Should().BeGreaterThan(0m);
        summary.CashConversionCycle.Should().BeGreaterThan(0m);
        summary.TopCouriers.Should().NotBeEmpty();
        summary.DeficitSeverity.Should().BeOneOf("Healthy", "Warning", "Critical");
        summary.LowestBalanceDate.Should().BeAfter(DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task GetCourierFloatBreakdown_ShouldReturnMultiCourierLedgerWithWeibullDistributions()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act
        var couriers = await service.GetCourierFloatBreakdownAsync(tenantId);

        // Assert
        couriers.Should().HaveCount(4);
        couriers.Should().Contain(c => c.CourierName.Contains("Aramex"));
        couriers.Should().Contain(c => c.CourierName.Contains("DHL"));
        couriers.Should().Contain(c => c.CourierName.Contains("Yalidine"));
        couriers.Should().Contain(c => c.CourierName.Contains("FedEx"));

        foreach (var c in couriers)
        {
            c.NetReceivableCash.Should().Be(c.GrossCashValue - c.EstimatedFees);
            c.ClearanceDistribution.Should().HaveCount(14);
            c.ClearanceDistribution.Sum(d => d.ExpectedAmount).Should().Be(c.NetReceivableCash);
        }
    }

    [Fact]
    public async Task SimulateFactoringAdvance_ShouldCalculateDiscountFeeAndInjectImmediateLiquidity()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();
        var req = new FactoringSimulationRequestDto(
            TenantId: tenantId,
            AdvanceAmount: 20000.0m,
            DiscountFeePercentage: 2.5m,
            SourceAssetType: "CourierFloat"
        );

        // Act
        var result = await service.SimulateFactoringAdvanceAsync(req);

        // Assert
        result.RequestedAdvance.Should().Be(20000.0m);
        result.DiscountFeePercentage.Should().Be(2.5m);
        result.FactoringFeeAmount.Should().Be(500.0m); // 20,000 * 0.025 = 500
        result.NetCashInflowImmediate.Should().Be(19500.0m); // 20,000 - 500 = 19,500
        result.TrajectoryComparison.Should().HaveCount(90);

        // Day 1 factored cash should receive immediate liquidity boost
        var day1 = result.TrajectoryComparison.First();
        day1.FactoredCash.Should().BeGreaterThan(day1.BaselineCash);
    }

    [Fact]
    public async Task SimulateFactoringAdvance_ShouldEliminateDeficitDaysAndImproveLowestBalance()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();
        var req = new FactoringSimulationRequestDto(
            TenantId: tenantId,
            AdvanceAmount: 25000.0m,
            DiscountFeePercentage: 2.5m,
            SourceAssetType: "Blended"
        );

        // Act
        var result = await service.SimulateFactoringAdvanceAsync(req);

        // Assert
        result.FactoredLowestBalance.Should().BeGreaterThan(result.BaselineLowestBalance);
        result.NetLiquidityImprovement.Should().BeGreaterThan(0m);
        result.FactoredDeficitDays.Should().BeLessThanOrEqualTo(result.BaselineDeficitDays);
    }

    [Fact]
    public async Task SimulateFactoringAdvance_ShouldOffsetFutureFloatClearanceToPreventDoubleCounting()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();
        var req = new FactoringSimulationRequestDto(
            TenantId: tenantId,
            AdvanceAmount: 15000.0m,
            DiscountFeePercentage: 2.0m,
            ApplyOffsetToProjection: true
        );

        // Act
        var result = await service.SimulateFactoringAdvanceAsync(req);

        // Assert
        result.TrajectoryComparison.Should().NotBeEmpty();
        // Toward day 90, difference between factored and baseline should account for the factoring fee
        var day90 = result.TrajectoryComparison.Last();
        (day90.BaselineCash - day90.FactoredCash).Should().BeApproximately(result.FactoringFeeAmount, 50.0m);
    }

    [Fact]
    public async Task RecordAdjustment_ShouldIncorporateManualInflowAndOutflowIntoCashflow()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();
        var targetDate = DateTime.UtcNow.Date.AddDays(10);

        var reqInflow = new RecordAdjustmentRequestDto(
            TenantId: tenantId,
            DateUtc: targetDate,
            Amount: 12000.0m,
            Category: "TaxRefund",
            Description: "VAT refund from Treasury"
        );

        // Act
        var adj = await service.RecordAdjustmentAsync(reqInflow);
        var stored = await service.GetAdjustmentsAsync(tenantId);
        var projection = await service.GenerateProjectionAsync(tenantId, 30);

        // Assert
        adj.Amount.Should().Be(12000.0m);
        stored.Should().Contain(a => a.Id == adj.Id);

        var targetPoint = projection.FirstOrDefault(p => p.Date.Date == targetDate.Date);
        targetPoint.Should().NotBeNull();
        targetPoint!.InflowOther.Should().BeGreaterThanOrEqualTo(12000.0m);
    }

    [Fact]
    public void EdgeCase_AllReceivablesOverdue_ShouldIncreaseTrappedLiquidityRatio()
    {
        // Arrange
        var service = new CashflowForecastingService();
        decimal normalOverdue = 5000m;
        decimal highOverdue = 45000m;
        decimal cash = 30000m;
        decimal floatVal = 20000m;
        decimal receivables = 50000m;

        // Act
        var normalMetrics = service.CalculateWorkingCapitalMetrics(
            receivables, 20000m, 30000m, 100000m, 60000m, floatVal, normalOverdue, cash);

        var stressedMetrics = service.CalculateWorkingCapitalMetrics(
            receivables, 20000m, 30000m, 100000m, 60000m, floatVal, highOverdue, cash);

        // Assert
        stressedMetrics.TrappedLiquidityRatio.Should().BeGreaterThan(normalMetrics.TrappedLiquidityRatio);
    }

    [Fact]
    public async Task EdgeCase_ZeroCashReserve_ShouldFlagEarlyDeficitDays()
    {
        // Arrange
        var service = new CashflowForecastingService();
        var tenantId = Guid.NewGuid();

        // Act: Run projection with high safety threshold simulating zero-buffer environment
        var projection = await service.GenerateProjectionAsync(tenantId, 90, safetyThreshold: 100000m);

        // Assert
        projection.First().IsDeficit.Should().BeTrue();
        projection.First().DeficitGap.Should().BeGreaterThan(0m);
    }
}
