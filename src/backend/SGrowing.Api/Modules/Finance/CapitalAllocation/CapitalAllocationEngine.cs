using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Finance.CapitalAllocation;

public class CapitalAllocationEngine
{
    private readonly ConcurrentDictionary<string, TreasuryAssetAllocation> _allocations = new();
    private readonly ConcurrentBag<WorkingCapitalOptimizationRecord> _optimizations = new();

    public CapitalAllocationEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var a1 = new TreasuryAssetAllocation(
            "ALLOC-001",
            "SOVEREIGN_TBILLS_USD",
            "USD",
            15000000.00m,
            5.20,
            90.0,
            "US Federal Reserve Treasury Direct",
            "ACTIVE_YIELDING"
        );

        var a2 = new TreasuryAssetAllocation(
            "ALLOC-002",
            "OVERNIGHT_REPO_EUR",
            "EUR",
            8500000.00m,
            3.85,
            1.0,
            "European Central Bank Facility",
            "ACTIVE_YIELDING"
        );

        _allocations[a1.AllocationId] = a1;
        _allocations[a2.AllocationId] = a2;

        _optimizations.Add(new WorkingCapitalOptimizationRecord(
            "OPT-001",
            24250000.00m,
            850000.00m,
            28.5,
            1200000.00m,
            1107500.00m,
            DateTime.UtcNow.AddDays(-3)
        ));
    }

    public CapitalAllocationOverview GetOverview()
    {
        var allocList = _allocations.Values.ToList();
        var optList = _optimizations.ToList();

        decimal totalValue = allocList.Sum(a => a.AllocatedAmount);
        double weightedYield = totalValue > 0
            ? (double)(allocList.Sum(a => a.AllocatedAmount * (decimal)a.AnnualYieldPercentage) / totalValue)
            : 4.5;

        double runway = optList.Count > 0 ? optList.First().RunwayMonths : 24.0;

        return new CapitalAllocationOverview(
            totalValue,
            Math.Round(weightedYield, 2),
            Math.Round(runway, 1),
            allocList.Count(a => a.Status == "ACTIVE_YIELDING"),
            allocList,
            optList
        );
    }

    public TreasuryAssetAllocation AllocateCapital(AllocateCapitalRequest request)
    {
        var allocId = $"ALLOC-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var allocation = new TreasuryAssetAllocation(
            allocId,
            request.AssetClass,
            request.CurrencyIso,
            request.AllocatedAmount,
            request.AnnualYieldPercentage,
            request.MaturityDays,
            request.CounterpartyInstitution,
            "ACTIVE_YIELDING"
        );

        _allocations[allocId] = allocation;
        return allocation;
    }

    public WorkingCapitalOptimizationRecord OptimizeRunway(OptimizeRunwayRequest request)
    {
        var optId = $"OPT-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        decimal totalValue = _allocations.Values.Sum(a => a.AllocatedAmount);
        double runway = request.MonthlyBurnRateDollars > 0 ? (double)(totalValue / request.MonthlyBurnRateDollars) : 36.0;
        decimal interest = totalValue * 0.048m;

        var opt = new WorkingCapitalOptimizationRecord(
            optId,
            totalValue,
            request.MonthlyBurnRateDollars,
            Math.Round(runway, 1),
            totalValue * 0.05m,
            interest,
            DateTime.UtcNow
        );

        _optimizations.Add(opt);
        return opt;
    }
}
