namespace SGrowing.Api.Modules.Infrastructure.SeismicMetamaterialPhononicCloak;

public sealed record SeismicCloakTelemetry(
    string SiteId,
    double EarthquakeMomentMagnitudeMw,
    double PredominantSeismicFrequencyHz,
    double IncidentPeakGroundAccelerationG,
    double SoilRayleighWaveVelocityMetersPerSec,
    double PhononicLatticeConstantMeters,
    double ResonatorInclusionDiameterMeters,
    double ResonatorBandgapLowerBoundHz,
    double ResonatorBandgapUpperBoundHz,
    double SoilLiquefactionRiskIndex
);

public sealed record SeismicCloakAnalysisResult(
    string SiteId,
    double BraggScatteringCenterFrequencyHz,
    double ProtectedStructurePeakGroundAccelerationG,
    double GroundWaveAttenuationDecibels,
    double WaveEnergyDeflectionPercent,
    string CloakingRegime,
    string GeotechnicalActionGuidance,
    bool IsCloakingEffective
);

public interface ISeismicMetamaterialPhononicCloakEngine
{
    SeismicCloakAnalysisResult AnalyzeSeismicCloaking(SeismicCloakTelemetry telemetry);
}
