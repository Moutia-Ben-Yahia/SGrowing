namespace SGrowing.Tests.Energy;

using SGrowing.Api.Modules.Energy.SupercriticalWellboreJouleThomson;
using Xunit;

public class SupercriticalWellboreJouleThomsonTests
{
    private readonly SupercriticalWellboreEngine _engine = new();

    [Fact]
    public void AnalyzeWellbore_OptimalThermosiphon_ReportsSelfDrivenWithoutPumps()
    {
        // 5000m depth, delta_T = 300 - 40 = 260 C -> massive density difference
        // Thermosiphon pressure ~ 25 MPa >> friction ~ 4.5 MPa
        var telemetry = new SupercriticalWellboreTelemetry(
            WellboreId: "EGS-DEEP-COAXIAL-01",
            WellboreDepthMeters: 5000.0,
            BottomHoleTemperatureCelsius: 450.0,
            WorkingFluid: "Supercritical-CO2",
            MassFlowRateKgPerSec: 35.0,
            AnnulusInletTemperatureCelsius: 40.0,
            AnnulusInletPressureMpa: 10.0,
            TubingOutletTemperatureCelsius: 300.0,
            TubingOutletPressureMpa: 8.5,
            SurfaceChokePressureDropMpa: 1.2);

        var result = _engine.AnalyzeWellbore(telemetry);

        Assert.Equal("EGS-DEEP-COAXIAL-01", result.WellboreId);
        Assert.True(result.IsSelfDrivenThermosiphonSustained);
        Assert.Equal("SELF_DRIVEN_THERMOSIPHON_OPTIMAL", result.OperationalState);
        Assert.Equal("MAINTAIN_CHOKE_OPENING", result.RecommendedChokeActuation);
        Assert.True(result.ThermosiphonDrivingPressureMpa > result.FrictionalPressureLossMpa);
        Assert.True(result.EstimatedThermalPowerOutputMegaWatts > 10.0);
    }

    [Fact]
    public void AnalyzeWellbore_ExcessiveChokePressureDrop_FlagsJouleThomsonFreezingHazard()
    {
        // Choke drop = 4.5 MPa -> dT_JT = -1.8 * 4.5 = -8.1 C (<= -6.0 C)
        var telemetry = new SupercriticalWellboreTelemetry(
            WellboreId: "EGS-CHOKE-DROP-02",
            WellboreDepthMeters: 5000.0,
            BottomHoleTemperatureCelsius: 450.0,
            WorkingFluid: "Supercritical-CO2",
            MassFlowRateKgPerSec: 35.0,
            AnnulusInletTemperatureCelsius: 40.0,
            AnnulusInletPressureMpa: 12.0,
            TubingOutletTemperatureCelsius: 300.0,
            TubingOutletPressureMpa: 8.0,
            SurfaceChokePressureDropMpa: 4.5);

        var result = _engine.AnalyzeWellbore(telemetry);

        Assert.Equal("JOULE_THOMSON_FREEZING_HAZARD", result.OperationalState);
        Assert.Equal("RESTRICT_CHOKE_PRESSURE_DROP", result.RecommendedChokeActuation);
        Assert.True(result.JouleThomsonTemperatureShiftCelsius <= -6.0);
        Assert.Contains("JOULE-THOMSON CHATTERING", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeWellbore_ShallowWellboreInsufficientBuoyancy_FlagsParasiticPumping()
    {
        // Shallow wellbore 400m -> Thermosiphon pressure only ~2 MPa < friction ~3.5 MPa
        var telemetry = new SupercriticalWellboreTelemetry(
            WellboreId: "EGS-SHALLOW-PILOT",
            WellboreDepthMeters: 400.0,
            BottomHoleTemperatureCelsius: 120.0,
            WorkingFluid: "Supercritical-CO2",
            MassFlowRateKgPerSec: 35.0,
            AnnulusInletTemperatureCelsius: 40.0,
            AnnulusInletPressureMpa: 8.0,
            TubingOutletTemperatureCelsius: 100.0,
            TubingOutletPressureMpa: 7.5,
            SurfaceChokePressureDropMpa: 0.5);

        var result = _engine.AnalyzeWellbore(telemetry);

        Assert.False(result.IsSelfDrivenThermosiphonSustained);
        Assert.Equal("PARASITIC_PUMPING_REQUIRED", result.OperationalState);
        Assert.Equal("MODULATE_CHOKE_INCREASE_FLOW", result.RecommendedChokeActuation);
    }

    [Fact]
    public void AnalyzeWellbore_NegativeDepth_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new SupercriticalWellboreTelemetry(
            WellboreId: "ERR-001",
            WellboreDepthMeters: -100.0,
            BottomHoleTemperatureCelsius: 200.0,
            WorkingFluid: "Supercritical-CO2",
            MassFlowRateKgPerSec: 10.0,
            AnnulusInletTemperatureCelsius: 30.0,
            AnnulusInletPressureMpa: 8.0,
            TubingOutletTemperatureCelsius: 100.0,
            TubingOutletPressureMpa: 7.0,
            SurfaceChokePressureDropMpa: 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeWellbore(invalid));
    }
}
