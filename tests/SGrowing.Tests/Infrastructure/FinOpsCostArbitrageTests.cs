using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Infrastructure.FinOps;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class FinOpsCostArbitrageTests
{
    private readonly FinOpsCostAttributionEngine _attributionEngine = new();
    private readonly SpotInstanceEvictionGraceHandler _spotHandler = new();
    private readonly AutonomousRightSizingRecommender _rightSizingRecommender = new();

    #region Cost Attribution Engine Tests

    [Fact]
    public void GenerateStatement_CalculatesAccurateItemizedCostsAndMargin()
    {
        var usages = new List<TenantResourceUsage>
        {
            new("U1", "TEN-ORAN-01", "OrderProcessingService", CpuMillicoresHours: 10000.0, MemoryGigabyteHours: 2000.0, StorageGigabytes: 100.0, EgressGigabytes: 50.0, DateTime.UtcNow),
            new("U2", "TEN-ORAN-01", "CatalogSearchService", CpuMillicoresHours: 5000.0, MemoryGigabyteHours: 1000.0, StorageGigabytes: 50.0, EgressGigabytes: 25.0, DateTime.UtcNow)
        };

        var request = new GenerateCostStatementRequest("TEN-ORAN-01", "2026-11", usages, MarginMultiplier: 1.30m);
        var stmt = _attributionEngine.GenerateStatement(request);

        stmt.TenantId.Should().Be("TEN-ORAN-01");
        stmt.ComputeCostDzd.Should().Be(15000.0m * 0.045m); // 675.00
        stmt.MemoryCostDzd.Should().Be(3000.0m * 0.018m);   // 54.00
        stmt.StorageCostDzd.Should().Be(150.0m * 12.00m);   // 1800.00
        stmt.NetworkEgressCostDzd.Should().Be(75.0m * 8.50m); // 637.50
        stmt.TotalCostDzd.Should().Be(stmt.ComputeCostDzd + stmt.MemoryCostDzd + stmt.StorageCostDzd + stmt.NetworkEgressCostDzd);
        stmt.AllocatedPriceWithMarginDzd.Should().Be(Math.Round(stmt.TotalCostDzd * 1.30m, 2));
        stmt.GrossMarginDzd.Should().Be(stmt.AllocatedPriceWithMarginDzd - stmt.TotalCostDzd);
    }

    [Fact]
    public void GenerateStatement_EmptyTenantId_ThrowsArgumentException()
    {
        var act = () => _attributionEngine.GenerateStatement(new GenerateCostStatementRequest(" ", "2026-11", []));
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Spot Instance Graceful Eviction Tests

    [Fact]
    public void HandlePreemptionNotice_RegistersDrainingAlertWith120SecondDeadline()
    {
        var alert = _spotHandler.HandlePreemptionNotice(
            nodeId: "i-09941a2b3c4d-spot",
            provider: CloudProvider.Aws,
            region: "af-south-1",
            workloadIds: ["pod-payment-bridge-01", "pod-rate-limiter-02"],
            gracePeriodSeconds: 120
        );

        alert.Status.Should().Be(EvictionMigrationStatus.Draining);
        alert.TerminationDeadlineUtc.Should().BeAfter(alert.NoticeReceivedAtUtc);
    }

    [Fact]
    public void ConfirmWorkloadEvacuation_WithinDeadline_MarksSuccessfullyEvacuated()
    {
        var alert = _spotHandler.HandlePreemptionNotice("node-azure-spot-01", CloudProvider.Azure, "germanywestcentral", ["pod-1"]);

        // Complete evacuation 30 seconds after notice (well within 120s deadline)
        var evacuationTime = alert.NoticeReceivedAtUtc.AddSeconds(30);
        var confirmed = _spotHandler.ConfirmWorkloadEvacuation(alert.AlertId, evacuationTime);

        confirmed.Status.Should().Be(EvictionMigrationStatus.EvacuatedSuccessfully);
        confirmed.CompletedAtUtc.Should().Be(evacuationTime);
    }

    [Fact]
    public void ConfirmWorkloadEvacuation_PastDeadline_MarksTerminatedWithDrop()
    {
        var alert = _spotHandler.HandlePreemptionNotice("node-gcp-preempt-01", CloudProvider.Gcp, "europe-west1", ["pod-batch-01"]);

        // Evacuation completed 130s after notice (10s past 120s deadline)
        var lateTime = alert.NoticeReceivedAtUtc.AddSeconds(130);
        var confirmed = _spotHandler.ConfirmWorkloadEvacuation(alert.AlertId, lateTime);

        confirmed.Status.Should().Be(EvictionMigrationStatus.TerminatedWithDrop);
    }

    [Fact]
    public void ConfirmWorkloadEvacuation_InvalidAlertId_ThrowsKeyNotFoundException()
    {
        var act = () => _spotHandler.ConfirmWorkloadEvacuation("ALERT-NON-EXISTENT");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetMigrationSuccessMetrics_TracksCumulativeRatio()
    {
        var alert = _spotHandler.HandlePreemptionNotice("node-test-metrics", CloudProvider.Aws, "us-east-1", ["pod-metrics"]);
        _spotHandler.ConfirmWorkloadEvacuation(alert.AlertId);

        var (ratio, total, success) = _spotHandler.GetMigrationSuccessMetrics();
        total.Should().BeGreaterThan(0);
        success.Should().Be(1);
        ratio.Should().Be(1.0);
    }

    #endregion

    #region Autonomous Right-Sizing Recommender Tests

    [Fact]
    public void AnalyzeWorkload_OverprovisionedWorkload_GeneratesDownsizingSavings()
    {
        // 8 CPU cores allocated, but P99 is only 15% peak!
        // 32 GB RAM allocated, but P99 is only 25% peak!
        var overprovisioned = new WorkloadUsageMetrics(
            WorkloadId: "WL-LEGACY-API",
            ServiceName: "LegacyCustomerPortal",
            CurrentAllocatedCpuCores: 8.0,
            CurrentAllocatedMemoryGb: 32.0,
            P99CpuUtilizationPercent: 15.0,
            P99MemoryUtilizationPercent: 25.0,
            CurrentHourlyCostDzd: 180.0m
        );

        var rec = _rightSizingRecommender.AnalyzeWorkload(overprovisioned);

        rec.IsOverprovisioned.Should().BeTrue();
        rec.RecommendedCpuCores.Should().BeLessThan(8.0);
        rec.RecommendedMemoryGb.Should().BeLessThan(32.0);
        rec.MonthlySavingsDzd.Should().BeGreaterThan(0m);
        rec.SavingsPercentage.Should().BeGreaterThan(30.0);
        rec.OptimizedMonthlyCostDzd.Should().BeLessThan(rec.CurrentMonthlyCostDzd);
    }

    [Fact]
    public void AnalyzeWorkload_WellUtilizedWorkload_RecommendsNoChangesWithZeroSavings()
    {
        // Highly utilized: P99 CPU 85%, P99 RAM 90%
        var wellUtilized = new WorkloadUsageMetrics(
            WorkloadId: "WL-OPTIMAL-PAYMENT",
            ServiceName: "CorePaymentGateway",
            CurrentAllocatedCpuCores: 4.0,
            CurrentAllocatedMemoryGb: 16.0,
            P99CpuUtilizationPercent: 85.0,
            P99MemoryUtilizationPercent: 90.0,
            CurrentHourlyCostDzd: 120.0m
        );

        var rec = _rightSizingRecommender.AnalyzeWorkload(wellUtilized);

        rec.IsOverprovisioned.Should().BeFalse();
        rec.RecommendedCpuCores.Should().Be(4.0);
        rec.RecommendedMemoryGb.Should().Be(16.0);
        rec.MonthlySavingsDzd.Should().Be(0m);
        rec.SavingsPercentage.Should().Be(0.0);
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddFinOpsArbitrageServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddFinOpsArbitrageServices();

        services.Should().Contain(d => d.ServiceType == typeof(FinOpsCostAttributionEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(SpotInstanceEvictionGraceHandler) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(AutonomousRightSizingRecommender) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
