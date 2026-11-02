using System;

namespace SGrowing.Api.Modules.Finance.DynamicMarginCollateralVar;

public sealed class DynamicMarginCollateralVarEngine : IDynamicMarginCollateralVarEngine
{
    private const double ZQuantile999 = 3.09023; // N(0,1) 99.9% quantile

    public DynamicMarginVarAnalysisResult AnalyzeMarginRisk(DynamicMarginVarTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var notionalUsd = Math.Max(100.0, telemetry.TotalGrossNotionalPositionUsd);
        var collateralUsd = Math.Max(10.0, telemetry.TotalCollateralValueUsd);
        var annVol = Math.Clamp(telemetry.HistoricalVolatilityAnnualized, 0.05, 3.0);
        var dailyVol = annVol / Math.Sqrt(252.0);

        var xi = Math.Clamp(telemetry.EvtTailIndexShapeParameterXi, 0.0, 0.80);
        var evtMultiplier = 1.0 + (xi * 2.5);

        // 99.9% Value at Risk under heavy-tailed EVT GPD
        var var999 = notionalUsd * dailyVol * ZQuantile999 * evtMultiplier;

        // Expected Shortfall: ES = VaR / (1 - xi) for xi < 1
        var safeXiDenominator = Math.Max(0.20, 1.0 - xi);
        var expectedShortfall = var999 / safeXiDenominator;

        // Effective collateral accounting for haircuts and AMM impermanent loss
        var haircutFraction = Math.Clamp(telemetry.WeightedCollateralHaircutPercent / 100.0, 0.0, 0.90);
        var ilFraction = Math.Clamp(telemetry.ConcentratedLiquidityImpermanentLossPercent / 100.0, 0.0, 0.90);
        var effectiveCollateral = collateralUsd * (1.0 - haircutFraction) * (1.0 - ilFraction);

        var leverage = Math.Max(1.0, telemetry.PortfolioLeverageRatio);
        var requiredMaintenanceMargin = expectedShortfall * (1.0 + (leverage * 0.05));

        var utilization = requiredMaintenanceMargin / Math.Max(1.0, effectiveCollateral);

        string regime;
        string guidance;
        bool isCompliant;

        if (utilization > 1.0)
        {
            regime = "IMMINENT_LIQUIDATION_WATERFALL_HAZARD";
            guidance = "Required dynamic maintenance margin exceeds net effective collateral (utilization > 100%). Trigger immediate collateral top-up or automated TWAP position deleveraging.";
            isCompliant = false;
        }
        else if (utilization > 0.80)
        {
            regime = "CRITICAL_COLLATERAL_DEFICIT_WARNING";
            guidance = "Margin utilization entered critical warning zone (> 80%). High sensitivity to tail risk spikes. Restrict new levered positions and increase stablecoin collateral.";
            isCompliant = false;
        }
        else if (xi > 0.35 && telemetry.ConcentratedLiquidityImpermanentLossPercent > 12.0)
        {
            regime = "FAT_TAILED_IMPERMANENT_LOSS_STRESS";
            guidance = "Heavy-tailed market shock coupled with AMM concentrated liquidity impermanent loss (> 12%). Re-center liquidity tick range or hedge inventory delta.";
            isCompliant = false;
        }
        else
        {
            regime = "PRUDENT_DYNAMIC_MARGIN_RESILIENCE_REGIME";
            guidance = "Account collateral buffer robust against 99.9% EVT tail shock. Dynamic maintenance margin fully satisfied.";
            isCompliant = true;
        }

        return new DynamicMarginVarAnalysisResult(
            AccountPortfolioId: telemetry.AccountPortfolioId,
            ValueAtRisk999Usd: var999,
            ExpectedShortfallUsd: expectedShortfall,
            RequiredDynamicMaintenanceMarginUsd: requiredMaintenanceMargin,
            MarginBufferUtilizationRatio: utilization,
            MarginRiskRegime: regime,
            CollateralRebalancingGuidance: guidance,
            IsMarginCompliant: isCompliant
        );
    }
}
