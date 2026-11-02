namespace SGrowing.Tests.Telecom;

using FluentAssertions;
using SGrowing.Api.Modules.Telecom.SubseaAcousticEarlyWarning;
using Xunit;

public class SubseaAcousticEarlyWarningTests
{
    private readonly SubseaAcousticEarlyWarningEngine _engine = new();

    [Fact]
    public void AnalyzeReadings_SeismicWavefrontWithLowFrequencyStrain_ClassifiesSeismicRuptureAndTriggersReroute()
    {
        var segments = new List<SubseaCableSegment>
        {
            new("SEG-MED-01", "SeaMeWe-5", 0.0, 150.0, WaterDepthMeters: 2200.0, "Hellenic Trench", 0.18)
        };

        var readings = new List<DistributedAcousticReading>
        {
            new("SEG-MED-01", PositionKm: 85.4, StrainMicrostrains: 280.0, FrequencyHz: 4.5, DateTimeOffset.UtcNow, AcousticEnergyJoules: 120.0)
        };

        var seismic = new List<SeismicWavefront>
        {
            new("QUAKE-MED-01", 35.2, 24.1, MagnitudeRichter: 6.8, DepthKm: 18.0, DateTimeOffset.UtcNow.AddSeconds(-30))
        };

        var request = new SubseaAcousticAnalysisRequest(segments, readings, seismic);
        var report = _engine.AnalyzeReadings(request);

        report.TotalSegmentsMonitored.Should().Be(1);
        report.ActiveThreats.Should().HaveCount(1);
        report.OpticalRerouteRequired.Should().BeTrue();
        report.PeakStrainObserved.Should().Be(280.0);

        var threat = report.ActiveThreats[0];
        threat.ThreatCategory.Should().Be("SeismicFaultRupture");
        threat.RecommendedMitigation.Should().Contain("terrestrial optical mesh bypass");
    }

    [Fact]
    public void AnalyzeReadings_ShallowWaterStrainSpike_DetectsAnchorDragImpact()
    {
        var segments = new List<SubseaCableSegment>
        {
            new("SEG-ENG-02", "Cross-Channel Link", 0.0, 45.0, WaterDepthMeters: 65.0, "Dover Strait", 0.20)
        };

        var readings = new List<DistributedAcousticReading>
        {
            new("SEG-ENG-02", PositionKm: 18.2, StrainMicrostrains: 220.0, FrequencyHz: 80.0, DateTimeOffset.UtcNow, AcousticEnergyJoules: 15.0)
        };

        var request = new SubseaAcousticAnalysisRequest(segments, readings);
        var report = _engine.AnalyzeReadings(request);

        report.ActiveThreats.Should().HaveCount(1);
        var threat = report.ActiveThreats[0];
        threat.ThreatCategory.Should().Be("AnchorDragImpact");
        threat.RecommendedMitigation.Should().Contain("AIS hazard alert");
    }

    [Fact]
    public void AnalyzeReadings_NominalStrain_ReturnsZeroAlertsAndHealthyStatus()
    {
        var segments = new List<SubseaCableSegment>
        {
            new("SEG-ATL-03", "Transatlantic TAT-14", 0.0, 500.0, WaterDepthMeters: 3800.0, "Mid-Atlantic Ridge", 0.16)
        };

        var readings = new List<DistributedAcousticReading>
        {
            new("SEG-ATL-03", PositionKm: 210.0, StrainMicrostrains: 45.0, FrequencyHz: 12.0, DateTimeOffset.UtcNow, AcousticEnergyJoules: 1.2)
        };

        var request = new SubseaAcousticAnalysisRequest(segments, readings, AlertStrainThresholdMicrostrains: 150.0);
        var report = _engine.AnalyzeReadings(request);

        report.ActiveThreats.Should().BeEmpty();
        report.OpticalRerouteRequired.Should().BeFalse();
        report.NetworkHealthStatus.Should().Contain("Nominal");
    }

    [Fact]
    public void AnalyzeReadings_DeepWaterTurbidityCurrent_DetectsSubseaLandslide()
    {
        var segments = new List<SubseaCableSegment>
        {
            new("SEG-PAC-04", "Jupiter Cable", 0.0, 800.0, WaterDepthMeters: 4500.0, "Japan Trench Slope", 0.17)
        };

        var readings = new List<DistributedAcousticReading>
        {
            new("SEG-PAC-04", PositionKm: 340.5, StrainMicrostrains: 190.0, FrequencyHz: 25.0, DateTimeOffset.UtcNow, AcousticEnergyJoules: 75.0)
        };

        var request = new SubseaAcousticAnalysisRequest(segments, readings);
        var report = _engine.AnalyzeReadings(request);

        report.ActiveThreats.Should().HaveCount(1);
        report.ActiveThreats[0].ThreatCategory.Should().Be("SubseaLandslide");
        report.ActiveThreats[0].RecommendedMitigation.Should().Contain("automatic optical protection switching");
    }
}
