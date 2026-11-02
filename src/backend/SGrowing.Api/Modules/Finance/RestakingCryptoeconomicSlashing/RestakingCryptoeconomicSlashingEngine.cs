using System;

namespace SGrowing.Api.Modules.Finance.RestakingCryptoeconomicSlashing;

public interface IRestakingCryptoeconomicSlashingEngine
{
    RestakingPoolAnalysisResult AnalyzeRestakingSecurity(RestakingPoolTelemetry telemetry);
}

public sealed class RestakingCryptoeconomicSlashingEngine : IRestakingCryptoeconomicSlashingEngine
{
    public RestakingPoolAnalysisResult AnalyzeRestakingSecurity(RestakingPoolTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.TotalRestakedEthAmount <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Restaked ETH amount must be strictly positive.");
        }

        double ethPrice = Math.Max(1.0, telemetry.EthPriceUsd);
        double attributableSecurityUsd = telemetry.TotalRestakedEthAmount * ethPrice;
        double protectedTvl = Math.Max(1.0, telemetry.ProtectedAvsTvlUsd);
        double securityCoverageRatio = attributableSecurityUsd / protectedTvl;

        double correlatedSlashingRate = 0.0;
        double totalSlashedEth = 0.0;

        if (telemetry.IsEquivocationDoubleSignProven)
        {
            double faultFraction = Math.Clamp(telemetry.FaultyOperatorStakeEth / telemetry.TotalRestakedEthAmount, 0.0, 1.0);
            double baseSlash = Math.Clamp(telemetry.BaseSlashingFraction, 0.01, 0.50);
            // Quadratic penalty on correlated faults: SlashRate = base + 3.0 * (faultFraction)^2
            correlatedSlashingRate = Math.Min(1.0, baseSlash + (3.0 * Math.Pow(faultFraction, 2.0)));
            totalSlashedEth = telemetry.FaultyOperatorStakeEth * correlatedSlashingRate;
        }

        double correlatedSlashingPercent = correlatedSlashingRate * 100.0;
        double poolLossPercent = (totalSlashedEth / telemetry.TotalRestakedEthAmount) * 100.0;

        // Net yield: Gross * (1 - operatorFee - protocolFee) - poolLoss
        double feeFraction = (telemetry.OperatorFeeCommissionPercent / 100.0) + (telemetry.ProtocolReserveFeePercent / 100.0);
        double netYield = (telemetry.GrossAvsYieldPercent * Math.Max(0.0, 1.0 - feeFraction)) - poolLossPercent;
        netYield = Math.Max(0.0, netYield);

        string state;
        string action;
        bool isAdequate;

        double faultRatio = telemetry.FaultyOperatorStakeEth / telemetry.TotalRestakedEthAmount;
        if (telemetry.IsEquivocationDoubleSignProven && faultRatio >= 0.05)
        {
            state = "EQUIVOCATION_CORRELATED_SLASH_CRITICAL";
            action = "EXECUTE_CORRELATED_QUADRATIC_SLASH_AND_EJECT_OPERATORS";
            isAdequate = false;
        }
        else if (securityCoverageRatio < 1.25)
        {
            state = "AVS_SECURITY_DEFICIT_WARNING";
            action = "BOOST_RESTAKING_YIELD_INCENTIVE_AND_ALERT_AVS_CONSENSUS";
            isAdequate = false;
        }
        else
        {
            state = "RESTAKING_CONSENSUS_HEALTHY";
            action = "DISPATCH_PERIODIC_CONSENSUS_REWARD_DISTRIBUTIONS";
            isAdequate = true;
        }

        return new RestakingPoolAnalysisResult(
            telemetry.AvsClusterId,
            Math.Round(attributableSecurityUsd, 2),
            Math.Round(securityCoverageRatio, 3),
            Math.Round(correlatedSlashingPercent, 2),
            Math.Round(totalSlashedEth, 2),
            Math.Round(netYield, 3),
            state,
            action,
            isAdequate,
            DateTimeOffset.UtcNow);
    }
}
