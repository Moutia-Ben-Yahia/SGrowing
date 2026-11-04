using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Support.ScreenDiagnostic;
using Xunit;

namespace SGrowing.Tests.Support;

public class ScreenDiagnosticTests
{
    private readonly ScreenDiagnosticEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveSessionsAndElements()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveSessionsCount.Should().Be(3);
        overview.ElementDetectionAccuracyPct.Should().BeGreaterThan(90.0);
        overview.AutonomousGuidanceSuccessPct.Should().BeGreaterThan(85.0);
        overview.Sessions.Should().HaveCount(3);
        overview.HighlightedElements.Should().NotBeEmpty();
    }

    [Fact]
    public void AnalyzeFrame_DetectsStickingPointAndRecommendsAction()
    {
        var command = new AnalyzeFrameCommand("SESS-SCR-401", "HASH-F48A99", 1920, 1080);
        var result = _engine.AnalyzeFrame(command);

        result.Should().NotBeNull();
        result.SessionId.Should().Be("SESS-SCR-401");
        result.DetectedElements.Should().NotBeEmpty();
        result.RecommendedAction.Should().Contain("Save Callback URL");
        result.ConfidencePct.Should().BeGreaterThanOrEqualTo(90);
    }

    [Fact]
    public void GenerateTrajectory_ComputesSmoothBezierWaypoints()
    {
        var command = new GenerateTrajectoryCommand("SESS-SCR-401", 100, 100, "UI-EL-02");
        var result = _engine.GenerateTrajectory(command);

        result.Should().NotBeNull();
        result.TargetElementId.Should().Be("UI-EL-02");
        result.Waypoints.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Waypoints.Should().Contain(w => w.InterpolationType == "CubicBezier");
        result.Waypoints.Should().Contain(w => w.Action == "Click");
    }

    [Fact]
    public void GenerateTrajectory_SetsDurationAndCoordinatesProperly()
    {
        var command = new GenerateTrajectoryCommand("SESS-SCR-402", 200, 300, "UI-EL-01");
        var result = _engine.GenerateTrajectory(command);

        result.Should().NotBeNull();
        result.TrajectoryId.Should().StartWith("TRAJ-");
        result.TotalDurationMs.Should().Be(850);
        result.SessionId.Should().Be("SESS-SCR-402");
        result.Waypoints.First().TargetX.Should().Be(200);
        result.Waypoints.First().TargetY.Should().Be(300);
    }

    [Fact]
    public void AnalyzeFrame_GeneratesUniqueAnalysisIdPerInvocation()
    {
        var cmd1 = new AnalyzeFrameCommand("SESS-SCR-401", "H1", 1280, 720);
        var cmd2 = new AnalyzeFrameCommand("SESS-SCR-401", "H2", 1280, 720);

        var res1 = _engine.AnalyzeFrame(cmd1);
        var res2 = _engine.AnalyzeFrame(cmd2);

        res1.AnalysisId.Should().StartWith("ANLZ-");
        res2.AnalysisId.Should().StartWith("ANLZ-");
        res1.AnalysisId.Should().NotBe(res2.AnalysisId);
    }
}
