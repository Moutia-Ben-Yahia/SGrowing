using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.Asc606Revenue;

public interface IAsc606RevenueEngine
{
    List<RevenueContractSchedule> GetSchedules();
    RevenueContractSchedule? GetSchedule(string scheduleId);
    RevenueContractSchedule RecognizeNextMonth(RecognizeNextMonthRequest request);
}

public class Asc606RevenueEngine : IAsc606RevenueEngine
{
    private readonly ConcurrentDictionary<string, RevenueContractSchedule> _schedules = new(StringComparer.OrdinalIgnoreCase);

    public Asc606RevenueEngine()
    {
        // Seed 12-month contract: $120,000 Total ($10,000/mo)
        var tranches = new List<MonthlyAmortizationTranche>();
        decimal monthly = 10000m;
        for (int m = 1; m <= 12; m++)
        {
            bool recognized = m <= 2; // Months 1 and 2 already recognized
            decimal remaining = 120000m - (m * monthly);
            tranches.Add(new MonthlyAmortizationTranche(
                MonthIndex: m,
                MonthLabel: $"Month {m} Amortization",
                RecognizedAmountUsd: monthly,
                RemainingDeferredBalanceUsd: remaining,
                IsRecognized: recognized
            ));
        }

        var s1 = new RevenueContractSchedule(
            ScheduleId: "asc606_sched_301",
            CustomerId: "cust_acme_corp",
            CustomerName: "Acme Global Enterprise",
            TotalContractValueUsd: 120000m,
            CumulativeRecognizedUsd: 20000m,
            DeferredRevenueLiabilityUsd: 100000m,
            ContractDurationMonths: 12,
            Tranches: tranches
        );

        _schedules[s1.ScheduleId] = s1;
    }

    public List<RevenueContractSchedule> GetSchedules() => _schedules.Values.OrderBy(s => s.ScheduleId).ToList();

    public RevenueContractSchedule? GetSchedule(string scheduleId)
    {
        _schedules.TryGetValue(scheduleId, out var s);
        return s;
    }

    public RevenueContractSchedule RecognizeNextMonth(RecognizeNextMonthRequest request)
    {
        if (!_schedules.TryGetValue(request.ScheduleId, out var existing))
        {
            throw new KeyNotFoundException($"Schedule {request.ScheduleId} not found.");
        }

        var nextUnrecognized = existing.Tranches.FirstOrDefault(t => !t.IsRecognized);
        if (nextUnrecognized is null)
        {
            return existing; // Fully recognized
        }

        var updatedTranches = existing.Tranches.Select(t =>
        {
            if (t.MonthIndex == nextUnrecognized.MonthIndex)
            {
                return t with { IsRecognized = true };
            }
            return t;
        }).ToList();

        decimal recognizedSoFar = updatedTranches.Where(t => t.IsRecognized).Sum(t => t.RecognizedAmountUsd);
        decimal remainingDeferred = existing.TotalContractValueUsd - recognizedSoFar;

        var updated = existing with
        {
            Tranches = updatedTranches,
            CumulativeRecognizedUsd = recognizedSoFar,
            DeferredRevenueLiabilityUsd = remainingDeferred
        };

        _schedules[request.ScheduleId] = updated;
        return updated;
    }
}
