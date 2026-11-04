using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.Asc606Revenue;
using Xunit;

namespace SGrowing.Tests.Finance;

public class Asc606RevenueTests
{
    private readonly Asc606RevenueEngine _engine = new();

    [Fact]
    public void GetSchedules_ReturnsSeedContractWithTranches()
    {
        var list = _engine.GetSchedules();

        list.Should().NotBeNull();
        list.Should().HaveCount(1);
        var s = list.First();
        s.ScheduleId.Should().Be("asc606_sched_301");
        s.TotalContractValueUsd.Should().Be(120000m);
        s.CumulativeRecognizedUsd.Should().Be(20000m);
        s.DeferredRevenueLiabilityUsd.Should().Be(100000m);
        s.Tranches.Should().HaveCount(12);
    }

    [Fact]
    public void RecognizeNextMonth_RecognizesTrancheAndReducesDeferred()
    {
        var scheduleId = "asc606_sched_301";
        var request = new RecognizeNextMonthRequest(scheduleId);

        var updated = _engine.RecognizeNextMonth(request);

        // Before: 20000 recognized, 100000 deferred.
        // After month 3: 30000 recognized, 90000 deferred.
        updated.CumulativeRecognizedUsd.Should().Be(30000m);
        updated.DeferredRevenueLiabilityUsd.Should().Be(90000m);
        updated.Tranches.Count(t => t.IsRecognized).Should().Be(3);
    }

    [Fact]
    public void RecognizeNextMonth_ThrowsKeyNotFoundException_WhenScheduleDoesNotExist()
    {
        var request = new RecognizeNextMonthRequest("non_existent_schedule_999");
        var act = () => _engine.RecognizeNextMonth(request);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*non_existent_schedule_999*");
    }

    [Fact]
    public void RecognizeNextMonth_FullyRecognized_IsIdempotentAndDoesNotExceedTotalValue()
    {
        var scheduleId = "asc606_sched_301";

        // Recognize all remaining 10 months (months 3 through 12)
        RevenueContractSchedule last = null!;
        for (int i = 0; i < 10; i++)
        {
            last = _engine.RecognizeNextMonth(new RecognizeNextMonthRequest(scheduleId));
        }

        last.CumulativeRecognizedUsd.Should().Be(120000m);
        last.DeferredRevenueLiabilityUsd.Should().Be(0m);
        last.Tranches.Should().OnlyContain(t => t.IsRecognized);

        // 13th call: Already fully recognized, should return existing schedule idempotently
        var idempotent = _engine.RecognizeNextMonth(new RecognizeNextMonthRequest(scheduleId));
        idempotent.CumulativeRecognizedUsd.Should().Be(120000m);
        idempotent.DeferredRevenueLiabilityUsd.Should().Be(0m);
    }

    [Fact]
    public void GetSchedule_ExistingAndNonExistent_ReturnsExpected()
    {
        var existing = _engine.GetSchedule("asc606_sched_301");
        existing.Should().NotBeNull();
        existing!.CustomerName.Should().Be("Acme Global Enterprise");

        var missing = _engine.GetSchedule("non_existent_schedule");
        missing.Should().BeNull();
    }

    [Fact]
    public void GetSchedules_OrderById_ReturnsAllSchedules()
    {
        var schedules = _engine.GetSchedules();
        schedules.Should().NotBeEmpty();
        schedules.Should().BeInAscendingOrder(s => s.ScheduleId);
    }

    [Fact]
    public void RecognizeNextMonth_ConsecutiveMonthlyCalls_CorrectlyReducesRemainingLiabilityStepByStep()
    {
        var scheduleId = "asc606_sched_301";

        // Month 3 recognition
        var m3 = _engine.RecognizeNextMonth(new RecognizeNextMonthRequest(scheduleId));
        m3.CumulativeRecognizedUsd.Should().Be(30000m);
        m3.DeferredRevenueLiabilityUsd.Should().Be(90000m);

        // Month 4 recognition
        var m4 = _engine.RecognizeNextMonth(new RecognizeNextMonthRequest(scheduleId));
        m4.CumulativeRecognizedUsd.Should().Be(40000m);
        m4.DeferredRevenueLiabilityUsd.Should().Be(80000m);

        // Month 5 recognition
        var m5 = _engine.RecognizeNextMonth(new RecognizeNextMonthRequest(scheduleId));
        m5.CumulativeRecognizedUsd.Should().Be(50000m);
        m5.DeferredRevenueLiabilityUsd.Should().Be(70000m);

        m5.Tranches.Take(5).Should().OnlyContain(t => t.IsRecognized);
        m5.Tranches.Skip(5).Should().OnlyContain(t => !t.IsRecognized);
    }

    [Fact]
    public void GetSchedule_CaseInsensitiveScheduleId_ReturnsSchedule()
    {
        var scheduleUpper = _engine.GetSchedule("ASC606_SCHED_301");

        scheduleUpper.Should().NotBeNull();
        scheduleUpper!.ScheduleId.Should().Be("asc606_sched_301");
        scheduleUpper.CustomerName.Should().Be("Acme Global Enterprise");
        scheduleUpper.ContractDurationMonths.Should().Be(12);
        scheduleUpper.TotalContractValueUsd.Should().Be(120000m);
    }
}
