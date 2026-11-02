namespace SGrowing.Api.Modules.Energy.NeutrinoGeotomographyCoreDensity;

public sealed record NeutrinoTomographyTelemetry(
    string TomographyArrayId,
    double NeutrinoEnergyTeV,
    double EarthChordNadirAngleDegrees,
    double MeasuredNeutrinoTransmittance,
    double MeasuredGeoAntineutrinoFluxTnu,
    double ThoriumToUraniumMassRatio,
    double InferredGeoReactorPowerTerawatts,
    bool MatterEnhancedOscillationMswCorrected
);

public sealed record NeutrinoTomographyAnalysisResult(
    string TomographyArrayId,
    double InferredAverageChordDensityGramsPerCm3,
    double TheoreticalExpectedTransmittance,
    double RadiogenicHeatProductionTerawatts,
    double CoreMantleBoundaryDensityJumpRatio,
    string PlanetaryGeophysicsRegime,
    string GeophysicalInterpretation,
    bool IsPlanetaryModelConsistent
);

public interface INeutrinoGeotomographyCoreDensityEngine
{
    NeutrinoTomographyAnalysisResult AnalyzePlanetaryInterior(NeutrinoTomographyTelemetry telemetry);
}
