using SGrowing.Api.Modules.Finance.CdoGaussianCopulaTrancheCorrelation;
using Xunit;

namespace SGrowing.Tests.Finance;

public class CdoCopulaTrancheTests
{
    private readonly CdoCopulaTrancheEngine _engine = new();

    [Fact]
    public void AnalyzeTranche_NominalMezzanine_ReturnsProtectedAndContained()
    {
        var telemetry = new CdoCopulaTrancheTelemetry
        {
            DealIdentifier = "CDO-SYNTHETIC-2026-A",
            TrancheCategory = "Mezzanine_3_7_Percent",
            TrancheAttachmentPointPercent = 3.0,
            TrancheDetachmentPointPercent = 7.0,
            AssetPairwiseDefaultCorrelation = 0.22,
            ExtremeTailCorrelationMultiplier = 1.4,
            AverageObligorAnnualDefaultProbabilityPercent = 0.55,
            ExpectedPortfolioRecoveryRatePercent = 40.0,
            ObligorCountInCollateralBasket = 120,
            FiveYearCdsSpreadBasisPoints = 180.0
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.Equal("CDO-SYNTHETIC-2026-A", result.DealIdentifier);
        Assert.True(result.IsTrancheInvestmentGradeProtected);
        Assert.True(result.IsCorrelationRiskContained);
        Assert.True(result.IsSuperSeniorSafeFromSpillover);
        Assert.True(result.CalculatedTrancheExpectedLossPercent <= 15.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_SystemicCorrelationSpike_TriggersContagionAlert()
    {
        var telemetry = new CdoCopulaTrancheTelemetry
        {
            DealIdentifier = "CDO-CONTAGION-STRESS",
            AssetPairwiseDefaultCorrelation = 0.58, // High correlation
            ExtremeTailCorrelationMultiplier = 1.8 // Tail spike
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.False(result.IsCorrelationRiskContained);
        Assert.Contains("SYSTEMIC_CORRELATION_SPIKE_TAIL_CONTAGION", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeTranche_FirstLossEquityDepletion_TriggersEquityAlert()
    {
        var telemetry = new CdoCopulaTrancheTelemetry
        {
            DealIdentifier = "CDO-EQUITY-FIRST-LOSS",
            TrancheAttachmentPointPercent = 0.0, // Equity tranche
            TrancheDetachmentPointPercent = 3.0,
            AverageObligorAnnualDefaultProbabilityPercent = 3.8
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.Contains("EQUITY_TRANCHE_FIRST_LOSS_DEPLETION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_SevereDefaultRate_TriggersMezzanineErosion()
    {
        var telemetry = new CdoCopulaTrancheTelemetry
        {
            DealIdentifier = "CDO-MEZZ-EROSION",
            TrancheAttachmentPointPercent = 3.0,
            TrancheDetachmentPointPercent = 7.0,
            AverageObligorAnnualDefaultProbabilityPercent = 4.5,
            AssetPairwiseDefaultCorrelation = 0.40
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.False(result.IsTrancheInvestmentGradeProtected);
        Assert.Contains("MEZZANINE_SUBORDINATION_EROSION_ALERT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_TailContagionLoss_ExceedsBaseExpectedLoss()
    {
        var telemetry = new CdoCopulaTrancheTelemetry
        {
            DealIdentifier = "CDO-TAIL-RISK-COMPARE",
            TrancheAttachmentPointPercent = 3.0,
            TrancheDetachmentPointPercent = 7.0,
            AssetPairwiseDefaultCorrelation = 0.35,
            ExtremeTailCorrelationMultiplier = 1.5
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.True(result.StressedTailContagionLossPercent >= result.CalculatedTrancheExpectedLossPercent);
    }
}
