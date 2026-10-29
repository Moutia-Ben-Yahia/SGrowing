using FluentAssertions;
using SGrowing.Api.Modules.Sales;
using Xunit;

namespace SGrowing.Tests.Sales;

public class PredictiveCadenceServiceTests
{
    private static PredictiveCadenceService CreateService()
    {
        return new PredictiveCadenceService(null);
    }

    [Fact]
    public async Task Get7x24Heatmap_GeneratesExactly168HourlyProbabilitySlots()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var heatmap = await service.Get7x24HeatmapAsync(tenantId, "UTC");

        // Assert
        heatmap.Should().NotBeNull();
        heatmap.Slots.Should().HaveCount(168, "7 days * 24 hours must yield exactly 168 probability slots");

        heatmap.Slots.All(s => s.OverallProbabilityScore >= 0.0 && s.OverallProbabilityScore <= 100.0)
            .Should().BeTrue("all probability scores must fall within [0, 100]%");

        heatmap.Slots.All(s => s.ChannelScores.ContainsKey(PredictiveCadenceChannel.WhatsApp) &&
                               s.ChannelScores.ContainsKey(PredictiveCadenceChannel.Phone) &&
                               s.ChannelScores.ContainsKey(PredictiveCadenceChannel.Email))
            .Should().BeTrue("each slot must include multi-channel breakdown scores");

        heatmap.PeakSlot.Should().NotBeNull();
        heatmap.PeakSlot!.OverallProbabilityScore.Should().BeGreaterThan(70.0);
    }

    [Theory]
    [InlineData("UTC+1", 60)]
    [InlineData("UTC-5", -300)]
    [InlineData("+03:00", 180)]
    [InlineData("-04:00", -240)]
    [InlineData("UTC", 0)]
    public void ParseTimeZoneOffset_ParsesStandardOffsetsCorrectly(string tzInput, int expectedMinutes)
    {
        // Act
        var offset = PredictiveCadenceService.ParseTimeZoneOffset(tzInput);

        // Assert
        offset.TotalMinutes.Should().Be(expectedMinutes);
    }

    [Fact]
    public async Task Get7x24Heatmap_TranslatesQuietHoursAccuratelyForOffsetTimezone()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act - Request heatmap for UTC-5
        var heatmapUtcMinus5 = await service.Get7x24HeatmapAsync(tenantId, "UTC-5");

        // Assert
        heatmapUtcMinus5.UtcOffsetMinutes.Should().Be(-300);

        // Hours 0..6 (12 AM - 6 AM local time) should be flagged as quiet hours with low probability
        var nightSlots = heatmapUtcMinus5.Slots.Where(s => s.HourOfDay < 7).ToList();
        nightSlots.Should().NotBeEmpty();
        nightSlots.All(s => s.IsQuietHours).Should().BeTrue();
        nightSlots.All(s => s.OverallProbabilityScore <= 20.0).Should().BeTrue();
    }

    [Fact]
    public void CalculateChannelPrior_PhonePeaksDuringBusinessHoursAndDropsAtNight()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        // Act
        // Tuesday at 10 AM (Peak Phone Window)
        var tuesdayMorningUtc = new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);
        var peakScore = service.CalculateInstantaneousScore(tenantId, leadId, PredictiveCadenceChannel.Phone, "UTC", tuesdayMorningUtc);

        // Tuesday at 2 AM (Night off-hours)
        var tuesdayNightUtc = new DateTime(2026, 10, 27, 2, 0, 0, DateTimeKind.Utc);
        var nightScore = service.CalculateInstantaneousScore(tenantId, leadId, PredictiveCadenceChannel.Phone, "UTC", tuesdayNightUtc);

        // Assert
        peakScore.Should().BeGreaterThan(75.0, "Tuesday 10 AM is a golden phone connect window");
        nightScore.Should().BeLessThan(20.0, "2 AM local time should be damped to off-hours score");
    }

    [Fact]
    public async Task GetOptimalContactSlots_RanksTopUpcomingSlotsByConfidenceExcludingQuietHours()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var referenceTime = new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc); // Monday 8 AM

        // Act
        var recommendations = await service.GetOptimalContactSlotsAsync(
            tenantId,
            leadId,
            PredictiveCadenceChannel.Phone,
            "UTC",
            limit: 3,
            referenceTimeUtc: referenceTime);

        // Assert
        recommendations.Should().HaveCount(3);
        recommendations[0].ConfidenceScore.Should().BeGreaterThanOrEqualTo(recommendations[1].ConfidenceScore);
        recommendations[1].ConfidenceScore.Should().BeGreaterThanOrEqualTo(recommendations[2].ConfidenceScore);

        // None should be during quiet hours (local hour must be between 8 and 19)
        recommendations.All(r => r.HourOfDay >= 8 && r.HourOfDay < 20).Should().BeTrue();
        recommendations.All(r => !string.IsNullOrWhiteSpace(r.Reasoning)).Should().BeTrue();
    }

    [Fact]
    public async Task OptimalContactSlots_BoostsScoreWhenHistoricalLeadActivityMatches()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        // Baseline slot recommendation
        var baselineSlots = await service.GetOptimalContactSlotsAsync(
            tenantId,
            leadId,
            PredictiveCadenceChannel.WhatsApp,
            "UTC",
            limit: 1,
            referenceTimeUtc: new DateTime(2026, 10, 26, 6, 0, 0, DateTimeKind.Utc));

        // Act: Record positive interaction telemetry for this lead at Tuesday 10 AM
        var interactionTime = new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);
        await service.RecordTelemetryEventAsync(tenantId, new RecordTelemetryRequest(
            leadId,
            PredictiveCadenceChannel.WhatsApp,
            TelemetryEventType.WhatsAppReplied,
            interactionTime));

        await service.RecordTelemetryEventAsync(tenantId, new RecordTelemetryRequest(
            leadId,
            PredictiveCadenceChannel.WhatsApp,
            TelemetryEventType.WhatsAppRead,
            interactionTime));

        var boostedScore = service.CalculateInstantaneousScore(tenantId, leadId, PredictiveCadenceChannel.WhatsApp, "UTC", interactionTime);

        // Assert
        boostedScore.Should().BeGreaterThan(80.0, "Multiple positive telemetry signals should boost slot confidence");
    }

    [Fact]
    public async Task CadenceStateMachine_EnrollsLeadAndSchedulesFirstStep()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Create a 3-step cadence
        var cadence = await service.CreateCadenceAsync(tenantId, new CreateCadenceRequest(
            "Fast Qualification",
            "Inbound qualification",
            CadenceCategory.InboundFastResponse,
            new List<CadenceStepDefinition>
            {
                new() { StepNumber = 1, Name = "WhatsApp Intro", Channel = PredictiveCadenceChannel.WhatsApp, DelayFromPrevious = TimeSpan.Zero, UseOptimalTiming = false },
                new() { StepNumber = 2, Name = "Discovery Call", Channel = PredictiveCadenceChannel.Phone, DelayFromPrevious = TimeSpan.FromHours(1), UseOptimalTiming = true },
                new() { StepNumber = 3, Name = "Email Case Study", Channel = PredictiveCadenceChannel.Email, DelayFromPrevious = TimeSpan.FromDays(1), UseOptimalTiming = true }
            }
        ));

        // Act
        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            cadence.Id,
            Guid.NewGuid(),
            "John Doe",
            "john@enterprise.com",
            "+1 555-0100",
            "Enterprise Ltd",
            "UTC+1"
        ));

        // Assert
        enrollment.Should().NotBeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.CurrentStepNumber.Should().Be(1);
        enrollment.NextStepChannel.Should().Be(PredictiveCadenceChannel.WhatsApp);
        enrollment.LeadTimeZone.Should().Be("UTC+1");
        enrollment.NextStepScheduledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task AdvanceStep_ProgressesToSubsequentStepAndLogsHistory()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        var cadences = await service.GetCadencesAsync(PredictiveCadenceService.DefaultDemoTenantId);
        var enterpriseCadence = cadences.First(c => c.Name.Contains("Enterprise SaaS"));

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            enterpriseCadence.Id,
            Guid.NewGuid(),
            "Alice Smith",
            "alice@acme.org",
            "+1 555-0200",
            "Acme Corp",
            "UTC"
        ));

        // Act: Advance Step 1 -> Step 2
        var advanced = await service.AdvanceStepAsync(tenantId, enrollment.Id);

        // Assert
        advanced.Should().NotBeNull();
        advanced!.CurrentStepNumber.Should().Be(2);
        advanced.NextStepChannel.Should().Be(PredictiveCadenceChannel.Phone);
        advanced.ExecutionLogs.Should().ContainSingle(l => l.StepNumber == 1 && l.Status == "Executed");
        advanced.LastStepExecutedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task AdvanceStep_CompletesCadenceWhenFinalStepFinished()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // 1-step cadence
        var singleStepCadence = await service.CreateCadenceAsync(tenantId, new CreateCadenceRequest(
            "Single Touch Flash",
            "Single notification",
            CadenceCategory.InboundFastResponse,
            new List<CadenceStepDefinition>
            {
                new() { StepNumber = 1, Name = "Instant WhatsApp", Channel = PredictiveCadenceChannel.WhatsApp, DelayFromPrevious = TimeSpan.Zero, UseOptimalTiming = false }
            }
        ));

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            singleStepCadence.Id,
            Guid.NewGuid(),
            "Bob Miller",
            "bob@quick.io",
            "+1 555-0300",
            "Quick IO",
            "UTC"
        ));

        // Act: Advance past the final step
        var finished = await service.AdvanceStepAsync(tenantId, enrollment.Id);

        // Assert
        finished.Should().NotBeNull();
        finished!.Status.Should().Be(EnrollmentStatus.Completed);
        finished.NextStepScheduledAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AutoPauseOnReply_AutomaticallyHaltsCadenceWhenReplyTelemetryRecorded()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var cadences = await service.GetCadencesAsync(PredictiveCadenceService.DefaultDemoTenantId);
        var c1 = cadences.First();

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            c1.Id,
            leadId,
            "Samantha Ray",
            "samantha@target.com",
            "+1 555-0400",
            "Target Corp",
            "UTC"
        ));

        enrollment.Status.Should().Be(EnrollmentStatus.Active);

        // Act: Inbound email reply telemetry arrives
        await service.RecordTelemetryEventAsync(tenantId, new RecordTelemetryRequest(
            leadId,
            PredictiveCadenceChannel.Email,
            TelemetryEventType.EmailReply,
            DateTime.UtcNow,
            "{\"subject\": \"Re: Demo request\", \"sentiment\": \"positive\"}"));

        // Assert: Enrollment must be automatically paused / transitioned to Replied
        var updated = await service.GetEnrollmentByIdAsync(tenantId, enrollment.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(EnrollmentStatus.Replied);
        updated.PauseReason.Should().Contain("Prospect replied via Email");
        updated.NextStepScheduledAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task DealWonTelemetry_TransitionsCadenceToDealWonAndGraduates()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var cadences = await service.GetCadencesAsync(PredictiveCadenceService.DefaultDemoTenantId);
        var c1 = cadences.First();

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            c1.Id,
            leadId,
            "Enterprise Prospect",
            "vp@enterpriseglobal.com",
            "+1 555-0500",
            "Enterprise Global",
            "UTC"
        ));

        // Act: Deal Won event
        await service.RecordTelemetryEventAsync(tenantId, new RecordTelemetryRequest(
            leadId,
            PredictiveCadenceChannel.Email,
            TelemetryEventType.DealWon,
            DateTime.UtcNow));

        // Assert
        var updated = await service.GetEnrollmentByIdAsync(tenantId, enrollment.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(EnrollmentStatus.DealWon);
        updated.PauseReason.Should().Contain("Deal marked as Won");
    }

    [Fact]
    public async Task PredictiveDialerQueue_OrdersLeadsByInstantaneousConfidenceScore()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Create 2-step cadence with Step 2 as Phone
        var phoneCadence = await service.CreateCadenceAsync(tenantId, new CreateCadenceRequest(
            "Outbound Phone Blitz",
            "Phone sequence",
            CadenceCategory.EnterpriseOutbound,
            new List<CadenceStepDefinition>
            {
                new() { StepNumber = 1, Name = "Phone Outreach", Channel = PredictiveCadenceChannel.Phone, DelayFromPrevious = TimeSpan.Zero }
            }
        ));

        // Lead 1: Local time at reference is 10 AM (Peak connect) -> UTC timezone
        var lead1 = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            phoneCadence.Id,
            Guid.NewGuid(),
            "High Probability Lead",
            "lead1@corp.com",
            "+1 555-0601",
            "Prime Tech",
            "UTC"
        ));

        // Lead 2: Local time at reference is 1 AM (Off-hours night) -> UTC+15 (or UTC-9)
        var lead2 = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            phoneCadence.Id,
            Guid.NewGuid(),
            "Night Off-Hours Lead",
            "lead2@corp.com",
            "+1 555-0602",
            "Sleep Tech",
            "UTC-9"
        ));

        // Act: Query dialer at Tuesday 10:00 AM UTC
        var tuesday10AmUtc = new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);
        var queue = await service.GetPredictiveDialerQueueAsync(tenantId, limit: 10, referenceTimeUtc: tuesday10AmUtc);

        // Assert
        queue.Should().NotBeEmpty();
        queue.First().LeadId.Should().Be(lead1.LeadId, "lead in peak local morning window must be ranked first in dialer queue");
        queue.First().InstantaneousConfidenceScore.Should().BeGreaterThan(queue.Last().InstantaneousConfidenceScore);
    }

    [Fact]
    public async Task SimulateDialerCall_Answered_AutoGraduatesLeadAndLogsTelemetry()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        var cadences = await service.GetCadencesAsync(PredictiveCadenceService.DefaultDemoTenantId);
        var cadence = cadences.First();

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            cadence.Id,
            Guid.NewGuid(),
            "Dialer Lead",
            "dialer@test.com",
            "+1 555-0700",
            "Dialer Corp",
            "UTC"
        ));

        // Advance to phone step if step 1 is whatsapp
        await service.AdvanceStepAsync(tenantId, enrollment.Id);

        // Act: Simulate Answered call
        var result = await service.SimulateDialerCallAsync(tenantId, enrollment.Id, "Answered");

        // Assert
        result.Outcome.Should().Be("Answered");
        result.CadenceAutoPausedOrGraduated.Should().BeTrue();

        var updated = await service.GetEnrollmentByIdAsync(tenantId, enrollment.Id);
        updated!.Status.Should().Be(EnrollmentStatus.Replied);
    }

    [Fact]
    public async Task SimulateDialerCall_NoAnswer_AdvancesStepAndMaintainsActiveStatus()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        var cadences = await service.GetCadencesAsync(PredictiveCadenceService.DefaultDemoTenantId);
        var cadence = cadences.First();

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            cadence.Id,
            Guid.NewGuid(),
            "Busy Lead",
            "busy@test.com",
            "+1 555-0800",
            "Busy Corp",
            "UTC"
        ));

        // Act: Simulate NoAnswer call
        var result = await service.SimulateDialerCallAsync(tenantId, enrollment.Id, "NoAnswer");

        // Assert
        result.Outcome.Should().Be("NoAnswer");
        result.CadenceAutoPausedOrGraduated.Should().BeFalse();

        var updated = await service.GetEnrollmentByIdAsync(tenantId, enrollment.Id);
        updated!.CurrentStepNumber.Should().Be(2); // Advanced to step 2
        updated.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task PauseAndResumeEnrollment_UpdatesLifecycleStateAccurately()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        var cadences = await service.GetCadencesAsync(PredictiveCadenceService.DefaultDemoTenantId);
        var cadence = cadences.First();

        var enrollment = await service.EnrollLeadAsync(tenantId, new EnrollLeadRequest(
            cadence.Id,
            Guid.NewGuid(),
            "Pause Test",
            "pause@test.com",
            "+1 555-0900",
            "Pause Corp",
            "UTC"
        ));

        // Act 1: Pause
        var paused = await service.PauseEnrollmentAsync(tenantId, enrollment.Id, "SDR manual pause");
        paused!.Status.Should().Be(EnrollmentStatus.Paused);
        paused.PauseReason.Should().Be("SDR manual pause");

        // Act 2: Resume
        var resumed = await service.ResumeEnrollmentAsync(tenantId, enrollment.Id);
        resumed!.Status.Should().Be(EnrollmentStatus.Active);
        resumed.PauseReason.Should().BeNull();
        resumed.NextStepScheduledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCadence_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        var action = async () => await service.CreateCadenceAsync(tenantId, new CreateCadenceRequest(
            "",
            "Empty name description",
            CadenceCategory.InboundFastResponse,
            new List<CadenceStepDefinition>()
        ));

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Cadence name is required.*");
    }

    [Fact]
    public async Task GetOverviewStats_ReturnsAggregatedMetrics()
    {
        // Arrange
        var service = CreateService();

        // Act
        var stats = await service.GetOverviewStatsAsync(PredictiveCadenceService.DefaultDemoTenantId);

        // Assert
        stats.Should().NotBeNull();
        stats.TotalCadences.Should().BeGreaterThanOrEqualTo(3);
        stats.ActiveEnrollments.Should().BeGreaterThan(0);
        stats.TotalTelemetryEventsRecorded.Should().BeGreaterThan(0);
        stats.TopChannel.Should().NotBeNullOrWhiteSpace();
    }
}
