namespace SGrowing.Api.Modules.Energy.LiquidMetalBlanketHartmannFlow;

public record LiquidMetalMhdTelemetry(
    string BlanketModuleId,
    double ToroidalMagneticFieldTesla,
    double ChannelHalfWidthMeters,
    double ChannelLengthMeters,
    double LiquidMetalFlowVelocityMPerSec,
    double WallConductanceRatio,
    double TritiumPermeationReductionFactor,
    double MeasuredMhdPressureDropMpa,
    double MaxAllowableStructuralPressureDropMpa);

public record LiquidMetalMhdAnalysisResult(
    string BlanketModuleId,
    double HartmannNumber,
    double HartmannLayerThicknessMicrons,
    double PredictedMhdPressureDropMpa,
    double PressureSafetyMarginPercent,
    bool IsMhdPressureLimitExceeded,
    bool IsTritiumPermeationBarrierDegraded,
    string FlowRegimeStatus,
    string FlowControlRecommendation,
    string DiagnosticSummary);
