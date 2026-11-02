namespace SGrowing.Api.Modules.Energy.LiquidLithiumCpsDivertor;

public sealed record LiquidLithiumDivertorTelemetry(
    string DivertorCassetteId,
    double PeakIncidentPlasmaHeatFluxMwPerM2,
    double LithiumSurfaceTemperatureCelsius,
    double MeshPoreRadiusMicrons,
    double CapillaryReplenishmentPressureKPa,
    double TransverseMagneticFieldTesla,
    double CorePlasmaEffectiveChargeZeff,
    bool ThermoelectricMhdReplenishmentPumpActive
);

public sealed record LiquidLithiumDivertorAnalysisResult(
    string DivertorCassetteId,
    double NetTargetHeatFluxMwPerM2,
    double VaporShieldingAttenuationRatio,
    double CapillaryToMhdHeadSafetyMargin,
    string DivertorProtectionRegime,
    string DivertorCoolingCommand,
    bool IsDivertorTargetProtected
);

public interface ILiquidLithiumCpsDivertorEngine
{
    LiquidLithiumDivertorAnalysisResult AnalyzeLiquidMetalDivertor(LiquidLithiumDivertorTelemetry telemetry);
}
