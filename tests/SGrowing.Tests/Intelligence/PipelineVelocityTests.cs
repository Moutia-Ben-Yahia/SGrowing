using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Intelligence.PipelineVelocity;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class PipelineVelocityTests
{
    private readonly PipelineVelocityEngine _engine = new();

    [Fact]
    public void GetOverview_CalculatesPipelineVelocityMetrics()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalActiveDeals.Should().Be(133);
        overview.TotalPipelineValue.Should().Be(3850000m);
        overview.DailyVelocityRate.Should().BeGreaterThan(0);
        overview.Stages.Should().HaveCount(5);
        overview.Stages.Should().Contain(s => s.StageId == "stg_security" && s.IsBottleneck);
        overview.Stages.Should().BeInAscendingOrder(s => s.StageId);
    }

    [Fact]
    public void AccelerateStage_ReducesDwellDaysAndComputesLift()
    {
        var stageId = "stg_security";
        var before = _engine.GetOverview().Stages.Find(s => s.StageId == stageId)!;

        var request = new AccelerateStageRequest(stageId, "AutomatedSoc2ReportDispatcher");
        var result = _engine.AccelerateStage(request);

        result.Should().NotBeNull();
        result.NewDwellDays.Should().BeLessThan(before.AverageDwellDays);
        result.ProjectedVelocityLift.Should().BeGreaterThan(0);

        var after = _engine.GetOverview().Stages.Find(s => s.StageId == stageId)!;
        after.AverageDwellDays.Should().Be(result.NewDwellDays);
    }

    [Fact]
    public void AccelerateStage_ThrowsWhenStageNotFound()
    {
        var action = () => _engine.AccelerateStage(new AccelerateStageRequest("unknown_stage", "Action"));

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void AccelerateStage_MultipleTimes_CompressesDwellDaysTowardsBenchmark()
    {
        var stageId = "stg_demo"; // Initial dwell 16.5, benchmark 12.0
        var req = new AccelerateStageRequest(stageId, "InteractivePreRecordedSandbox");

        // Accelerate twice
        _engine.AccelerateStage(req);
        var finalResult = _engine.AccelerateStage(req);

        finalResult.NewDwellDays.Should().BeLessThanOrEqualTo(14.5);
        finalResult.NewDwellDays.Should().BeGreaterThanOrEqualTo(12.0); // Floored at benchmark
    }

    [Fact]
    public void AccelerateStage_CaseInsensitiveLookup_Succeeds()
    {
        // Arrange
        var req = new AccelerateStageRequest("STG_DISCOVERY", "FastTrackQualification");

        // Act
        var result = _engine.AccelerateStage(req);

        // Assert
        result.StageId.Should().Be("STG_DISCOVERY");
        result.NewDwellDays.Should().Be(10.0); // Benchmark is 10.0, initial was 12.0, reduction min 2.0
    }

    [Fact]
    public void AccelerateStage_ClearsBottleneckFlag_WhenDwellDaysDropBelowThreshold()
    {
        // Arrange: stg_procurement initial dwell 24.5, benchmark 15.0, bottleneck is true (24.5 > 15 * 1.2 = 18.0)
        var stageId = "stg_procurement";
        var req = new AccelerateStageRequest(stageId, "DocuSignExpressWorkflow");

        // Act: Multiple accelerations to push dwell days below 18.0 (15.0 * 1.2)
        _engine.AccelerateStage(req);
        _engine.AccelerateStage(req);
        _engine.AccelerateStage(req);

        var overview = _engine.GetOverview();
        var stage = overview.Stages.Find(s => s.StageId == stageId);

        // Assert
        stage.Should().NotBeNull();
        stage!.AverageDwellDays.Should().BeLessThanOrEqualTo(18.0); // Benchmark is 15.0, 15.0 * 1.2 = 18.0
        stage.IsBottleneck.Should().BeFalse("stage dwell days now within 120% of benchmark");
    }

    [Fact]
    public void GetOverview_DailyVelocityRecalculatesAccurately()
    {
        // Act
        var overview = _engine.GetOverview();

        // Assert: DailyVelocityRate = (Deals * WinRate% * ACV) / Days
        var avgCycleDays = overview.Stages.Sum(s => s.AverageDwellDays);
        overview.AverageSalesCycleDays.Should().Be(Math.Round(avgCycleDays, 1));
        overview.DailyVelocityRate.Should().BeGreaterThan(0);
        overview.OverallWinRatePercentage.Should().Be(28.5);
    }

    [Fact]
    public void AccelerateStage_CalculatesProjectedVelocityLiftCorrectly()
    {
        // Arrange: stg_closing dwell 7.5, benchmark 7.0, difference is 0.5, reductionDays = Max(2.0, 0.25) = 2.0
        var req = new AccelerateStageRequest("stg_closing", "ContractCloserAI");

        // Act
        var result = _engine.AccelerateStage(req);

        // Assert: reductionDays = 2.0, projectedLift = (decimal)(2.0 * 1250.0) = 2500m
        result.ProjectedVelocityLift.Should().Be(2500m);
        result.ActionLog.Should().Contain("compressed by 2.0 days");
    }
}
