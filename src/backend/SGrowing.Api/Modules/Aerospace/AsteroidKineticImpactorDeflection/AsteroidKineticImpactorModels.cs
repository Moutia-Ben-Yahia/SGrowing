namespace SGrowing.Api.Modules.Aerospace.AsteroidKineticImpactorDeflection;

public sealed record AsteroidKineticImpactorTelemetry(
    string AsteroidTargetId,
    double AsteroidMassKilograms, // M_ast: e.g. 4.8e9 kg
    double SpacecraftMassKilograms, // m_sc: e.g. 580.0 kg
    double ImpactVelocityKilometersPerSec, // v_imp: e.g. 6.1 km/s
    double ImpactIncidenceAngleDegrees, // theta: e.g. 15.0 deg
    double EstimatedMomentumBetaFactor, // beta: e.g. 2.45
    double WarningTimeLeadYears // T_lead: e.g. 12.0 yrs
);

public sealed record AsteroidKineticImpactorAnalysisResult(
    string AsteroidTargetId,
    double DeltaVMillimetersPerSecond,
    double DeflectionDistanceKilometers,
    double EarthRadiiDeflected,
    string PlanetaryDefenseRegime,
    string MissionOperationalGuidance,
    bool IsDeflectionMissionSuccessful
);

public interface IAsteroidKineticImpactorDeflectionEngine
{
    AsteroidKineticImpactorAnalysisResult AnalyzeKineticImpactor(AsteroidKineticImpactorTelemetry telemetry);
}
