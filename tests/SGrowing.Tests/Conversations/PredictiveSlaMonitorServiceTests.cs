using FluentAssertions;
using SGrowing.Api.Modules.Conversations;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class PredictiveSlaMonitorServiceTests
{
    private readonly PredictiveSlaMonitorService _service;

    public PredictiveSlaMonitorServiceTests()
    {
        _service = new PredictiveSlaMonitorService();
        _service.SetBusinessHours(new TimeOnly(9, 0), new TimeOnly(18, 0)); // 09:00 - 18:00 UTC (9h = 540min)
    }

    // ==========================================
    // 1. BUSINESS HOURS CALCULATION TESTS
    // ==========================================

    [Fact]
    public void CalculateElapsedBusinessMinutes_WithinSingleDay_ReturnsExactMinutes()
    {
        // Monday 10:00 to Monday 12:30 -> 150 minutes
        var start = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc); // Monday
        var end = new DateTime(2026, 11, 2, 12, 30, 0, DateTimeKind.Utc);

        var elapsed = _service.CalculateElapsedBusinessMinutes(start, end, is24x7: false);

        elapsed.Should().Be(150.0);
    }

    [Fact]
    public void CalculateElapsedBusinessMinutes_SpanningWeekend_ExcludesSaturdayAndSunday()
    {
        // Friday 17:00 to Monday 10:00:
        // Friday 17:00-18:00 = 60 min
        // Saturday & Sunday = 0 min
        // Monday 09:00-10:00 = 60 min
        // Total = 120 minutes
        var fridayEvening = new DateTime(2026, 11, 6, 17, 0, 0, DateTimeKind.Utc); // Friday
        var mondayMorning = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc); // Monday

        var elapsed = _service.CalculateElapsedBusinessMinutes(fridayEvening, mondayMorning, is24x7: false);

        elapsed.Should().Be(120.0);
    }

    [Fact]
    public void CalculateElapsedBusinessMinutes_SpanningHoliday_ExcludesStatutoryHoliday()
    {
        // Add Tuesday as a statutory holiday
        var holiday = new DateOnly(2026, 11, 3);
        _service.AddStatutoryHoliday(holiday);

        // Monday 17:00 to Wednesday 10:00
        // Monday: 17:00 - 18:00 = 60 min
        // Tuesday: 0 min (Holiday)
        // Wednesday: 09:00 - 10:00 = 60 min
        // Total = 120 min
        var mondayEvening = new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc);
        var wednesdayMorning = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

        var elapsed = _service.CalculateElapsedBusinessMinutes(mondayEvening, wednesdayMorning, is24x7: false);

        elapsed.Should().Be(120.0);
    }

    [Fact]
    public void CalculateElapsedBusinessMinutes_OutsideBusinessHours_ReturnsZero()
    {
        // Monday 20:00 to Monday 23:00 (outside 09:00 - 18:00)
        var start = new DateTime(2026, 11, 2, 20, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 2, 23, 0, 0, DateTimeKind.Utc);

        var elapsed = _service.CalculateElapsedBusinessMinutes(start, end, is24x7: false);

        elapsed.Should().Be(0.0);
    }

    [Fact]
    public void CalculateElapsedBusinessMinutes_ClampsPreAndPostBusinessHours()
    {
        // Monday 07:00 to Monday 20:00 -> Only 09:00 to 18:00 counts (9 hours = 540 min)
        var start = new DateTime(2026, 11, 2, 7, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 2, 20, 0, 0, DateTimeKind.Utc);

        var elapsed = _service.CalculateElapsedBusinessMinutes(start, end, is24x7: false);

        elapsed.Should().Be(540.0);
    }

    [Fact]
    public void CalculateElapsedBusinessMinutes_24x7Tier_IncludesAllCalendarMinutes()
    {
        // Friday 17:00 to Monday 10:00 in 24/7 mode:
        // (65 hours = 3,900 minutes)
        var start = new DateTime(2026, 11, 6, 17, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);

        var elapsed = _service.CalculateElapsedBusinessMinutes(start, end, is24x7: true);

        elapsed.Should().Be((end - start).TotalMinutes);
    }

    [Fact]
    public void CalculateDueDate_WithinSameBusinessDay_ReturnsExactTargetTime()
    {
        // Monday 10:00 + 120 minutes -> Monday 12:00
        var start = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var due = _service.CalculateDueDate(start, 120, is24x7: false);

        due.Should().Be(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CalculateDueDate_Overnight_RollsToNextBusinessMorning()
    {
        // Monday 17:00 + 120 minutes:
        // 60 min used on Monday (until 18:00)
        // 60 min remaining starts Tuesday 09:00 -> Tuesday 10:00
        var start = new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc);
        var due = _service.CalculateDueDate(start, 120, is24x7: false);

        due.Should().Be(new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CalculateDueDate_OverWeekend_RollsToMondayMorning()
    {
        // Friday 17:00 + 120 minutes:
        // 60 min used on Friday (until 18:00)
        // Saturday/Sunday skipped
        // 60 min on Monday 09:00 -> Monday 10:00
        var start = new DateTime(2026, 11, 6, 17, 0, 0, DateTimeKind.Utc); // Friday
        var due = _service.CalculateDueDate(start, 120, is24x7: false);

        due.Should().Be(new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc)); // Monday
    }

    [Fact]
    public void CalculateDueDate_OverHoliday_RollsPastHoliday()
    {
        // Monday 17:00 + 120 min, Tuesday is holiday:
        // 60 min Friday/Monday -> Tuesday skipped -> Wednesday 10:00
        _service.AddStatutoryHoliday(new DateOnly(2026, 11, 3));
        var start = new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc);

        var due = _service.CalculateDueDate(start, 120, is24x7: false);

        due.Should().Be(new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CalculateRemainingBusinessMinutes_WhenPastDue_ReturnsZero()
    {
        var due = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);

        var remaining = _service.CalculateRemainingBusinessMinutes(now, due, is24x7: false);

        remaining.Should().Be(0.0);
    }

    // ==========================================
    // 2. PREDICTIVE VELOCITY PROJECTION TESTS
    // ==========================================

    [Fact]
    public void ProjectVelocityToBreach_WithHighBuffer_ReturnsSafeRiskLevel()
    {
        // Now: 10:00. Due: 14:00 (240 min away)
        // QueueAhead: 1, Velocity: 0.05 tickets/min (20 min/ticket), Complexity: 0
        // Needed: (1 + 1) / 0.05 = 40 minutes -> Predicted: 10:40
        // Buffer: 14:00 - 10:40 = +200 minutes (> 60m => Safe)
        var now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);

        var (predicted, buffer, risk) = _service.ProjectVelocityToBreach(now, due, queueAhead: 1, avgAgentVelocityMinutes: 0.05, complexityWeight: 0.0);

        predicted.Should().Be(now.AddMinutes(40));
        buffer.Should().Be(200.0);
        risk.Should().Be(SlaRiskLevel.Safe);
    }

    [Fact]
    public void ProjectVelocityToBreach_BetweenZeroAndSixtyMinutes_ReturnsWarningRiskLevel()
    {
        // Now: 10:00. Due: 11:00 (60 min away)
        // QueueAhead: 0 (1 ticket). Velocity: 0.05 -> Needed: 1 / 0.05 = 20 minutes -> Predicted: 10:20
        // Buffer: 11:00 - 10:20 = +40 minutes (0 <= 40 <= 60 => Warning)
        var now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);

        var (predicted, buffer, risk) = _service.ProjectVelocityToBreach(now, due, queueAhead: 0, avgAgentVelocityMinutes: 0.05, complexityWeight: 0.0);

        buffer.Should().Be(40.0);
        risk.Should().Be(SlaRiskLevel.Warning);
    }

    [Fact]
    public void ProjectVelocityToBreach_WithNegativeBuffer_ReturnsImminentBreachRiskLevel()
    {
        // Now: 10:00. Due: 10:30 (30 min away)
        // QueueAhead: 2 (3 tickets). Velocity: 0.05 -> Needed: 3 / 0.05 = 60 minutes -> Predicted: 11:00
        // Buffer: 10:30 - 11:00 = -30 minutes (< 0 => ImminentBreach)
        var now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 2, 10, 30, 0, DateTimeKind.Utc);

        var (predicted, buffer, risk) = _service.ProjectVelocityToBreach(now, due, queueAhead: 2, avgAgentVelocityMinutes: 0.05, complexityWeight: 0.0);

        buffer.Should().Be(-30.0);
        risk.Should().Be(SlaRiskLevel.ImminentBreach);
    }

    [Fact]
    public void ProjectVelocityToBreach_ComplexityWeight_IncreasesEstimatedTime()
    {
        var now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 2, 13, 0, 0, DateTimeKind.Utc);

        // Base without complexity: (1+1)/0.05 = 40 min
        var (predBase, _, _) = _service.ProjectVelocityToBreach(now, due, queueAhead: 1, avgAgentVelocityMinutes: 0.05, complexityWeight: 0.0);
        // With 50% complexity: 40 * 1.5 = 60 min
        var (predComplex, _, _) = _service.ProjectVelocityToBreach(now, due, queueAhead: 1, avgAgentVelocityMinutes: 0.05, complexityWeight: 0.5);

        (predComplex - predBase).TotalMinutes.Should().Be(20.0);
    }

    [Fact]
    public void ProjectVelocityToBreach_ZeroVelocity_FallsBackSafely()
    {
        var now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

        // Velocity = 0 should not throw divide-by-zero, falls back to 0.05
        var action = () => _service.ProjectVelocityToBreach(now, due, queueAhead: 1, avgAgentVelocityMinutes: 0.0);

        action.Should().NotThrow();
        var (_, buffer, _) = action();
        buffer.Should().BeGreaterThan(-1000);
    }

    // ==========================================
    // 3. ESCALATION LADDER TESTS
    // ==========================================

    [Fact]
    public void EvaluateEscalationStage_Under70Percent_ReturnsNominal()
    {
        // 50 min of 100 min target (50%)
        var (stage, boosted, reassign, alert) = _service.EvaluateEscalationStage(50, 100);

        stage.Should().Be(SlaEscalationStage.Nominal);
        boosted.Should().BeFalse();
        reassign.Should().BeFalse();
        alert.Should().BeFalse();
    }

    [Fact]
    public void EvaluateEscalationStage_Between70And85Percent_ReturnsWarningWithPriorityBoost()
    {
        // 75 min of 100 min target (75%)
        var (stage, boosted, reassign, alert) = _service.EvaluateEscalationStage(75, 100);

        stage.Should().Be(SlaEscalationStage.Warning);
        boosted.Should().BeTrue();
        reassign.Should().BeFalse();
        alert.Should().BeFalse();
    }

    [Fact]
    public void EvaluateEscalationStage_Between85And100Percent_ReturnsCriticalWithAutoReassignment()
    {
        // 90 min of 100 min target (90%)
        var (stage, boosted, reassign, alert) = _service.EvaluateEscalationStage(90, 100);

        stage.Should().Be(SlaEscalationStage.Critical);
        boosted.Should().BeTrue();
        reassign.Should().BeTrue();
        alert.Should().BeFalse();
    }

    [Fact]
    public void EvaluateEscalationStage_AtOrOver100Percent_ReturnsBreachedWithExecutiveAlert()
    {
        // 105 min of 100 min target (105%)
        var (stage, boosted, reassign, alert) = _service.EvaluateEscalationStage(105, 100);

        stage.Should().Be(SlaEscalationStage.Breached);
        boosted.Should().BeTrue();
        reassign.Should().BeTrue();
        alert.Should().BeTrue();
    }

    // ==========================================
    // 4. SWEEP, EVALUATION & REASSIGNMENT OPERATIONS
    // ==========================================

    [Fact]
    public async Task RunEscalationSweepAsync_EvaluatesAllSeedTickets()
    {
        var sweep = await _service.RunEscalationSweepAsync();

        sweep.EvaluatedCount.Should().Be(6);
        sweep.Tickets.Should().HaveCount(6);
        sweep.Tickets.Should().Contain(t => t.EscalationStage == SlaEscalationStage.Critical);
        sweep.Tickets.Should().Contain(t => t.EscalationStage == SlaEscalationStage.Breached);
    }

    [Fact]
    public async Task EvaluateTicketAsync_UpdatesQueueAndRecalculatesProjection()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111103"); // Nominal ticket

        var updated = await _service.EvaluateTicketAsync(id, queueAhead: 5, velocity: 0.02, complexity: 0.5);

        updated.QueueAhead.Should().Be(5);
        updated.AvgAgentVelocityMinutes.Should().Be(0.02);
        updated.ComplexityWeight.Should().Be(0.5);
    }

    [Fact]
    public async Task EvaluateTicketAsync_ThrowsKeyNotFound_ForInvalidId()
    {
        var invalidId = Guid.NewGuid();

        var act = async () => await _service.EvaluateTicketAsync(invalidId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ManualReassignAsync_AssignsSupervisorAndLogsReason()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111102");

        var updated = await _service.ManualReassignAsync(id, "agent-lead-09", "Commander Shepherd", "VIP customer high-touch escalation");

        updated.AssignedAgentId.Should().Be("agent-lead-09");
        updated.AssignedAgentName.Should().Be("Commander Shepherd");
        updated.AutoReassigned.Should().BeTrue();
        updated.AutoReassignedTo.Should().Contain("VIP customer high-touch escalation");
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsAggregatedComplianceAndCounts()
    {
        var metrics = await _service.GetMetricsAsync();

        metrics.TotalMonitoredTickets.Should().Be(6);
        metrics.ImminentBreachesCount.Should().BeGreaterThan(0);
        metrics.CriticalCount.Should().BeGreaterThan(0);
        metrics.WarningCount.Should().BeGreaterThan(0);
        metrics.BreachedCount.Should().BeGreaterThan(0);
        metrics.FirstResponseCompliancePercentage.Should().BeInRange(0, 100);
        metrics.ResolutionCompliancePercentage.Should().BeInRange(0, 100);
        metrics.ActiveQueueVelocity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMonitoredTicketsAsync_FiltersByRiskLevel()
    {
        var imminent = await _service.GetMonitoredTicketsAsync("imminent");
        imminent.Should().OnlyContain(t => t.RiskLevel == SlaRiskLevel.ImminentBreach);

        var safe = await _service.GetMonitoredTicketsAsync("safe");
        safe.Should().OnlyContain(t => t.RiskLevel == SlaRiskLevel.Safe);
    }

    [Fact]
    public async Task ConcurrentOperations_AreThreadSafe()
    {
        // Execute multiple concurrent sweeps and evaluations simultaneously
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _service.RunEscalationSweepAsync();
                await _service.GetMetricsAsync();
                await _service.GetMonitoredTicketsAsync();
            }));
        }

        var act = async () => await Task.WhenAll(tasks);

        await act.Should().NotThrowAsync();
    }
}
