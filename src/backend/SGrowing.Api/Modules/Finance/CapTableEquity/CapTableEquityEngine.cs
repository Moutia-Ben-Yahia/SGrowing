using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.CapTableEquity;

public sealed class CapTableEquityEngine
{
    private readonly ConcurrentDictionary<string, EquityStakeholderRecord> _stakeholders = new();
    private readonly ConcurrentDictionary<string, StockOptionGrantRecord> _grants = new();

    public CapTableEquityEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var s1 = new EquityStakeholderRecord(
            "STK-FND-01",
            "Founders Holding Entity LLC",
            "FOUNDER",
            6000000,
            60.0,
            50000.00m,
            "COMMON"
        );

        var s2 = new EquityStakeholderRecord(
            "STK-INV-02",
            "Apex Horizon Ventures Series A",
            "INVESTOR_PREFERRED",
            2500000,
            25.0,
            15000000.00m,
            "SERIES_A"
        );

        var s3 = new EquityStakeholderRecord(
            "STK-OPT-03",
            "Employee Stock Option Pool (ESOP)",
            "EMPLOYEE_COMMON",
            1500000,
            15.0,
            0.00m,
            "COMMON"
        );

        _stakeholders[s1.StakeholderId] = s1;
        _stakeholders[s2.StakeholderId] = s2;
        _stakeholders[s3.StakeholderId] = s3;

        var g1 = new StockOptionGrantRecord(
            "GRT-2026-001",
            "Dr. Elena Rostova (Chief AI Architect)",
            120000,
            1.25m,
            DateTime.UtcNow.AddMonths(-18),
            DateTime.UtcNow.AddMonths(-18),
            12,
            48,
            45000,
            "VESTING_ACTIVE"
        );

        _grants[g1.GrantId] = g1;
    }

    public CapTableOverview GetOverview()
    {
        var shList = _stakeholders.Values.ToList();
        var grtList = _grants.Values.ToList();
        var totalShares = shList.Sum(s => s.SharesCount);

        return new CapTableOverview(
            totalShares,
            75000000.00m, // 409A Fair Market Valuation $75M
            7.50m,         // $7.50 per share FMV
            shList.Count,
            shList,
            grtList
        );
    }

    public StockOptionGrantRecord GrantStockOption(GrantStockOptionRequest request)
    {
        var grantId = $"GRT-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var grant = new StockOptionGrantRecord(
            grantId,
            request.EmployeeName,
            request.SharesCount,
            request.StrikePriceUsd,
            DateTime.UtcNow,
            DateTime.UtcNow,
            request.VestingCliffMonths,
            request.TotalVestingMonths,
            0,
            "VESTING_ACTIVE"
        );

        _grants[grantId] = grant;
        return grant;
    }

    public EquityStakeholderRecord SimulateDilution(SimulateDilutionRequest request)
    {
        var postMoney = request.PreMoneyValuationUsd + request.NewRoundInvestmentAmountUsd;
        var dilutionPct = (double)(request.NewRoundInvestmentAmountUsd / postMoney) * 100.0;
        var newShares = (long)(10000000 * (dilutionPct / 100.0));
        var stkId = $"STK-{request.NewRoundShareClass}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var newStakeholder = new EquityStakeholderRecord(
            stkId,
            $"New Round Syndicate ({request.NewRoundShareClass})",
            "INVESTOR_PREFERRED",
            newShares,
            dilutionPct,
            request.NewRoundInvestmentAmountUsd,
            request.NewRoundShareClass
        );

        _stakeholders[stkId] = newStakeholder;
        return newStakeholder;
    }
}
