using System;

namespace SGrowing.Api.Modules.Finance.DeFiLendingBadDebtSocialization;

public interface IDeFiLendingBadDebtSocializationEngine
{
    LendingPoolResolutionResult AnalyzeBadDebtResolution(LendingPoolRiskTelemetry telemetry);
}

public sealed class DeFiLendingBadDebtSocializationEngine : IDeFiLendingBadDebtSocializationEngine
{
    public LendingPoolResolutionResult AnalyzeBadDebtResolution(LendingPoolRiskTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.TotalSuppliedLiquidityUsd <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Supplied liquidity must be strictly positive.");
        }

        double slashableFraction = Math.Clamp(telemetry.MaxGovernanceSlashFraction, 0.05, 0.50);
        double slashableGovernanceUsd = telemetry.StakedGovernanceTokenMarketCapUsd * slashableFraction;
        double totalBackstopCapacityUsd = telemetry.ProtocolInsuranceReserveUsd + slashableGovernanceUsd;

        double badDebt = Math.Max(0.0, telemetry.UnderwaterBadDebtUsd);
        double coverageRatio = badDebt > 0.0 ? totalBackstopCapacityUsd / badDebt : 999.0;

        double insuranceDrawnUsd;
        double governanceSlashedUsd;
        double haircutPercent;
        string state;
        string action;
        bool isSolvent;

        if (badDebt == 0.0)
        {
            insuranceDrawnUsd = 0.0;
            governanceSlashedUsd = 0.0;
            haircutPercent = 0.0;
            state = "COLLATERAL_SOLVENCY_HEALTHY";
            action = "NORMAL_MARKET_OPERATIONS_AND_COLLECT_RESERVE_FEES";
            isSolvent = true;
        }
        else if (badDebt <= telemetry.ProtocolInsuranceReserveUsd)
        {
            insuranceDrawnUsd = badDebt;
            governanceSlashedUsd = 0.0;
            haircutPercent = 0.0;
            state = "INSURANCE_BACKSTOP_ABSORPTION";
            action = "DRAW_DOWN_INSURANCE_RESERVE_AND_RESTORE_PARITY";
            isSolvent = true;
        }
        else if (badDebt <= totalBackstopCapacityUsd)
        {
            insuranceDrawnUsd = telemetry.ProtocolInsuranceReserveUsd;
            governanceSlashedUsd = badDebt - telemetry.ProtocolInsuranceReserveUsd;
            haircutPercent = 0.0;
            state = "GOVERNANCE_STAKE_SLASH_ABSORPTION";
            action = "SLASH_STAKED_GOVERNANCE_TOKENS_TO_COVER_DEFICIT";
            isSolvent = true;
        }
        else
        {
            insuranceDrawnUsd = telemetry.ProtocolInsuranceReserveUsd;
            governanceSlashedUsd = slashableGovernanceUsd;
            double uncoveredDeficit = badDebt - totalBackstopCapacityUsd;
            haircutPercent = (uncoveredDeficit / telemetry.TotalSuppliedLiquidityUsd) * 100.0;
            state = "SYSTEMIC_INSOLVENCY_HAIRCUT_TRIGGERED";
            action = "EXECUTE_TIER_3_DEPOSITOR_YIELD_HAIRCUT_AND_PAUSE_BORROWING";
            isSolvent = false;
        }

        return new LendingPoolResolutionResult(
            telemetry.PoolId,
            Math.Round(badDebt, 2),
            Math.Round(totalBackstopCapacityUsd, 2),
            Math.Round(coverageRatio, 3),
            Math.Round(insuranceDrawnUsd, 2),
            Math.Round(governanceSlashedUsd, 2),
            Math.Round(haircutPercent, 3),
            state,
            action,
            isSolvent,
            DateTimeOffset.UtcNow);
    }
}
