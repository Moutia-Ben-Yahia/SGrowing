using FluentAssertions;
using SGrowing.Api.Modules.Voice.EmotionDeescalation;
using Xunit;

namespace SGrowing.Tests.Voice;

public class EmotionDeescalationTests
{
    private readonly EmotionDeescalationEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveTelemetryAndEscalations()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveCallsMonitoredCount.Should().Be(2);
        overview.DeescalationSuccessRatePct.Should().BeGreaterThan(85.0);
        overview.PendingSupervisorEscalationsCount.Should().Be(1);
        overview.ActiveTelemetry.Should().HaveCount(2);
        overview.RecentEscalations.Should().HaveCount(1);
    }

    [Fact]
    public void AnalyzeAcoustics_HighPitchAndVelocityFlagsElevatedFrustration()
    {
        var request = new AnalyzeAcousticsRequest(
            "CALL-TEST-999",
            "AGT-EMMA-01",
            "dGVzdGF1ZGlvY2h1bms=",
            185.0, // High pitch
            230.0, // Fast speech
            "This is the third time this week our billing integration stopped working!"
        );

        var result = _engine.AnalyzeAcoustics(request);

        result.Should().NotBeNull();
        result.CallId.Should().Be("CALL-TEST-999");
        result.FrustrationLevel.Should().BeGreaterThan(0.7);
        result.RequiresUrgentDeescalation.Should().BeTrue();
        result.SuggestedCalmingResponseScript.Should().Contain("personal ownership");
        result.LatencyMs.Should().BeLessThan(150);
    }

    [Fact]
    public void TriggerSupervisorHandoff_CreatesTransferReceiptWithBriefcase()
    {
        var request = new SupervisorHandoffRequest(
            "CALL-TEST-999",
            "SUP-SARAH-LEAD",
            "Customer requesting executive contract cancellation",
            "1-month free service credit + priority SLA"
        );

        var receipt = _engine.TriggerSupervisorHandoff(request);

        receipt.Should().NotBeNull();
        receipt.TransferId.Should().StartWith("TRF-");
        receipt.Status.Should().Be("WARM_HANDOFF_CONNECTED");
        receipt.ContextBriefcaseId.Should().StartWith("BRIEF-");
    }
}
