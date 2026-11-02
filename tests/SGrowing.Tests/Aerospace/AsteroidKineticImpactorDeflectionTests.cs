using System;
using SGrowing.Api.Modules.Aerospace.AsteroidKineticImpactorDeflection;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class AsteroidKineticImpactorDeflectionTests
{
    private readonly AsteroidKineticImpactorDeflectionEngine _engine = new();

    [Fact]
    public void AnalyzeKineticImpactor_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeKineticImpactor(null!));
    }

    [Fact]
    public void AnalyzeKineticImpactor_NegativeAsteroidMass_ThrowsArgumentException()
    {
        var telemetry = new AsteroidKineticImpactorTelemetry(
            AsteroidTargetId: "NEA-2026-XQ",
            AsteroidMassKilograms: -1e9, // Negative!
            SpacecraftMassKilograms: 500.0,
            ImpactVelocityKilometersPerSec: 6.0,
            ImpactIncidenceAngleDegrees: 10.0,
            EstimatedMomentumBetaFactor: 2.2,
            WarningTimeLeadYears: 10.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeKineticImpactor(telemetry));
    }

    [Fact]
    public void AnalyzeKineticImpactor_SuccessfulDeflection_ReturnsClearanceRegime()
    {
        // Tunguska-class NEA: M_ast = 6.0e7 kg (60,000 tonnes), m_sc = 600 kg, v = 6.2 km/s, beta = 2.5, lead = 10 yrs
        // delta_v = 2.5 * (600 / 6e7) * 6200 * cos(5 deg) ~ 0.154 m/s = 154 mm/s
        // deflection = 0.154 * 3.15576e8 / 1000 ~ 48,800 km > 25,000 km
        var telemetry = new AsteroidKineticImpactorTelemetry(
            AsteroidTargetId: "ASTEROID-DIMORPHOS-B",
            AsteroidMassKilograms: 6.0e7,
            SpacecraftMassKilograms: 600.0,
            ImpactVelocityKilometersPerSec: 6.2,
            ImpactIncidenceAngleDegrees: 5.0,
            EstimatedMomentumBetaFactor: 2.5,
            WarningTimeLeadYears: 10.0
        );

        var result = _engine.AnalyzeKineticImpactor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ASTEROID-DIMORPHOS-B", result.AsteroidTargetId);
        Assert.True(result.DeltaVMillimetersPerSecond >= 1.0);
        Assert.True(result.DeflectionDistanceKilometers >= 25000.0);
        Assert.True(result.IsDeflectionMissionSuccessful);
        Assert.Equal("SUCCESSFUL_PLANETARY_DEFENSE_EARTH_CLEARANCE_REGIME", result.PlanetaryDefenseRegime);
        Assert.Contains("Asteroid completely defused", result.MissionOperationalGuidance);
    }

    [Fact]
    public void AnalyzeKineticImpactor_InsufficientDeflection_ReturnsImpactCorridorCrossing()
    {
        // Huge asteroid, tiny lead time -> deflection < 6378 km
        var telemetry = new AsteroidKineticImpactorTelemetry(
            AsteroidTargetId: "ASTEROID-COLOSSAL-99",
            AsteroidMassKilograms: 8.0e11, // 800 billion kg!
            SpacecraftMassKilograms: 400.0,
            ImpactVelocityKilometersPerSec: 5.0,
            ImpactIncidenceAngleDegrees: 20.0,
            EstimatedMomentumBetaFactor: 1.5,
            WarningTimeLeadYears: 4.0
        );

        var result = _engine.AnalyzeKineticImpactor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("INSUFFICIENT_DEFLECTION_IMPACT_CORRIDOR_CROSSING_HAZARD", result.PlanetaryDefenseRegime);
        Assert.False(result.IsDeflectionMissionSuccessful);
        Assert.Contains("fails to clear Earth capture cross-section", result.MissionOperationalGuidance);
    }

    [Fact]
    public void AnalyzeKineticImpactor_UnphysicalBeta_ReturnsAbsorptiveAnomaly()
    {
        var telemetry = new AsteroidKineticImpactorTelemetry(
            AsteroidTargetId: "NEA-ANOMALY-03",
            AsteroidMassKilograms: 5.0e9,
            SpacecraftMassKilograms: 500.0,
            ImpactVelocityKilometersPerSec: 6.0,
            ImpactIncidenceAngleDegrees: 10.0,
            EstimatedMomentumBetaFactor: 0.85, // Beta < 1.0 is unphysical!
            WarningTimeLeadYears: 10.0
        );

        var result = _engine.AnalyzeKineticImpactor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("UNPHYSICAL_OR_MASS_ABSORPTIVE_CRATERING_ANOMALY", result.PlanetaryDefenseRegime);
        Assert.False(result.IsDeflectionMissionSuccessful);
        Assert.Contains("violates conservation of momentum", result.MissionOperationalGuidance);
    }
}
