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
}
