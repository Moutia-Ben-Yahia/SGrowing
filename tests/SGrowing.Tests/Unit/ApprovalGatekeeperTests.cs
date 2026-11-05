using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class ApprovalGatekeeperTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Theory]
    [InlineData("SchemaMutation", 0, ApprovalRiskLevel.Critical)]
    [InlineData("DropTable", 0, ApprovalRiskLevel.Critical)]
    [InlineData("AdSpendAdjustment", 550, ApprovalRiskLevel.High)]
    [InlineData("AdSpendAdjustment", 50, ApprovalRiskLevel.Low)]
    [InlineData("WholesaleDiscountOverride", 30, ApprovalRiskLevel.High)]
    [InlineData("WholesaleDiscountOverride", 10, ApprovalRiskLevel.Medium)]
    [InlineData("AdSpendAdjustment", 200, ApprovalRiskLevel.Medium)]
    public void CalculateRiskLevel_CategorizesCorrectly(string opType, decimal impact, ApprovalRiskLevel expectedRisk)
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);

        var risk = service.CalculateRiskLevel(opType, impact);

        risk.Should().Be(expectedRisk);
    }

    [Fact]
    public void CalculateRiskLevel_WithBulkCampaign_EvaluatesRecipientCount()
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);
        var payloadHigh = JsonSerializer.Serialize(new { recipientCount = 10_000 });
        var payloadLow = JsonSerializer.Serialize(new { recipientCount = 500 });

        var riskHigh = service.CalculateRiskLevel("BulkCampaignDispatch", 50, payloadHigh);
        var riskLow = service.CalculateRiskLevel("BulkCampaignDispatch", 50, payloadLow);

        riskHigh.Should().Be(ApprovalRiskLevel.High);
        riskLow.Should().Be(ApprovalRiskLevel.Medium);
    }

    [Fact]
    public async Task EvaluateAndRegisterActionAsync_LowRisk_AutoApprovesWithoutToken()
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);
        var tenantId = Guid.NewGuid();

        var decision = await service.EvaluateAndRegisterActionAsync(
            tenantId,
            "AdSpendAdjustment",
            "Minor budget update",
            40m,
            "{}"
        );

        decision.RequiresHumanApproval.Should().BeFalse();
        decision.Status.Should().Be(ToolApprovalStatus.AutoApproved);
        decision.OneTimeApprovalToken.Should().BeNull();

        var persisted = await service.GetApprovalByIdAsync(decision.RequestId);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ToolApprovalStatus.AutoApproved);
    }

    [Fact]
    public async Task EvaluateAndRegisterActionAsync_HighRisk_GeneratesCryptographicTokenAndPendingState()
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);
        var tenantId = Guid.NewGuid();

        var decision = await service.EvaluateAndRegisterActionAsync(
            tenantId,
            "AdSpendAdjustment",
            "Scale campaign budget to 1500 USD",
            1500m,
            "{}"
        );

        decision.RequiresHumanApproval.Should().BeTrue();
        decision.Status.Should().Be(ToolApprovalStatus.Pending);
        decision.OneTimeApprovalToken.Should().NotBeNullOrWhiteSpace();
        decision.OneTimeApprovalToken!.Length.Should().Be(64); // 32 bytes hex = 64 characters

        var pendingList = await service.GetPendingApprovalsAsync(tenantId);
        pendingList.Should().ContainSingle(p => p.Id == decision.RequestId);
    }

    [Fact]
    public async Task ApproveWithTokenAsync_WithValidToken_ApprovesSuccessfully()
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);
        var tenantId = Guid.NewGuid();

        var decision = await service.EvaluateAndRegisterActionAsync(
            tenantId,
            "AdSpendAdjustment",
            "Critical budget increase",
            1200m,
            "{}"
        );

        var verifyResult = await service.ApproveWithTokenAsync(
            decision.RequestId,
            decision.OneTimeApprovalToken!,
            "user_lead_growth_director"
        );

        verifyResult.Success.Should().BeTrue();
        verifyResult.Status.Should().Be(ToolApprovalStatus.Approved);

        var persisted = await service.GetApprovalByIdAsync(decision.RequestId);
        persisted!.Status.Should().Be(ToolApprovalStatus.Approved);
        persisted.ApprovedByUserId.Should().Be("user_lead_growth_director");
        persisted.DecidedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveWithTokenAsync_WithForgedToken_RejectsVerification()
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);
        var tenantId = Guid.NewGuid();

        var decision = await service.EvaluateAndRegisterActionAsync(
            tenantId,
            "WholesaleDiscountOverride",
            "Override enterprise contract pricing by 40%",
            40m,
            "{}"
        );

        var fakeToken = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var verifyResult = await service.ApproveWithTokenAsync(
            decision.RequestId,
            fakeToken,
            "attacker"
        );

        verifyResult.Success.Should().BeFalse();
        verifyResult.Message.Should().Contain("failed");

        var persisted = await service.GetApprovalByIdAsync(decision.RequestId);
        persisted!.Status.Should().Be(ToolApprovalStatus.Pending);
    }

    [Fact]
    public async Task RejectActionAsync_TransitionsToRejectedWithReason()
    {
        using var db = CreateDbContext();
        var service = new ApprovalGatekeeperService(db);
        var tenantId = Guid.NewGuid();

        var decision = await service.EvaluateAndRegisterActionAsync(
            tenantId,
            "AdSpendAdjustment",
            "Huge budget leap",
            3000m,
            "{}"
        );

        var rejectResult = await service.RejectActionAsync(
            decision.RequestId,
            "compliance_officer",
            "CPA exceeds acceptable target"
        );

        rejectResult.Success.Should().BeTrue();
        rejectResult.Status.Should().Be(ToolApprovalStatus.Rejected);

        var persisted = await service.GetApprovalByIdAsync(decision.RequestId);
        persisted!.Status.Should().Be(ToolApprovalStatus.Rejected);
        persisted.DecisionReason.Should().Be("CPA exceeds acceptable target");
        persisted.ApprovedByUserId.Should().Be("compliance_officer");
    }
}
