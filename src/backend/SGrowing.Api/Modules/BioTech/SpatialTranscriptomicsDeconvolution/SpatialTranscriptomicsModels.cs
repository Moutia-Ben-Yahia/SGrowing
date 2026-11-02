namespace SGrowing.Api.Modules.BioTech.SpatialTranscriptomicsDeconvolution;

public record CellSpotTelemetry(
    string CellId,
    double SpatialCoordinateXUm,
    double SpatialCoordinateYUm,
    double CellAreaUm2,
    int DetectedBarcodeCount,
    int OpticalMiscallCount,
    Dictionary<string, int> MarkerGeneCounts);

public record SpatialTranscriptomicsRequest(
    string SlideId,
    string TissueType,
    int ImagingRoundsCount,
    List<CellSpotTelemetry> CellSpots);

public record SpatialTranscriptomicsAnalysisResult(
    string SlideId,
    int TotalCellsAnalyzed,
    double GlobalOpticalMiscallRatePercent,
    bool IsOpticalQualityAcceptable,
    Dictionary<string, double> DeconvolvedCellTypeFractions,
    bool TertiaryLymphoidStructureDetected,
    double TlsColocalizationScore,
    string SpatialMicroenvironmentSummary);
