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

        finalResult.NewDwellDays.Should().BeLessThanOrEqualTo(14.0);
        finalResult.NewDwellDays.Should().BeGreaterThanOrEqualTo(12.0); // Floored at benchmark
    }
}
