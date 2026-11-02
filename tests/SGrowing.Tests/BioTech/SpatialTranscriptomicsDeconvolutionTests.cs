namespace SGrowing.Tests.BioTech;

using SGrowing.Api.Modules.BioTech.SpatialTranscriptomicsDeconvolution;
using Xunit;

public class SpatialTranscriptomicsDeconvolutionTests
{
    private readonly SpatialTranscriptomicsDeconvolutionEngine _engine = new();

    [Fact]
    public void DeconvolveSpatialSlide_TertiaryLymphoidStructure_DetectsCoLocalizedAggregate()
    {
        // 2 B-cells and 2 T-cells within 30 um of each other -> 4 proximate B-T pairs
        var spots = new List<CellSpotTelemetry>
        {
            new("CELL-B1", 100.0, 100.0, 150.0, 800, 2, new() { ["MS4A1"] = 45, ["CD3E"] = 0 }),
            new("CELL-B2", 110.0, 105.0, 160.0, 850, 1, new() { ["MS4A1"] = 52, ["CD3E"] = 1 }),
            new("CELL-T1", 115.0, 110.0, 140.0, 900, 2, new() { ["CD3E"] = 60, ["MS4A1"] = 0 }),
            new("CELL-T2", 120.0, 108.0, 145.0, 920, 3, new() { ["CD3E"] = 58, ["MS4A1"] = 2 }),
            new("CELL-MAC1", 300.0, 300.0, 200.0, 600, 1, new() { ["CD68"] = 35 })
        };

        var request = new SpatialTranscriptomicsRequest(
            SlideId: "CRC-SLIDE-TLS-01",
            TissueType: "Colorectal-Adenocarcinoma",
            ImagingRoundsCount: 16,
            CellSpots: spots);

        var result = _engine.DeconvolveSpatialSlide(request);

        Assert.Equal("CRC-SLIDE-TLS-01", result.SlideId);
        Assert.Equal(5, result.TotalCellsAnalyzed);
        Assert.True(result.IsOpticalQualityAcceptable);
        Assert.True(result.TertiaryLymphoidStructureDetected);
        Assert.True(result.TlsColocalizationScore > 0);
        Assert.Contains("ACTIVE TERTIARY LYMPHOID STRUCTURE", result.SpatialMicroenvironmentSummary);
        Assert.Equal(0.4, result.DeconvolvedCellTypeFractions["B-Cell"]); // 2 / 5
        Assert.Equal(0.4, result.DeconvolvedCellTypeFractions["T-Cell"]); // 2 / 5
    }

    [Fact]
    public void DeconvolveSpatialSlide_HighOpticalMiscallRate_FlagsOpticalQcFailure()
    {
        var spots = new List<CellSpotTelemetry>
        {
            new("CELL-1", 10.0, 10.0, 100.0, 500, 45, new() { ["EPCAM"] = 50 }) // 45 / 500 = 9% miscall rate
        };

        var request = new SpatialTranscriptomicsRequest(
            SlideId: "BAD-OPTICS-SLIDE",
            TissueType: "Breast-Cancer-FFPE",
            ImagingRoundsCount: 16,
            CellSpots: spots);

        var result = _engine.DeconvolveSpatialSlide(request);

        Assert.False(result.IsOpticalQualityAcceptable);
        Assert.True(result.GlobalOpticalMiscallRatePercent > 1.0);
        Assert.Contains("OPTICAL QC FAILURE", result.SpatialMicroenvironmentSummary);
    }

    [Fact]
    public void DeconvolveSpatialSlide_DiffuseTissueNoTls_ReportsNominalDiffuseInfiltration()
    {
        // Widely dispersed cells (> 200 um apart)
        var spots = new List<CellSpotTelemetry>
        {
            new("C1", 0.0, 0.0, 120.0, 600, 1, new() { ["EPCAM"] = 40 }),
            new("C2", 250.0, 250.0, 130.0, 620, 1, new() { ["PECAM1"] = 35 }),
            new("C3", 600.0, 600.0, 125.0, 610, 0, new() { ["CD68"] = 30 })
        };

        var request = new SpatialTranscriptomicsRequest(
            SlideId: "DIFFUSE-SLIDE",
            TissueType: "Healthy-Stroma",
            ImagingRoundsCount: 16,
            CellSpots: spots);

        var result = _engine.DeconvolveSpatialSlide(request);

        Assert.True(result.IsOpticalQualityAcceptable);
        Assert.False(result.TertiaryLymphoidStructureDetected);
        Assert.Contains("SPATIAL PROFILE NOMINAL", result.SpatialMicroenvironmentSummary);
    }

    [Fact]
    public void DeconvolveSpatialSlide_NullSlideId_ThrowsArgumentException()
    {
        var invalid = new SpatialTranscriptomicsRequest(
            SlideId: "",
            TissueType: "Tumor",
            ImagingRoundsCount: 16,
            CellSpots: []);

        Assert.Throws<ArgumentException>(() => _engine.DeconvolveSpatialSlide(invalid));
    }
}
