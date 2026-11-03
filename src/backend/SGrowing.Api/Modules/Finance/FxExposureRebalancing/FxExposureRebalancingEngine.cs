using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.FxExposureRebalancing;

public interface IFxExposureRebalancingEngine
{
    FxExposureRebalancingOverview GetOverview();
    EvaluateExposureResult EvaluateExposure(EvaluateExposureCommand command);
    ExecuteForwardHedgeResult ExecuteForwardHedge(ExecuteForwardHedgeCommand command);
}

public class FxExposureRebalancingEngine : IFxExposureRebalancingEngine
{
    private readonly List<CurrencyExposurePosition> _exposures =
    [
        new("EUR", 4200000m, 1200000m, 3000000m, 1.0850m, 6.4, 185000m, 4.2),
        new("GBP", 2800000m, 950000m, 1850000m, 1.2940m, 7.8, 142000m, 6.8),
        new("JPY", 450000000m, 90000000m, 360000000m, 0.0065m, 11.2, 210000m, 8.5),
        new("SGD", 1800000m, 600000m, 1200000m, 0.7620m, 4.5, 52000m, 2.1)
    ];

    private readonly List<ForwardHedgeContract> _contracts =
    [
        new("HEDGE-FWD-901", "EUR/USD", 2500000m, 1.0890m, DateTime.UtcNow.AddMonths(3), "JPMorgan Chase", "CashFlowHedge_GAAP"),
        new("HEDGE-FWD-902", "GBP/USD", 1500000m, 1.2980m, DateTime.UtcNow.AddMonths(3), "HSBC Global Markets", "CashFlowHedge_GAAP"),
        new("HEDGE-FWD-903", "USD/JPY", 2000000m, 154.20m, DateTime.UtcNow.AddMonths(1), "Barclays Capital", "FairValueHedge_IFRS")
    ];

    public FxExposureRebalancingOverview GetOverview()
    {
        var totalVar = _exposures.Sum(e => e.ValueAtRisk95Pct);
        var totalHedged = _contracts.Sum(c => c.NotionalAmount);
        var hedgeRatio = 84.5;
        var maxDrift = 5.0;

        return new FxExposureRebalancingOverview(
            totalVar,
            totalHedged,
            hedgeRatio,
            maxDrift,
            _exposures,
            _contracts
        );
    }

    public EvaluateExposureResult EvaluateExposure(EvaluateExposureCommand command)
    {
        var evaluationId = $"EVAL-FX-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var totalNetUsd = 7450000m;
        var totalVar = _exposures.Sum(e => e.ValueAtRisk95Pct);
        var highRisk = _exposures.Where(e => e.UnhedgedDriftPct > 5.0).Select(e => e.CurrencyCode).ToList();

        return new EvaluateExposureResult(
            evaluationId,
            totalNetUsd,
            totalVar,
            highRisk,
            highRisk.Count > 0
        );
    }

    public ExecuteForwardHedgeResult ExecuteForwardHedge(ExecuteForwardHedgeCommand command)
    {
        var contractId = $"HEDGE-FWD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var lockedRate = command.CurrencyPair.StartsWith("EUR") ? 1.0875m : 1.2960m;
        var maturity = DateTime.UtcNow.AddDays(command.DurationDays > 0 ? command.DurationDays : 90);

        return new ExecuteForwardHedgeResult(
            contractId,
            command.CurrencyPair,
            command.NotionalAmount,
            lockedRate,
            maturity,
            $"AUDIT-FX-HEDGE-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}"
        );
    }
}
