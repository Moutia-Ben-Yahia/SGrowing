using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.DynamicPricing;
using SGrowing.Api.Modules.CRM.RetentionConcierge;
using SGrowing.Api.Modules.Finance;
using SGrowing.Api.Modules.SEO.TopicalAuthority;
using Xunit;

namespace SGrowing.Tests.Unit;

public class PersistentHorizonEnginesTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task PersistentCashflowForecastingEngine_PersistsForecastAndCalculatesSeverity()
    {
        using var db = CreateInMemoryDb();
        var engine = new PersistentCashflowForecastingEngine(db);
        var tenantId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date.AddDays(7);

        var point = await engine.RecordForecastPointAsync(
            tenantId: tenantId,
            forecastDateUtc: date,
            projectedCashBalance: 45000m,
            netBurnRate: 2000m,
            runwayDays: 22,
            confidenceLower: 40000m,
            confidenceUpper: 50000m);

        point.DeficitSeverity.Should().Be("Warning");
        point.ProjectedCashBalance.Should().Be(45000m);

        // Verify stored in DB
        var persisted = await db.CashflowForecasts.FirstOrDefaultAsync(f => f.Id == point.Id);
        persisted.Should().NotBeNull();
        persisted!.RunwayDays.Should().Be(22);

        // Get summary
        var summary = await engine.GetTenantForecastSummaryAsync(tenantId, horizonDays: 14);
        summary.TenantId.Should().Be(tenantId);
        summary.OverallSeverity.Should().Be("Warning");
        summary.Trajectory.Should().Contain(p => p.Id == point.Id);
    }

    [Fact]
    public async Task PersistentTopicalAuthorityEngine_UpsertsClusterAndRetrievesFromJson()
    {
        using var db = CreateInMemoryDb();
        var engine = new PersistentTopicalAuthorityEngine(db);
        var tenantId = Guid.NewGuid();

        var cluster = await engine.UpsertClusterAsync(
            tenantId: tenantId,
            clusterId: "cluster_growth_ai",
            pillarTopic: "Autonomous B2B Growth Engine",
            targetKeyword: "b2b growth automation",
            authorityScore: 88.5,
            totalSubArticles: 14,
            missingSubTopicsCount: 2,
            coveredEntities: new[] { "CDP Identity Resolution", "Dynamic Funnels" },
            recommendedExpansions: new[] { "Account-Based Marketing orchestration" });

        cluster.Should().NotBeNull();
        cluster.TopicalAuthorityScore.Should().Be(88.5);

        // Retrieve DTOs
        var dtos = await engine.GetClustersAsync(tenantId);
        dtos.Should().HaveCount(1);
        dtos[0].ClusterId.Should().Be("cluster_growth_ai");
        dtos[0].CoveredEntities.Should().Contain("CDP Identity Resolution");
        dtos[0].RecommendedExpansions.Should().Contain("Account-Based Marketing orchestration");
    }

    [Fact]
    public async Task PersistentPriceElasticityEngine_CalculatesOptimalPriceAndPersists()
    {
        using var db = CreateInMemoryDb();
        var engine = new PersistentPriceElasticityEngine(db);
        var tenantId = Guid.NewGuid();

        // Elastic demand: elasticity = -1.5, basePrice = 100, baseDemand = 1000, unitCost = 30
        var model = await engine.ComputeAndPersistOptimalPriceAsync(
            tenantId: tenantId,
            productId: "PROD-ELASTIC-01",
            basePrice: 100m,
            baseDemand: 1000m,
            unitCost: 30m,
            elasticity: -1.5m);

        model.ElasticityCategory.Should().Be("Elastic");
        model.OptimalPrice.Should().BeGreaterThan(30m);
        model.MaxContributionMargin.Should().BeGreaterThan(0);

        // Verify retrieval via L1 / DB
        var retrieved = await engine.GetProductModelAsync(tenantId, "PROD-ELASTIC-01");
        retrieved.Should().NotBeNull();
        retrieved!.OptimalPrice.Should().Be(model.OptimalPrice);
    }

    [Fact]
    public async Task PersistentRetentionBanditService_SeedsArmsAndUpdatesAlphaBetaRewards()
    {
        using var db = CreateInMemoryDb();
        var service = new PersistentRetentionBanditService(db);
        var tenantId = Guid.NewGuid();

        // 1. Initial selection seeds default arms
        var selection = await service.SelectBestIncentiveArmAsync(tenantId);
        selection.Should().NotBeNull();
        selection.ArmId.Should().NotBeNullOrWhiteSpace();

        var initialAlpha = selection.Alpha;
        var initialBeta = selection.Beta;

        // 2. Record success reward
        await service.RecordRewardAsync(tenantId, selection.ArmId, success: true, retainedLtvDzd: 15000m);

        var armsAfterSuccess = await service.GetArmStatesAsync(tenantId);
        var updatedArm = armsAfterSuccess.First(a => a.ArmId == selection.ArmId);
        updatedArm.Alpha.Should().Be(initialAlpha + 1.0);
        updatedArm.TotalLtvRetainedDzd.Should().Be(15000m);
        updatedArm.TotalTrials.Should().Be(1);

        // 3. Record failure
        await service.RecordRewardAsync(tenantId, selection.ArmId, success: false);
        var armsAfterFailure = await service.GetArmStatesAsync(tenantId);
        var finalArm = armsAfterFailure.First(a => a.ArmId == selection.ArmId);
        finalArm.Beta.Should().Be(initialBeta + 1.0);
        finalArm.TotalTrials.Should().Be(2);
    }
}
