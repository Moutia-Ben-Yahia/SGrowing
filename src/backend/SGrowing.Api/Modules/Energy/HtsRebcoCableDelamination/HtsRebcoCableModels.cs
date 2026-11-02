namespace SGrowing.Api.Modules.Energy.HtsRebcoCableDelamination;

public record HtsRebcoCableTelemetry(
    string CableId,
    double OperatingCurrentAmps,
    double PeakMagneticFieldTesla,
    double MagneticFieldAngleDegrees,
    double TapeWidthMm,
    int NumberOfTapesInConductor,
    double CriticalInterfacialYieldStrengthMpa,
    double MeasuredTapVoltageMilliVolts,
    double VoltageGrowthDurationMs);

public record HtsRebcoCableAnalysisResult(
    string CableId,
    double LorentzTransverseStressMpa,
    double DelaminationSafetyMarginPercent,
    bool IsDelaminationRiskDetected,
    bool IsQuenchRunawayDetected,
    string OperatingState,
    string RecommendedProtectionAction,
    string DiagnosticSummary);
