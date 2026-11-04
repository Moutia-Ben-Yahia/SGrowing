using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.AgentWhisper;
using Xunit;

namespace SGrowing.Tests.Voice;

public class AgentWhisperTests
{
    private readonly AgentWhisperEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveSessionsAndGuardrails()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveCallsCount.Should().Be(3);
        overview.TotalWhispersDispatchedToday.Should().BeGreaterThan(1000);
        overview.ConcessionAdherenceRatePct.Should().BeGreaterThan(90.0);
        overview.DealWinRateLiftPct.Should().BeGreaterThan(20.0);
        overview.ActiveSessions.Should().HaveCount(3);
        overview.Guardrails.Should().NotBeEmpty();
    }

    [Fact]
    public void AnalyzeSpeechTurn_DetectsPriceObjectionAndDispatchesLowLatencyWhisper()
    {
        var command = new AnalyzeSpeechTurnCommand("CALL-8841", "Buyer", "Your software seems really expensive compared to our budget.", 3200);
        var result = _engine.AnalyzeSpeechTurn(command);

        result.Should().NotBeNull();
        result.DetectedIntent.Should().Be("PriceSensitivity");
        result.RecommendedWhisperText.Should().Contain("multi-year tier");
        result.MaxConcessionAllowedPct.Should().Be(12.0m);
        result.LatencyDispatchedMs.Should().BeLessThan(200); // Sub-200ms latency guarantee
    }

    [Fact]
    public void AnalyzeSpeechTurn_DetectsFeatureInquiry_WhenNoPriceKeywordsPresent()
    {
        // Arrange
        var command = new AnalyzeSpeechTurnCommand("CALL-8842", "Buyer", "How does your platform integrate with HubSpot and Salesforce?", 2100);

        // Act
        var result = _engine.AnalyzeSpeechTurn(command);

        // Assert
        result.DetectedIntent.Should().Be("FeatureInquiry");
        result.RecommendedWhisperText.Should().Contain("native bi-directional CRM syncing");
        result.MaxConcessionAllowedPct.Should().Be(0.0m);
    }

    [Fact]
    public void AnalyzeSpeechTurn_GeneratesUniqueTurnIds()
    {
        // Arrange & Act
        var r1 = _engine.AnalyzeSpeechTurn(new AnalyzeSpeechTurnCommand("CALL-8841", "Buyer", "Price too high", 1000));
        var r2 = _engine.AnalyzeSpeechTurn(new AnalyzeSpeechTurnCommand("CALL-8841", "Buyer", "Price too high", 1000));

        // Assert
        r1.TurnAnalysisId.Should().StartWith("TURN-");
        r2.TurnAnalysisId.Should().StartWith("TURN-");
        r1.TurnAnalysisId.Should().NotBe(r2.TurnAnalysisId);
    }

    [Fact]
    public void CalculateConcession_ApprovesConcessionWithinThreshold()
    {
        var command = new CalculateConcessionCommand("CALL-8841", 10.0m, 45);
        var result = _engine.CalculateConcession(command);

        result.Should().NotBeNull();
        result.Status.Should().Be("Approved_Autonomous");
        result.ApprovedDiscountPct.Should().Be(10.0m);
        result.ApprovedPaymentDays.Should().Be(45);
    }

    [Fact]
    public void CalculateConcession_DefaultsTo45Days_WhenZeroOrNegativeDaysProvided()
    {
        // Arrange
        var command = new CalculateConcessionCommand("CALL-8841", 8.0m, 0);

        // Act
        var result = _engine.CalculateConcession(command);

        // Assert
        result.Status.Should().Be("Approved_Autonomous");
        result.ApprovedDiscountPct.Should().Be(8.0m);
        result.ApprovedPaymentDays.Should().Be(45, "zero or negative days should fallback to standard 45-day default");
    }

    [Fact]
    public void CalculateConcession_CountersExcessiveDiscount()
    {
        var command = new CalculateConcessionCommand("CALL-8841", 22.0m, 60);
        var result = _engine.CalculateConcession(command);

        result.Should().NotBeNull();
        result.Status.Should().Be("CounterOffer_Recommended");
        result.ApprovedDiscountPct.Should().Be(12.0m);
        result.ApprovedPaymentDays.Should().Be(60);
        result.RationaleNarrative.Should().Contain("exceeds AE limits");
    }

    [Fact]
    public void CalculateConcession_UnknownCallSessionId_FallsBackToDefaultSession()
    {
        // Arrange: Unknown CallSessionId
        var command = new CalculateConcessionCommand("CALL-UNKNOWN-9999", 5.0m, 30);

        // Act
        var result = _engine.CalculateConcession(command);

        // Assert
        result.Status.Should().Be("Approved_Autonomous");
        result.ApprovedDiscountPct.Should().Be(5.0m);
        result.ApprovedPaymentDays.Should().Be(30);
    }
}
