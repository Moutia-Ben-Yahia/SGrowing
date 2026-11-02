namespace SGrowing.Tests.Manufacturing;

using FluentAssertions;
using SGrowing.Api.Modules.Manufacturing.CleanroomAirflowPredictor;
using Xunit;

public class CleanroomAirflowPredictorTests
{
    private readonly CleanroomAirflowPredictorEngine _engine = new();

    [Fact]
    public void EvaluateCleanroomFacility_UltraCleanBay_TrimsFfuSpeedForEnergySavings()
    {
        var zones = new List<CleanroomZone>
        {
            new("BAY-EUV-01", "Extreme UV Lithography Bay", VolumeCubicMeters: 600.0, TargetIsoClass: 3, FfuCount: 40, NominalAirVelocityMps: 0.45)
        };

        // Very low particle count (2.0/m³ at 0.5um << 35.0 limit for ISO 3)
        var sensor = new ParticleSensorTelemetry("SENSOR-01", "BAY-EUV-01", 120.0, 2.0, 0.46, 25.0, DateTimeOffset.UtcNow);

        var request = new CleanroomAnalysisRequest(zones, [sensor]);
        var report = _engine.EvaluateCleanroomFacility(request);

        report.AllBaysCompliant.Should().BeTrue();
        var audit = report.BayAudits.Should().ContainSingle().Subject;
        audit.InSpec.Should().BeTrue();
        audit.AchievedIsoClass.Should().BeLessThanOrEqualTo(3);
        audit.FfuSpeedAdjustmentPercent.Should().Be(-10.0);
        audit.OptimizationRecommendation.Should().Contain("Eco-Optimized");
    }

    [Fact]
    public void EvaluateCleanroomFacility_ParticulateSpikeNearLimit_RampsFfuSpeed()
    {
        var zones = new List<CleanroomZone>
        {
            new("BAY-ETCH-02", "Plasma Etch Bay", 450.0, TargetIsoClass: 4, FfuCount: 25, NominalAirVelocityMps: 0.40)
        };

        // 320/m³ at 0.5um approaches 352/m³ limit for ISO 4
        var sensor = new ParticleSensorTelemetry("SENSOR-02", "BAY-ETCH-02", 8500.0, 320.0, 0.38, 15.0, DateTimeOffset.UtcNow);

        var request = new CleanroomAnalysisRequest(zones, [sensor]);
        var report = _engine.EvaluateCleanroomFacility(request);

        var audit = report.BayAudits.Should().ContainSingle().Subject;
        audit.FfuSpeedAdjustmentPercent.Should().Be(15.0);
        audit.OptimizationRecommendation.Should().Contain("Contamination Threat");
    }

    [Fact]
    public void EvaluateCleanroomFacility_OutOfSpecBay_MarksFacilityNonCompliant()
    {
        var zones = new List<CleanroomZone>
        {
            new("BAY-DIFF-03", "Diffusion Furnace Bay", 300.0, TargetIsoClass: 2, FfuCount: 30, NominalAirVelocityMps: 0.50)
        };

        // 50 particles at 0.5um breaches ISO 2 (limit is 4.0)
        var sensor = new ParticleSensorTelemetry("SENSOR-03", "BAY-DIFF-03", 2500.0, 50.0, 0.40, 10.0, DateTimeOffset.UtcNow);

        var request = new CleanroomAnalysisRequest(zones, [sensor]);
        var report = _engine.EvaluateCleanroomFacility(request);

        report.AllBaysCompliant.Should().BeFalse();
        report.BayAudits[0].InSpec.Should().BeFalse();
        report.OverallAirflowQuality.Should().Contain("Airflow Out-Of-Spec");
    }

    [Fact]
    public void EvaluateCleanroomFacility_MissingSensorTelemetry_UsesNominalZoneParameters()
    {
        var zones = new List<CleanroomZone>
        {
            new("BAY-MET-04", "Metrology Bay", 200.0, 4, 15, 0.42)
        };

        var request = new CleanroomAnalysisRequest(zones, []);
        var report = _engine.EvaluateCleanroomFacility(request);

        report.BayAudits.Should().ContainSingle();
        report.BayAudits[0].AirChangesPerHour.Should().BeGreaterThan(50.0);
    }
}
