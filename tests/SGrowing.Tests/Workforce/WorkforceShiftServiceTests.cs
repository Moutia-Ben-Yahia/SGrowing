using FluentAssertions;
using SGrowing.Api.Modules.Workforce.Shifts;
using Xunit;

namespace SGrowing.Tests.Workforce;

public class WorkforceShiftServiceTests
{
    private readonly WorkforceShiftService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _agent1 = Guid.NewGuid();
    private readonly Guid _agent2 = Guid.NewGuid();

    [Fact]
    public async Task CreateShiftTemplateAsync_ShouldComputeDuration_AndStoreQuotas()
    {
        var req = new CreateShiftTemplateRequest(
            _tenantId,
            "Morning WhatsApp Primary",
            ShiftType.Morning,
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(16),
            new Dictionary<ChannelSpecialty, int>
            {
                { ChannelSpecialty.WhatsApp, 4 },
                { ChannelSpecialty.Voice, 2 }
            }
        );

        var template = await _service.CreateShiftTemplateAsync(req);

        template.Should().NotBeNull();
        template.TemplateId.Should().NotBeEmpty();
        template.DurationHours.Should().Be(8.0);
        template.RequiredChannelQuotas.Should().ContainKey(ChannelSpecialty.WhatsApp);
    }

    [Fact]
    public async Task ScheduleShiftAsync_ShouldCreateShift_WhenNoConflictsExist()
    {
        var baseDate = DateTime.UtcNow.Date.AddDays(1);
        var req = new ScheduleShiftRequest(
            _tenantId,
            _agent1,
            "Sami Support",
            Guid.NewGuid(),
            ShiftType.Morning,
            baseDate.AddHours(8),
            baseDate.AddHours(16),
            ChannelSpecialty.WhatsApp
        );

        var shift = await _service.ScheduleShiftAsync(req);

        shift.Should().NotBeNull();
        shift.ShiftId.Should().NotBeEmpty();
        shift.AgentId.Should().Be(_agent1);
        shift.Status.Should().Be(ShiftStatus.Scheduled);
    }

    [Fact]
    public async Task ScheduleShiftAsync_ShouldDetectOverlapConflict_ForSameAgent()
    {
        var baseDate = DateTime.UtcNow.Date.AddDays(2);
        var shift1 = new ScheduleShiftRequest(
            _tenantId,
            _agent1,
            "Sami Support",
            Guid.NewGuid(),
            ShiftType.Morning,
            baseDate.AddHours(8),
            baseDate.AddHours(16)
        );
        await _service.ScheduleShiftAsync(shift1);

        // Overlapping shift
        var shift2 = new ScheduleShiftRequest(
            _tenantId,
            _agent1,
            "Sami Support",
            Guid.NewGuid(),
            ShiftType.Afternoon,
            baseDate.AddHours(14),
            baseDate.AddHours(22)
        );

        var act = () => _service.ScheduleShiftAsync(shift2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*overlaps*");
    }

    [Fact]
    public async Task ScheduleShiftAsync_ShouldEnforce11HourRestPeriodRule()
    {
        var baseDate = DateTime.UtcNow.Date.AddDays(3);
        // Shift ends at 20:00
        var shift1 = new ScheduleShiftRequest(
            _tenantId,
            _agent1,
            "Sami Support",
            Guid.NewGuid(),
            ShiftType.Afternoon,
            baseDate.AddHours(12),
            baseDate.AddHours(20)
        );
        await _service.ScheduleShiftAsync(shift1);

        // Next shift starts at 05:00 next morning (only 9 hours rest)
        var shift2 = new ScheduleShiftRequest(
            _tenantId,
            _agent1,
            "Sami Support",
            Guid.NewGuid(),
            ShiftType.Morning,
            baseDate.AddDays(1).AddHours(5),
            baseDate.AddDays(1).AddHours(13)
        );

        var act = () => _service.ScheduleShiftAsync(shift2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*11-hour rest period*");
    }

    [Fact]
    public async Task ClockInShiftAsync_ShouldMarkCheckedIn_WhenOnTime()
    {
        var start = DateTime.UtcNow.AddMinutes(-5);
        var shift = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, start, start.AddHours(8)
        ));

        var clocked = await _service.ClockInShiftAsync(new ClockInShiftRequest(
            _tenantId, shift.ShiftId, _agent1, ClockInTimeUtc: start.AddMinutes(2)
        ));

        clocked.Status.Should().Be(ShiftStatus.CheckedIn);
        clocked.ActualClockInUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ClockInShiftAsync_ShouldMarkLateArrival_WhenOver10MinutesPastStart()
    {
        var start = DateTime.UtcNow.AddMinutes(-30);
        var shift = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, start, start.AddHours(8)
        ));

        var clocked = await _service.ClockInShiftAsync(new ClockInShiftRequest(
            _tenantId, shift.ShiftId, _agent1, ClockInTimeUtc: start.AddMinutes(15) // 15 mins late
        ));

        clocked.Status.Should().Be(ShiftStatus.LateArrival);
    }

    [Fact]
    public async Task ClockInShiftAsync_ShouldThrow_WhenUnauthorizedAgentAttemptsClockIn()
    {
        var start = DateTime.UtcNow;
        var shift = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, start, start.AddHours(8)
        ));

        var act = () => _service.ClockInShiftAsync(new ClockInShiftRequest(
            _tenantId, shift.ShiftId, _agent2 // Agent 2 trying to clock in for Agent 1
        ));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ClockOutShiftAsync_ShouldMarkCompleted()
    {
        var start = DateTime.UtcNow.AddHours(-8);
        var shift = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, start, start.AddHours(8)
        ));

        await _service.ClockInShiftAsync(new ClockInShiftRequest(_tenantId, shift.ShiftId, _agent1, start));
        var completed = await _service.ClockOutShiftAsync(new ClockOutShiftRequest(_tenantId, shift.ShiftId, _agent1, start.AddHours(8)));

        completed.Status.Should().Be(ShiftStatus.Completed);
        completed.ActualClockOutUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveShiftSwapAsync_ShouldExchangeAgentAssignmentsAtomically()
    {
        var d1 = DateTime.UtcNow.Date.AddDays(5);
        var d2 = DateTime.UtcNow.Date.AddDays(6);

        var shiftA = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent1, "Sami Alpha", Guid.NewGuid(), ShiftType.Morning, d1.AddHours(8), d1.AddHours(16)
        ));

        var shiftB = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent2, "Karim Beta", Guid.NewGuid(), ShiftType.Morning, d2.AddHours(8), d2.AddHours(16)
        ));

        var swapReq = await _service.RequestShiftSwapAsync(new RequestShiftSwapRequest(
            _tenantId,
            _agent1,
            "Sami Alpha",
            shiftA.ShiftId,
            _agent2,
            "Karim Beta",
            shiftB.ShiftId,
            "Family emergency on day 5"
        ));

        swapReq.Status.Should().Be(SwapRequestStatus.PendingManagerApproval);

        var managerId = Guid.NewGuid();
        var approved = await _service.ApproveShiftSwapAsync(_tenantId, swapReq.SwapId, managerId);

        approved.Status.Should().Be(SwapRequestStatus.Approved);
        approved.ReviewedByManagerId.Should().Be(managerId);

        // Verify that assignments were swapped
        var updatedShiftA = await _service.GetScheduleByIdAsync(_tenantId, shiftA.ShiftId);
        var updatedShiftB = await _service.GetScheduleByIdAsync(_tenantId, shiftB.ShiftId);

        updatedShiftA!.AgentId.Should().Be(_agent2);
        updatedShiftA.AgentName.Should().Be("Karim Beta");

        updatedShiftB!.AgentId.Should().Be(_agent1);
        updatedShiftB.AgentName.Should().Be("Sami Alpha");
    }

    [Fact]
    public async Task EvaluateCapacityAndGapsAsync_ShouldDetectUnderstaffingAgainstForecast()
    {
        var date = DateTime.UtcNow.Date.AddDays(7);

        // Schedule 1 agent for WhatsApp (capacity = 15 chats/hour)
        await _service.ScheduleShiftAsync(new ScheduleShiftRequest(
            _tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, date.AddHours(9), date.AddHours(17), ChannelSpecialty.WhatsApp
        ));

        // Evaluate with high volume (1,000 forecasted chats for the day)
        var result = await _service.EvaluateCapacityAndGapsAsync(_tenantId, date, 1000, ChannelSpecialty.WhatsApp);

        result.Should().NotBeNull();
        result.TotalForecastedVolume.Should().Be(1000);
        result.HourlyGaps.Should().HaveCount(24);
        result.TotalGapsCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAdherenceReportAsync_ShouldCalculatePunctualityAccurately()
    {
        var now = DateTime.UtcNow;
        var s1 = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(_tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, now.AddDays(-2), now.AddDays(-2).AddHours(8)));
        var s2 = await _service.ScheduleShiftAsync(new ScheduleShiftRequest(_tenantId, _agent1, "Sami", Guid.NewGuid(), ShiftType.Morning, now.AddDays(-1), now.AddDays(-1).AddHours(8)));

        // Shift 1 on time
        await _service.ClockInShiftAsync(new ClockInShiftRequest(_tenantId, s1.ShiftId, _agent1, s1.StartTimeUtc));
        // Shift 2 late
        await _service.ClockInShiftAsync(new ClockInShiftRequest(_tenantId, s2.ShiftId, _agent1, s2.StartTimeUtc.AddMinutes(20)));

        var report = await _service.GetAdherenceReportAsync(_tenantId, now.AddDays(-3), now.AddDays(1));

        report.Should().ContainSingle(r => r.AgentId == _agent1);
        var record = report.First(r => r.AgentId == _agent1);
        record.TotalScheduledShifts.Should().Be(2);
        record.OnTimeShifts.Should().Be(1);
        record.LateArrivals.Should().Be(1);
        record.AdherencePercentage.Should().Be(50.0);
    }
}
