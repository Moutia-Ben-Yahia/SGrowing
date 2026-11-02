using System;

namespace SGrowing.Api.Modules.Finance.SovereignDebtClimateResilience;

public sealed class SovereignDebtClimateResilienceEngine : ISovereignDebtClimateResilienceEngine
{
    public SovereignDebtAnalysisResult AnalyzeDebtSustainability(SovereignDebtTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var rMinusG = telemetry.RealEffectiveInterestRatePercent - telemetry.RealGdpGrowthRatePercent;

        // Debt compounding factor: (r - g)/(1 + g) * d
        var denom = Math.Max(0.1, 100.0 + telemetry.RealGdpGrowthRatePercent);
        var compoundingTerm = (rMinusG / denom) * telemetry.DebtToGdpRatioPercent;

        // Climate damage fiscal risk addition
        var cvi = Math.Clamp(telemetry.ClimateVulnerabilityIndex, 0.0, 1.0);
        var climateRiskAddition = telemetry.ProjectedAnnualClimateDamagePercentOfGdp * (1.0 + 0.45 * cvi);

        // Required primary surplus to stabilize debt-to-GDP
        var requiredSurplus = compoundingTerm + climateRiskAddition;
        var fiscalGap = requiredSurplus - telemetry.PrimaryBalancePercentOfGdp;

        // Default probability model
        var spreadScore = telemetry.SovereignBondSpreadBasisPoints / 800.0;
        var debtScore = telemetry.DebtToGdpRatioPercent / 90.0;
        var fxScore = telemetry.ForeignCurrencyDebtSharePercent / 60.0;
        var logitExponent = 0.9 * spreadScore + 0.6 * debtScore + 0.4 * cvi + 0.3 * fxScore - 2.2;
        var defaultProbability = Math.Clamp(100.0 / (1.0 + Math.Exp(-logitExponent)), 0.2, 98.5);

        string regime;
        string policy;
        bool isSustainable;

        if (telemetry.DebtToGdpRatioPercent > 115.0 || telemetry.SovereignBondSpreadBasisPoints > 900.0)
        {
            regime = "SOVEREIGN_DEBT_DISTRESS_DEFAULT_HAZARD";
            policy = "Sovereign debt trajectory explosive (spread > 900 bps). Activate emergency Paris Club / Common Framework debt restructuring and liquidity backstop.";
            isSustainable = false;
        }
        else if (fiscalGap > 4.5)
        {
            regime = "CLIMATE_INDUCED_FISCAL_CLIFF_WARNING";
            policy = "Climate catastrophe damages and adaptation spending widen fiscal gap beyond 4.5% of GDP. Execute debt-for-nature swap and parametric catastrophe bond issuance.";
            isSustainable = false;
        }
        else if (rMinusG > 3.0)
        {
            regime = "ADVERSE_SNOWBALL_DYNAMICS_TIGHTENING";
            policy = "Real borrowing cost significantly exceeds GDP growth rate (r - g > 3%). Tighten fiscal stance and extend debt maturity profile.";
            isSustainable = false;
        }
        else
        {
            regime = "SUSTAINABLE_CLIMATE_RESILIENT_FISCAL_PATH";
            policy = "Sovereign debt trajectory within sustainable boundary with resilient climate buffers and adequate primary surplus.";
            isSustainable = true;
        }

        return new SovereignDebtAnalysisResult(
            SovereignCountryCode: telemetry.SovereignCountryCode,
            SnowballDifferentialPercent: rMinusG,
            RequiredPrimarySurplusPercentOfGdp: requiredSurplus,
            ProjectedFiscalGapPercentOfGdp: fiscalGap,
            ClimateAdjustedDefaultProbabilityPercent: defaultProbability,
            FiscalSustainabilityRegime: regime,
            PolicyRecommendation: policy,
            IsFiscalSustainable: isSustainable
        );
    }
}
