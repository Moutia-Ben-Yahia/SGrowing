using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.AI.Evaluation;
using Xunit;

namespace SGrowing.Tests.Unit;

public class AiAgentEvaluationTests
{
    private readonly AiAgentEvaluationService _service;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public AiAgentEvaluationTests()
    {
        _service = new AiAgentEvaluationService();
    }

    [Fact]
    public void GetGlobalRadarReport_ReturnsExpectedSeededReportAndKpis()
    {
        // Act
        var report = _service.GetGlobalRadarReport(_testTenantId);

        // Assert
        report.Should().NotBeNull();
        report.AgentScorecards.Should().HaveCount(5);

        // KPI checks matching PRD Section 6.6 & 14
        report.FullyAutonomousAgentCount.Should().Be(3); // ResearchAgent, SeoAgent, ApprovalGatekeeper
        report.SupervisedAgentCount.Should().Be(2);     // AdvertisingAgent, ContentAgent
        report.AverageCostPerTaskSar.Should().Be(6.44m); // (4.20 + 6.50 + 12.00 + 8.40 + 1.10) / 5
        report.GlobalScore.Should().BeApproximately(94.2, 0.5); // Seeded around 94.2%
        report.TotalTasksEvaluated.Should().Be(11600); // 2400 + 1850 + 920 + 1540 + 4890

        // Top rejection reasons clustering
        report.TopRejectionReasons.Should().NotBeEmpty();
        report.TopRejectionReasons.First().Should().Be("Unverified pricing claim against ERP price list");
    }

    [Fact]
    public void GetAgentScorecard_ContainsAllFivePrimaryAgentRoles()
    {
        // 1. ResearchAgent
        var research = _service.GetAgentScorecard(_testTenantId, "ResearchAgent");
        research.Should().NotBeNull();
        research!.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Execute);
        research.TaskSuccessRate.Should().Be(98.0);
        research.HallucinationIndex.Should().Be(0.02);
        research.CostPerTaskSar.Should().Be(4.20m);
        research.Trend.Should().Be(RegressionTrend.Improving);

        // 2. SeoAgent
        var seo = _service.GetAgentScorecard(_testTenantId, "SeoAgent");
        seo.Should().NotBeNull();
        seo!.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Execute);
        seo.TaskSuccessRate.Should().Be(96.0);
        seo.HallucinationIndex.Should().Be(0.04);
        seo.CostPerTaskSar.Should().Be(6.50m);
        seo.Trend.Should().Be(RegressionTrend.Stable);

        // 3. AdvertisingAgent
        var ads = _service.GetAgentScorecard(_testTenantId, "AdvertisingAgent");
        ads.Should().NotBeNull();
        ads!.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Approve);
        ads.TaskSuccessRate.Should().Be(92.0);
        ads.HallucinationIndex.Should().Be(0.08);
        ads.CostPerTaskSar.Should().Be(12.00m);
        ads.ApprovalRate.Should().Be(88.0);

        // 4. ContentAgent
        var content = _service.GetAgentScorecard(_testTenantId, "ContentAgent");
        content.Should().NotBeNull();
        content!.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Approve);
        content.TaskSuccessRate.Should().Be(94.0);
        content.HallucinationIndex.Should().Be(0.05);
        content.CostPerTaskSar.Should().Be(8.40m);

        // 5. ApprovalGatekeeper
        var gatekeeper = _service.GetAgentScorecard(_testTenantId, "ApprovalGatekeeper");
        gatekeeper.Should().NotBeNull();
        gatekeeper!.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Execute);
        gatekeeper.ToolPrecisionRate.Should().Be(100.0);
        gatekeeper.HallucinationIndex.Should().Be(0.00);
        gatekeeper.CostPerTaskSar.Should().Be(1.10m);
        gatekeeper.CompositeScore.Should().Be(100.0);
    }

    [Fact]
    public void RecordEvaluation_UpdatesAgentVitalsAndTrendReactively()
    {
        // Arrange
        var request = new RecordEvaluationRequest(
            TenantId: _testTenantId,
            AgentRole: "ResearchAgent",
            ModelVersion: "gemini-2.5-pro",
            PromptVersion: "v3.1.2",
            WasSuccessful: true,
            ToolExecutionValid: true,
            HumanApproved: true,
            HadHallucination: false,
            CostSar: 4.10m,
            LatencyMs: 750.0
        );

        // Act
        var updated = _service.RecordEvaluation(request);

        // Assert
        updated.Should().NotBeNull();
        updated.AgentRole.Should().Be("ResearchAgent");
        updated.CostPerTaskSar.Should().BeApproximately(4.20m, 0.05m);

        var report = _service.GetGlobalRadarReport(_testTenantId);
        report.TotalTasksEvaluated.Should().Be(11601);
    }

    [Fact]
    public void RejectionReasonClustering_AggregatesAndRanksFrequentIssues()
    {
        // Record 3 recurring rejections for policy violations
        for (var i = 0; i < 50; i++)
        {
            _service.RecordEvaluation(new RecordEvaluationRequest(
                TenantId: _testTenantId,
                AgentRole: "AdvertisingAgent",
                ModelVersion: "gemini-2.5-pro",
                PromptVersion: "v2.0",
                WasSuccessful: false,
                ToolExecutionValid: true,
                HumanApproved: false,
                HadHallucination: false,
                CostSar: 10.0m,
                LatencyMs: 900.0,
                RejectionReason: "Exceeded daily budget limit in riyals"
            ));
        }

        var report = _service.GetGlobalRadarReport(_testTenantId);
        report.TopRejectionReasons.Should().Contain("Exceeded daily budget limit in riyals");
        report.TopRejectionReasons.First().Should().Be("Exceeded daily budget limit in riyals");
    }

    [Fact]
    public void EvaluateAutonomyPromotion_DraftToApprove_WhenCompositeExceeds80()
    {
        // Arrange: create a new custom agent at Draft with composite > 80
        var newRole = "GrowthStrategistAgent";
        for (var i = 0; i < 10; i++)
        {
            _service.RecordEvaluation(new RecordEvaluationRequest(
                TenantId: _testTenantId,
                AgentRole: newRole,
                ModelVersion: "claude-3-7-sonnet",
                PromptVersion: "v1.0",
                WasSuccessful: true,
                ToolExecutionValid: true,
                HumanApproved: true,
                HadHallucination: false,
                CostSar: 5.0m,
                LatencyMs: 600.0
            ));
        }

        var initial = _service.GetAgentScorecard(_testTenantId, newRole);
        initial!.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Draft);
        initial.CompositeScore.Should().BeGreaterThan(80.0);

        // Act
        var checkResult = _service.EvaluateAutonomyPromotion(_testTenantId, newRole);

        // Assert
        checkResult.LevelChanged.Should().BeTrue();
        checkResult.CurrentLevel.Should().Be(AutonomyReadinessLevel.Draft);
        checkResult.RecommendedLevel.Should().Be(AutonomyReadinessLevel.Approve);
        checkResult.Scorecard.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Approve);
    }

    [Fact]
    public void EvaluateAutonomyPromotion_ApproveToExecute_WhenCompositeAbove90AndApprovalAbove95()
    {
        // Arrange: ContentAgent starts at Approve with approval ~86%.
        // Push human approvals to exceed 95% (3200 runs with 100% approval)
        for (var i = 0; i < 3200; i++)
        {
            _service.RecordEvaluation(new RecordEvaluationRequest(
                TenantId: _testTenantId,
                AgentRole: "ContentAgent",
                ModelVersion: "gemini-2.5-flash",
                PromptVersion: "v2.5",
                WasSuccessful: true,
                ToolExecutionValid: true,
                HumanApproved: true,
                HadHallucination: false,
                CostSar: 7.50m,
                LatencyMs: 800.0
            ));
        }

        var candidate = _service.GetAgentScorecard(_testTenantId, "ContentAgent");
        candidate!.CompositeScore.Should().BeGreaterThan(90.0);
        candidate.ApprovalRate.Should().BeGreaterThan(95.0);

        // Act
        var result = _service.EvaluateAutonomyPromotion(_testTenantId, "ContentAgent");

        // Assert
        result.LevelChanged.Should().BeTrue();
        result.CurrentLevel.Should().Be(AutonomyReadinessLevel.Approve);
        result.RecommendedLevel.Should().Be(AutonomyReadinessLevel.Execute);
        result.Scorecard.AutonomyLevel.Should().Be(AutonomyReadinessLevel.Execute);
    }

    [Fact]
    public void EvaluateAutonomyPromotion_DemotesFromExecuteToApprove_WhenQualityDegrades()
    {
        // Arrange: SeoAgent is at Execute. Simulate sudden errors and rejections.
        for (var i = 0; i < 1500; i++)
        {
            _service.RecordEvaluation(new RecordEvaluationRequest(
                TenantId: _testTenantId,
                AgentRole: "SeoAgent",
                ModelVersion: "bad-model-checkpoint",
                PromptVersion: "v-buggy",
                WasSuccessful: false,
                ToolExecutionValid: false,
                HumanApproved: false,
                HadHallucination: true,
                CostSar: 8.0m,
                LatencyMs: 3000.0
            ));
        }

        // Act
        var result = _service.EvaluateAutonomyPromotion(_testTenantId, "SeoAgent");

        // Assert
        result.LevelChanged.Should().BeTrue();
        result.CurrentLevel.Should().Be(AutonomyReadinessLevel.Execute);
        result.RecommendedLevel.Should().Be(AutonomyReadinessLevel.Approve);
        result.Reason.Should().Contain("Demoted from Execute to Approve");
    }

    [Fact]
    public void MultiTenantIsolation_EnsuresIndependentEvaluationRegistries()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _service.RecordEvaluation(new RecordEvaluationRequest(
            TenantId: tenantA,
            AgentRole: "CustomBot",
            ModelVersion: "gpt-4.5",
            PromptVersion: "v1",
            WasSuccessful: true,
            ToolExecutionValid: true,
            HumanApproved: true,
            HadHallucination: false,
            CostSar: 1.0m,
            LatencyMs: 100.0
        ));

        _service.GetAgentScorecard(tenantA, "CustomBot").Should().NotBeNull();
        _service.GetAgentScorecard(tenantB, "CustomBot").Should().BeNull();
    }
}
