using SGrowing.Api.Modules.Finance.CdsIndexTrancheCorrelation;
using Xunit;

namespace SGrowing.Tests.Finance;

public class CdsIndexTrancheTests
{
    private readonly CdsIndexTrancheEngine _engine = new();

    [Fact]
    public void AnalyzeTranche_NominalMezzanineTranche_ReturnsArbitrageFree()
    {
        var telemetry = new CdsIndexTrancheTelemetry
        {
            TranchePortfolioUid = "CDX-MEZZ-001",
            IndexFamily = "CDX_NA_IG_Series43",
            TrancheAttachmentRange = "3-7_Mezzanine",
            IndexSpreadBasisPoints = 54.2,
            AverageConstituentCdsSpreadBps = 52.8,
            BaseCorrelationPercent = 38.5,
            BaseCorrelationSlopePerDetachmentPoint = 1.45,
            TrancheExpectedLossDensity = 0.082,
            EquityTrancheDelta = 0.62,
            ImpliedCompoundCorrelationPercent = 28.4
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CDX-MEZZ-001", result.TranchePortfolioUid);
        Assert.Equal(1.40, result.IndexCdsBasisBps);
        Assert.True(result.IsIndexCdsBasisNormal);
        Assert.True(result.IsBaseCorrelationMonotonic);
        Assert.True(result.IsEquityTrancheSolvent);
        Assert.True(result.IsTranchePricingArbitrageFree);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_WideBasisDislocation_TriggersArbitrageAlert()
    {
        var telemetry = new CdsIndexTrancheTelemetry
        {
            TranchePortfolioUid = "CDX-MEZZ-002",
            IndexSpreadBasisPoints = 68.0,
            AverageConstituentCdsSpreadBps = 52.0 // Basis = 16.0 > 7.5 bps
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.False(result.IsIndexCdsBasisNormal);
        Assert.False(result.IsTranchePricingArbitrageFree);
        Assert.Contains("CDX_INDEX_BASIS_ARBITRAGE_DISLOCATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_InvertedBaseCorrelationSmile_TriggersArbitrageViolationAlert()
    {
        var telemetry = new CdsIndexTrancheTelemetry
        {
            TranchePortfolioUid = "CDX-MEZZ-003",
            BaseCorrelationSlopePerDetachmentPoint = -0.35,
            TrancheExpectedLossDensity = -0.015
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.False(result.IsBaseCorrelationMonotonic);
        Assert.False(result.IsTranchePricingArbitrageFree);
        Assert.Contains("BASE_CORRELATION_SMILE_ARBITRAGE_VIOLATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_HighEquityDelta_TriggersFirstLossAlert()
    {
        var telemetry = new CdsIndexTrancheTelemetry
        {
            TranchePortfolioUid = "CDX-MEZZ-004",
            EquityTrancheDelta = 0.92
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.False(result.IsEquityTrancheSolvent);
        Assert.False(result.IsTranchePricingArbitrageFree);
        Assert.Contains("EQUITY_TRANCHE_FIRST_LOSS_EXHAUSTION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTranche_NegativeBasis_CalculatesCorrectly()
    {
        var telemetry = new CdsIndexTrancheTelemetry
        {
            TranchePortfolioUid = "CDX-MEZZ-005",
            IndexSpreadBasisPoints = 50.0,
            AverageConstituentCdsSpreadBps = 53.5
        };

        var result = _engine.AnalyzeTranche(telemetry);

        Assert.Equal(-3.50, result.IndexCdsBasisBps);
        Assert.True(result.IsIndexCdsBasisNormal);
    }
}
