using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Finance.Section174;

public interface ISection174Engine
{
    Section174Overview GetOverview();
    Section174Schedule CreateSchedule(int taxYear, JurisdictionResearchType jurisdiction, double totalExpenditureUsd);
    EngineeringLaborAllocation AllocateLabor(string sprintId, string squadName, double payrollUsd, double qualifyingRatio, JurisdictionResearchType jurisdiction);
}

public sealed class Section174Engine : ISection174Engine
{
    private readonly ConcurrentDictionary<string, Section174Schedule> _schedules = new();
    private readonly ConcurrentDictionary<string, EngineeringLaborAllocation> _allocations = new();

    public Section174Engine()
    {
        SeedSchedules();
        SeedInitialAllocations();
    }

    private void SeedSchedules()
    {
        var s1 = new Section174Schedule(
            "SCHED-174-2025-US",
            2025,
            JurisdictionResearchType.UnitedStatesDomestic5Year,
            TotalCapitalizedExpenditureUsd: 14500000.0,
            Year1DeductionUsd: 1450000.0, // 10% mid-year convention
            AnnualSubsequentDeductionUsd: 2900000.0, // 20% years 2-5
            CumulativeTaxShieldGeneratedUsd: 304500.0, // at 21% US corporate rate
            DateTime.UtcNow.AddMonths(-6)
        );
        _schedules.TryAdd(s1.ScheduleId, s1);

        var s2 = new Section174Schedule(
            "SCHED-174-2025-FOR",
            2025,
            JurisdictionResearchType.ForeignResearch15Year,
            TotalCapitalizedExpenditureUsd: 4200000.0,
            Year1DeductionUsd: 140000.0, // 3.33% mid-year convention
            AnnualSubsequentDeductionUsd: 280000.0,
            CumulativeTaxShieldGeneratedUsd: 29400.0,
            DateTime.UtcNow.AddMonths(-6)
        );
        _schedules.TryAdd(s2.ScheduleId, s2);
    }

    private void SeedInitialAllocations()
    {
        var a1 = new EngineeringLaborAllocation(
            "ALLOC-LABOR-001",
            "SPRINT-2026-W34",
            "Platform Core Mesh & eBPF Networking",
            850000.0,
            0.92,
            JurisdictionResearchType.UnitedStatesDomestic5Year,
            782000.0
        );
        _allocations.TryAdd(a1.AllocationId, a1);
    }

    public Section174Overview GetOverview()
    {
        var schedules = _schedules.Values.ToList();
        var allocs = _allocations.Values.Take(10).ToList();

        double domestic = schedules.Where(s => s.Jurisdiction == JurisdictionResearchType.UnitedStatesDomestic5Year).Sum(s => s.TotalCapitalizedExpenditureUsd);
        double foreign = schedules.Where(s => s.Jurisdiction == JurisdictionResearchType.ForeignResearch15Year).Sum(s => s.TotalCapitalizedExpenditureUsd);
        double deductions = schedules.Sum(s => s.Year1DeductionUsd);
        double cashTaxSavings = Math.Round(deductions * 0.21, 2);

        return new Section174Overview(
            TotalActiveSchedulesCount: schedules.Count,
            TotalDomesticCapitalizedUsd: domestic,
            TotalForeignCapitalizedUsd: foreign,
            AnnualSection174TaxDeductionUsd: deductions,
            EstimatedCashTaxSavingsUsd: cashTaxSavings,
            ActiveSchedules: schedules,
            RecentAllocations: allocs
        );
    }

    public Section174Schedule CreateSchedule(int taxYear, JurisdictionResearchType jurisdiction, double totalExpenditureUsd)
    {
        double yr1Deduction;
        double annualSubsequent;

        if (jurisdiction == JurisdictionResearchType.UnitedStatesDomestic5Year)
        {
            yr1Deduction = totalExpenditureUsd * 0.10; // 10% mid-year convention in year 1
            annualSubsequent = totalExpenditureUsd * 0.20; // 20% in years 2 through 5
        }
        else
        {
            yr1Deduction = totalExpenditureUsd * 0.0333; // 3.33% mid-year convention in year 1
            annualSubsequent = totalExpenditureUsd * 0.0667; // 6.67% in years 2 through 15
        }

        double taxShield = Math.Round(yr1Deduction * 0.21, 2);

        var schedule = new Section174Schedule(
            $"SCHED-174-{taxYear}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            taxYear,
            jurisdiction,
            totalExpenditureUsd,
            Math.Round(yr1Deduction, 2),
            Math.Round(annualSubsequent, 2),
            taxShield,
            DateTime.UtcNow
        );
        _schedules.TryAdd(schedule.ScheduleId, schedule);
        return schedule;
    }

    public EngineeringLaborAllocation AllocateLabor(string sprintId, string squadName, double payrollUsd, double qualifyingRatio, JurisdictionResearchType jurisdiction)
    {
        double qualifying = Math.Round(payrollUsd * qualifyingRatio, 2);

        var alloc = new EngineeringLaborAllocation(
            $"ALLOC-LABOR-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            sprintId,
            squadName,
            payrollUsd,
            qualifyingRatio,
            jurisdiction,
            qualifying
        );
        _allocations.TryAdd(alloc.AllocationId, alloc);
        return alloc;
    }
}
