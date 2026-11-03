namespace SGrowing.Api.Modules.Manufacturing.HighNaEuvStochasticDefects;

public record HighNaEuvTelemetry(
    string ScannerId,
    string WaferFieldId,
    double NumericalAperture,
    double TargetCriticalDimensionNm,
    double MeasuredDoseMilliJoulesPerCm2,
    double MeasuredLineEdgeRoughness3SigmaNm,
    double EuvFlareFraction,
    double AerialImageContrastNils,
    int ObservedStochasticDefectCount);

public record HighNaEuvAnalysisResult(
    string ScannerId,
    string WaferFieldId,
    double TheoreticalRayleighResolutionNm,
    double AnamorphicDepthOfFocusNm,
    double PredictedDefectDensityPerCm2,
    bool IsStochasticYieldLimitBreached,
    string LithographyProcessStatus,
    string DoseAndIlluminationCorrection,
    string MetrologyAdvisory);
