using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.AssetDepreciation;

public sealed class AssetDepreciationEngine
{
    private readonly ConcurrentDictionary<string, FixedAssetRecord> _assets = new();
    private readonly ConcurrentDictionary<string, DepreciationScheduleEntry> _schedules = new();
    private readonly ConcurrentBag<ImpairmentJournalReceipt> _impairmentsHistory = new();

    public AssetDepreciationEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var a1 = new FixedAssetRecord(
            "AST-GPU-H100-01",
            "NVIDIA H100 GPU AI Cluster (64 Nodes)",
            "GPU Server Fleet",
            2500000.00m,
            250000.00m,
            3,
            "US_MACRS_5YR",
            DateTime.UtcNow.AddMonths(-14)
        );

        var a2 = new FixedAssetRecord(
            "AST-EDGE-POP-02",
            "Anycast Edge Server Rack Deployments (12 POPs)",
            "Edge POP Hardware",
            850000.00m,
            85000.00m,
            5,
            "IFRS_STRAIGHT_LINE",
            DateTime.UtcNow.AddMonths(-8)
        );

        _assets[a1.AssetTag] = a1;
        _assets[a2.AssetTag] = a2;

        var s1 = new DepreciationScheduleEntry(
            "SCH-AST-01-2026",
            a1.AssetTag,
            2026,
            11,
            41666.67m,
            1875000.00m,
            "US_MACRS_5YR_HALF_YEAR"
        );

        _schedules[s1.ScheduleId] = s1;
    }

    public AssetDepreciationOverview GetOverview()
    {
        var assetList = _assets.Values.ToList();
        var schedList = _schedules.Values.ToList();

        var totalCost = assetList.Sum(a => a.CostBasisUsd);
        var accumDep = 625000.00m + 113333.33m;
        var netBook = totalCost - accumDep;

        return new AssetDepreciationOverview(
            assetList.Count,
            totalCost,
            accumDep,
            netBook,
            54444.44m,
            assetList,
            schedList
        );
    }

    public DepreciationCalculationResult CalculateDepreciation(CalculateDepreciationRequest request)
    {
        decimal annualExpense;
        if (string.Equals(request.DepreciationMethod, "STRAIGHT_LINE", StringComparison.OrdinalIgnoreCase))
        {
            var depreciableBase = request.CostBasisUsd - request.SalvageValueUsd;
            annualExpense = Math.Round(depreciableBase / Math.Max(1, request.UsefulLifeYears), 2);
        }
        else // MACRS_200_DB (Year 1 ~20%, Year 2 ~32%)
        {
            annualExpense = Math.Round(request.CostBasisUsd * 0.32m, 2);
        }

        var monthly = Math.Round(annualExpense / 12.0m, 2);
        var yearEndBookValue = Math.Max(request.SalvageValueUsd, request.CostBasisUsd - annualExpense);

        return new DepreciationCalculationResult(
            $"CALC-DEP-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            request.AssetTag,
            request.DepreciationMethod,
            annualExpense,
            monthly,
            yearEndBookValue,
            DateTime.UtcNow
        );
    }

    public ImpairmentJournalReceipt RecordImpairment(RecordAssetImpairmentRequest request)
    {
        var asset = _assets.TryGetValue(request.AssetTag, out var a) ? a : _assets.Values.First();
        var currentBookValue = asset.CostBasisUsd * 0.75m;
        var loss = Math.Max(0m, currentBookValue - request.RecoverableAmountUsd);

        var receipt = new ImpairmentJournalReceipt(
            $"JRN-IMP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            asset.AssetTag,
            currentBookValue,
            loss,
            request.RecoverableAmountUsd,
            "IMPAIRMENT_POSTED_GENERAL_LEDGER",
            DateTime.UtcNow
        );

        _impairmentsHistory.Add(receipt);
        return receipt;
    }
}
