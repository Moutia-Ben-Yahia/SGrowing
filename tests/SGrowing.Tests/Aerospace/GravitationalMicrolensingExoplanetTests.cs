namespace SGrowing.Tests.Aerospace;

using SGrowing.Api.Modules.Aerospace.GravitationalMicrolensingExoplanet;
using Xunit;

public class GravitationalMicrolensingExoplanetTests
{
    private readonly GravitationalMicrolensingEngine _engine = new();

    [Fact]
    public void AnalyzeEvent_NominalPaczynskiProfile_ReportsNominalSurveyCadence()
    {
        // Near peak at t = t0, u = u0 = 0.1
        // A0(0.1) = (0.01 + 2) / (0.1 * sqrt(0.01 + 4)) = 2.01 / (0.1 * 2.002498) = 10.037
        var telemetry = new MicrolensingEventTelemetry(
            EventId: "OGLE-2026-BLG-0042",
            CurrentTimeDays: 100.0,
            TimeOfClosestApproachDays: 100.0,
            ImpactParameterU0: 0.1,
            EinsteinCrossingTimeDays: 30.0,
            ObservedMagnification: 10.05,
            PhotometricErrorMag: 0.01,
            SourceAngularRadiusRho: 0.002,
            EstimatedMassRatioQ: 0.001,
            ProjectedSeparationS: 1.0);

        var result = _engine.AnalyzeEvent(telemetry);

        Assert.Equal("OGLE-2026-BLG-0042", result.EventId);
        Assert.False(result.IsCausticCrossingDetected);
        Assert.Equal("NOMINAL_POINT_LENS", result.RecommendedAlertLevel);
        Assert.Contains("Resonant", result.CausticTopology);
        Assert.Contains("Standard 1-Hour", result.FollowUpCadenceRecommendation);
    }

    [Fact]
    public void AnalyzeEvent_ViolentCausticCrossing_TriggersCriticalSubminuteAlert()
    {
        // Expected A0 ~ 10.037, but observed magnification is 28.5 (spike due to caustic fold)
        var telemetry = new MicrolensingEventTelemetry(
            EventId: "KMT-2026-BLG-1289",
            CurrentTimeDays: 100.0,
            TimeOfClosestApproachDays: 100.0,
            ImpactParameterU0: 0.1,
            EinsteinCrossingTimeDays: 25.0,
            ObservedMagnification: 28.5,
            PhotometricErrorMag: 0.008,
            SourceAngularRadiusRho: 0.0015,
            EstimatedMassRatioQ: 0.0005,
            ProjectedSeparationS: 1.05);

        var result = _engine.AnalyzeEvent(telemetry);

        Assert.True(result.IsCausticCrossingDetected);
        Assert.Equal("CRITICAL_CAUSTIC_INGRESS", result.RecommendedAlertLevel);
        Assert.Contains("Ultra-Fast", result.FollowUpCadenceRecommendation);
        Assert.True(result.EstimatedCausticCrossingDurationMinutes > 0);
        Assert.Contains("Immediate planetary caustic crossing detected", result.ActionableSummary);
    }

    [Fact]
    public void AnalyzeEvent_CloseBinaryTopology_CorrectlyClassifiesGeometry()
    {
        var telemetry = new MicrolensingEventTelemetry(
            EventId: "MOA-2026-BLG-0501",
            CurrentTimeDays: 102.0,
            TimeOfClosestApproachDays: 100.0,
            ImpactParameterU0: 0.2,
            EinsteinCrossingTimeDays: 20.0,
            ObservedMagnification: 7.2,
            PhotometricErrorMag: 0.015,
            SourceAngularRadiusRho: 0.003,
            EstimatedMassRatioQ: 0.003,
            ProjectedSeparationS: 0.65); // s < 0.9 -> Close Binary

        var result = _engine.AnalyzeEvent(telemetry);

        Assert.Contains("Close Binary", result.CausticTopology);
        Assert.True(result.IsCausticCrossingDetected);
    }

    [Fact]
    public void AnalyzeEvent_InvalidTelemetryParameters_ThrowsArgumentOutOfRangeException()
    {
        var invalidU0 = new MicrolensingEventTelemetry(
            EventId: "ERR-001",
            CurrentTimeDays: 10.0,
            TimeOfClosestApproachDays: 10.0,
            ImpactParameterU0: -0.05,
            EinsteinCrossingTimeDays: 20.0,
            ObservedMagnification: 5.0,
            PhotometricErrorMag: 0.01,
            SourceAngularRadiusRho: 0.001,
            EstimatedMassRatioQ: 0.001,
            ProjectedSeparationS: 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeEvent(invalidU0));
    }
}
