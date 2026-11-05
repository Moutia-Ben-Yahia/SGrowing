using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Api.Modules.Analytics;
using Xunit;

namespace SGrowing.Tests.Unit;

public class AutonomousCampaignLoopTests
{
    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task AuditCampaignRoasAsync_DetectsUnderperformingCampaign()
    {
        using var db = CreateDbContext();
        var olap = new ClickHouseAnalyticsProjectionService();
        var gatekeeper = new ApprovalGatekeeperService(db);
        var swarm = new AutonomousSwarmPersistenceService(db, gatekeeper);
        var loopService = new AutonomousCampaignLoopDaemonService(olap, swarm, gatekeeper);

        var tenantId = Guid.NewGuid();

        // Seed low ROAS ad spend in ClickHouse OLAP: Spend $200, Revenue $100 -> ROAS 0.5 (< 2.5)
        await olap.IngestBatchAsync(new List<ClickHouseEventRecord>
        {
            new(Guid.NewGuid(), tenantId, "ad.click", DateTime.UtcNow.AddDays(-1), "meta", "camp_meta_1", null, 200m, 0m, "{}"),
            new(Guid.NewGuid(), tenantId, "purchase", DateTime.UtcNow.AddDays(-1), "meta", "camp_meta_1", null, 0m, 100m, "{}")
        });

        var alerts = await loopService.AuditCampaignRoasAsync(tenantId, minTargetRoas: 2.5m);

        Assert.NotEmpty(alerts);
        var metaAlert = alerts.FirstOrDefault(a => a.Channel == "meta");
        Assert.NotNull(metaAlert);
        Assert.Equal(0.5000m, metaAlert.CurrentRoas);
        Assert.Equal(200m, metaAlert.AdSpend);
    }

    [Fact]
    public async Task ExecuteInterventionAsync_GatesHighBudgetAdjustmentWithCryptographicToken()
    {
        using var db = CreateDbContext();
        var olap = new ClickHouseAnalyticsProjectionService();
        var gatekeeper = new ApprovalGatekeeperService(db);
        var swarm = new AutonomousSwarmPersistenceService(db, gatekeeper);
        var loopService = new AutonomousCampaignLoopDaemonService(olap, swarm, gatekeeper);

        var tenantId = Guid.NewGuid();

        var alert = new CampaignRoasThresholdAlert(
            TenantId: tenantId,
            Channel: "meta",
            CampaignId: "camp_meta_auto_perf",
            CurrentRoas: 0.8m,
            TargetRoas: 2.5m,
            AdSpend: 400m,
            Revenue: 320m,
            DetectedAtUtc: DateTime.UtcNow
        );

        var plan = await loopService.ExecuteInterventionAsync(alert);

        Assert.NotNull(plan);
        Assert.True(plan.RequiresHitlApproval);
        Assert.Equal("PausedForApproval", plan.InterventionStatus);
        Assert.NotNull(plan.ApprovalToken);
        Assert.NotNull(plan.ApprovalRequestId);

        // Verify cryptographic approval completes plan
        var approved = await loopService.ApproveAndPublishInterventionAsync(tenantId, plan.MissionId, plan.ApprovalToken);
        Assert.True(approved);
    }
}
