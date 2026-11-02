namespace SGrowing.Tests.Infrastructure;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.SeismicMetamaterialPhononicCloak;
using Xunit;

public class SeismicMetamaterialPhononicCloakTests
{
    private readonly SeismicMetamaterialPhononicCloakEngine _engine = new();

    [Fact]
    public void AnalyzeSeismicCloaking_NominalInBandWave_ReturnsEffectivePhononicCloaking()
    {
        var telemetry = new SeismicCloakTelemetry(
            SiteId: "SITE-METACLOAK-01",
            EarthquakeMomentMagnitudeMw: 6.4,
            PredominantSeismicFrequencyHz: 3.5,
            IncidentPeakGroundAccelerationG: 0.45,
            SoilRayleighWaveVelocityMetersPerSec: 240.0,
            PhononicLatticeConstantMeters: 3.0,
            ResonatorInclusionDiameterMeters: 1.8,
            ResonatorBandgapLowerBoundHz: 1.5,
            ResonatorBandgapUpperBoundHz: 6.5,
            SoilLiquefactionRiskIndex: 0.12
        );

        var result = _engine.AnalyzeSeismicCloaking(telemetry);

        result.SiteId.Should().Be("SITE-METACLOAK-01");
        result.CloakingRegime.Should().Be("PHONONIC_BANDGAP_SEISMIC_WAVE_CLOAKING");
        result.IsCloakingEffective.Should().BeTrue();
        result.BraggScatteringCenterFrequencyHz.Should().BeApproximately(40.0, 0.5);
        result.GroundWaveAttenuationDecibels.Should().BeGreaterThan(14.0);
        result.ProtectedStructurePeakGroundAccelerationG.Should().BeLessThan(0.12);
        result.WaveEnergyDeflectionPercent.Should().BeGreaterThan(90.0);
        result.GeotechnicalActionGuidance.Should().ContainEquivalentOf("diverted around foundation perimeter");
    }

    [Fact]
    public void AnalyzeSeismicCloaking_SuperMagnitudeEarthquake_ReturnsOvertoppingHazard()
    {
        var telemetry = new SeismicCloakTelemetry(
            SiteId: "SITE-METACLOAK-02",
            EarthquakeMomentMagnitudeMw: 8.4, // Super magnitude Mw > 7.5
            PredominantSeismicFrequencyHz: 4.0,
            IncidentPeakGroundAccelerationG: 1.6, // Extreme acceleration
            SoilRayleighWaveVelocityMetersPerSec: 240.0,
            PhononicLatticeConstantMeters: 3.0,
            ResonatorInclusionDiameterMeters: 1.8,
            ResonatorBandgapLowerBoundHz: 1.5,
            ResonatorBandgapUpperBoundHz: 6.5,
            SoilLiquefactionRiskIndex: 0.20
        );

        var result = _engine.AnalyzeSeismicCloaking(telemetry);

        result.CloakingRegime.Should().Be("SUPER_MAGNITUDE_EARTHQUAKE_OVERTOPPING");
        result.IsCloakingEffective.Should().BeFalse();
        result.ProtectedStructurePeakGroundAccelerationG.Should().BeGreaterThan(0.20);
        result.GeotechnicalActionGuidance.Should().ContainEquivalentOf("base isolation dampers");
    }

    [Fact]
    public void AnalyzeSeismicCloaking_OutOfBandgapFrequency_ReturnsPenetrationRegime()
    {
        var telemetry = new SeismicCloakTelemetry(
            SiteId: "SITE-METACLOAK-03",
            EarthquakeMomentMagnitudeMw: 6.0,
            PredominantSeismicFrequencyHz: 0.75, // Below bandgap lower bound (1.5 Hz)
            IncidentPeakGroundAccelerationG: 0.35,
            SoilRayleighWaveVelocityMetersPerSec: 240.0,
            PhononicLatticeConstantMeters: 3.0,
            ResonatorInclusionDiameterMeters: 1.8,
            ResonatorBandgapLowerBoundHz: 1.5,
            ResonatorBandgapUpperBoundHz: 6.5,
            SoilLiquefactionRiskIndex: 0.15
        );

        var result = _engine.AnalyzeSeismicCloaking(telemetry);

        result.CloakingRegime.Should().Be("OUT_OF_BANDGAP_SEISMIC_PENETRATION");
        result.IsCloakingEffective.Should().BeFalse();
        result.GeotechnicalActionGuidance.Should().ContainEquivalentOf("piezo-tunable resonant mass");
    }

    [Fact]
    public void AnalyzeSeismicCloaking_SoilLiquefactionSurge_ReturnsMatrixDecoupling()
    {
        var telemetry = new SeismicCloakTelemetry(
            SiteId: "SITE-METACLOAK-04",
            EarthquakeMomentMagnitudeMw: 6.8,
            PredominantSeismicFrequencyHz: 3.5,
            IncidentPeakGroundAccelerationG: 0.40,
            SoilRayleighWaveVelocityMetersPerSec: 240.0,
            PhononicLatticeConstantMeters: 3.0,
            ResonatorInclusionDiameterMeters: 1.8,
            ResonatorBandgapLowerBoundHz: 1.5,
            ResonatorBandgapUpperBoundHz: 6.5,
            SoilLiquefactionRiskIndex: 0.88 // Liquefaction surge > 0.70
        );

        var result = _engine.AnalyzeSeismicCloaking(telemetry);

        result.CloakingRegime.Should().Be("SOIL_LIQUEFACTION_MATRIX_DECOUPLING");
        result.IsCloakingEffective.Should().BeFalse();
        result.GeotechnicalActionGuidance.Should().ContainEquivalentOf("subterranean dewatering");
    }

    [Fact]
    public void AnalyzeSeismicCloaking_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeSeismicCloaking(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
