using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.TreasuryYield;

public interface ITreasuryYieldEngine
{
    TreasuryYieldOverview GetOverview();
    SweepExecutionResult SweepSurplus(SweepTreasurySurplusCommand command);
}

public class TreasuryYieldEngine : ITreasuryYieldEngine
{
    private decimal _currentCashReserve = 7450000m;
    private readonly decimal _operatingBuffer = 2500000m; // Hard reserve floor
    private readonly ConcurrentDictionary<string, TreasuryYieldAllocation> _allocations = new();
    private readonly List<CashForecastPeriod> _projections = new();
    private readonly object _lock = new();

    public TreasuryYieldEngine()
    {
        SeedData();
    }

    private void SeedData()
    {
        // Stochastic 30/60/90 day forecasts
        _projections.Add(new CashForecastPeriod("30-Day Outlook", 30, 1850000m, 1120000m, +730000m, 8180000m, 96.5));
        _projections.Add(new CashForecastPeriod("60-Day Outlook", 60, 3620000m, 2280000m, +1340000m, 8790000m, 91.8));
        _projections.Add(new CashForecastPeriod("90-Day Outlook", 90, 5840000m, 3550000m, +2290000m, 9740000m, 87.2));

        var a1 = new TreasuryYieldAllocation(
            InstrumentId: "US-TBILL-4W",
            InstrumentName: "US Treasury 4-Week Bills (Direct Auction)",
            CurrentAllocatedAmount: 2200000m,
            RecommendedAllocationAmount: 3000000m,
            TargetApyPercentage: 5.25m,
            ProjectedAnnualYield: 115500m,
            LiquidityDaysTPlus: 1
        );

        var a2 = new TreasuryYieldAllocation(
            InstrumentId: "INST-MMF-PRIME",
            InstrumentName: "Goldman Sachs Institutional Prime MMF",
            CurrentAllocatedAmount: 1800000m,
            RecommendedAllocationAmount: 2200000m,
            TargetApyPercentage: 5.10m,
            ProjectedAnnualYield: 91800m,
            LiquidityDaysTPlus: 0
        );

        var a3 = new TreasuryYieldAllocation(
            InstrumentId: "FED-REPO-OVERNIGHT",
            InstrumentName: "High-Yield Overnight Federal Repo Sweep",
            CurrentAllocatedAmount: 950000m,
            RecommendedAllocationAmount: 1250000m,
            TargetApyPercentage: 4.95m,
            ProjectedAnnualYield: 47025m,
            LiquidityDaysTPlus: 0
        );

        _allocations[a1.InstrumentId] = a1;
        _allocations[a2.InstrumentId] = a2;
        _allocations[a3.InstrumentId] = a3;
    }

    public TreasuryYieldOverview GetOverview()
    {
        lock (_lock)
        {
            var allocationsList = _allocations.Values.OrderByDescending(a => a.TargetApyPercentage).ToList();
            var totalAllocated = allocationsList.Sum(a => a.CurrentAllocatedAmount);
            var surplus = Math.Max(0m, _currentCashReserve - _operatingBuffer);

            var totalAnnualYield = allocationsList.Sum(a => a.CurrentAllocatedAmount * (a.TargetApyPercentage / 100.0m));
            var blendedApy = totalAllocated > 0 ? (double)(totalAnnualYield / totalAllocated) * 100.0 : 5.12;

            // Monthly net burn or runway calculation
            var monthlyOpex = 1120000m;
            var runwayMonths = monthlyOpex > 0 ? (double)(_currentCashReserve / monthlyOpex) : 36.0;

            return new TreasuryYieldOverview(
                CurrentCashReserve: _currentCashReserve,
                RequiredOperatingBuffer: _operatingBuffer,
                SurplusDeployableCapital: surplus,
                BlendedPortfolioApy: Math.Round(blendedApy, 2),
                ProjectedTotalAnnualYield: Math.Round(totalAnnualYield, 2),
                RunwayMonths: Math.Round(runwayMonths, 1),
                Projections: _projections,
                Allocations: allocationsList
            );
        }
    }

    public SweepExecutionResult SweepSurplus(SweepTreasurySurplusCommand command)
    {
        lock (_lock)
        {
            if (!_allocations.TryGetValue(command.InstrumentId, out var existing))
            {
                throw new KeyNotFoundException($"Treasury instrument {command.InstrumentId} not found.");
            }

            var availableSurplus = Math.Max(0m, _currentCashReserve - _operatingBuffer);
            var sweepAmount = Math.Min(command.SweepAmount, availableSurplus);
            if (sweepAmount <= 0)
            {
                throw new InvalidOperationException("No surplus deployable capital available above operating buffer threshold.");
            }

            var prev = existing.CurrentAllocatedAmount;
            var newAlloc = prev + sweepAmount;
            var addedYield = sweepAmount * (existing.TargetApyPercentage / 100.0m);

            var updated = existing with
            {
                CurrentAllocatedAmount = newAlloc,
                ProjectedAnnualYield = Math.Round(newAlloc * (existing.TargetApyPercentage / 100.0m), 2)
            };

            _allocations[command.InstrumentId] = updated;
            _currentCashReserve -= sweepAmount;

            var remainingSurplus = Math.Max(0m, _currentCashReserve - _operatingBuffer);
            var refCode = $"TRSY-SWEEP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

            return new SweepExecutionResult(
                InstrumentId: command.InstrumentId,
                PreviousAllocation: prev,
                NewAllocation: newAlloc,
                RemainingSurplus: remainingSurplus,
                AdditionalYieldEarned: Math.Round(addedYield, 2),
                ConfirmationReference: refCode
            );
        }
    }
}
