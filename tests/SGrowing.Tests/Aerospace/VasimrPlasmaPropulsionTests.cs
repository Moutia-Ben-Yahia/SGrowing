namespace SGrowing.Tests.Aerospace;

using SGrowing.Api.Modules.Aerospace.VasimrPlasmaPropulsion;
using Xunit;

public class VasimrPlasmaPropulsionTests
{
    private readonly VasimrPlasmaPropulsionEngine _engine = new();

    [Fact]
    public void AnalyzePropulsion_NominalHighAltitudeCruise_ReportsHighIspMaintenance()
    {
        // 280 km altitude, very low density 1e-11 kg/m3 -> Drag ~ 0.002 N
        // RF Power = 100 kW, Isp = 15,000 s -> Thrust ~ 0.88 N >> Drag
        var telemetry = new VasimrPlasmaTelemetry(
            SatelliteId: "VLEO-SAT-OPT-01",
            OrbitalAltitudeKm: 280.0,
            OrbitalVelocityMPerSec: 7800.0,
            AtmosphericDensityKgPerM3: 1e-11,
            SatelliteRamAreaM2: 3.0,
            DragCoefficient: 2.2,
            AvailableRfPowerWatts: 100000.0,
            CurrentIspSeconds: 15000.0,
            PropellantMassRemainingKg: 120.0);

        var result = _engine.AnalyzePropulsion(telemetry);

        Assert.Equal("VLEO-SAT-OPT-01", result.SatelliteId);
        Assert.Equal("HIGH_ISP_ORBITAL_MAINTENANCE", result.OptimalThrustMode);
        Assert.True(result.IsAltitudeLossPrevented);
        Assert.True(result.NetThrustMarginNewtons > 0.8);
        Assert.Contains("NOMINAL VLEO CRUISE", result.ThrusterGuidanceAdvisory);
    }

    [Fact]
    public void AnalyzePropulsion_ModerateAtmosphericDrag_SwitchesToBalancedDragMakeup()
    {
        // Drag ~ 0.60 N
        // RF Power = 200 kW, Isp = 10,000 s -> Thrust ~ 2.65 N > Drag
        var telemetry = new VasimrPlasmaTelemetry(
            SatelliteId: "VLEO-IMAGING-02",
            OrbitalAltitudeKm: 200.0,
            OrbitalVelocityMPerSec: 7850.0,
            AtmosphericDensityKgPerM3: 2.5e-9,
            SatelliteRamAreaM2: 2.5,
            DragCoefficient: 2.2,
            AvailableRfPowerWatts: 200000.0,
            CurrentIspSeconds: 10000.0,
            PropellantMassRemainingKg: 95.0);

        var result = _engine.AnalyzePropulsion(telemetry);

        Assert.Equal("BALANCED_DRAG_MAKEUP", result.OptimalThrustMode);
        Assert.True(result.IsAltitudeLossPrevented);
        Assert.True(result.AerodynamicDragForceNewtons >= 0.40);
        Assert.Contains("ACTIVE DRAG MAKEUP", result.ThrusterGuidanceAdvisory);
    }

    [Fact]
    public void AnalyzePropulsion_GeomagneticStormDragSurge_TriggersMaxThrustOverride()
    {
        // Severe solar storm surge: Drag force > 2.5 N
        var telemetry = new VasimrPlasmaTelemetry(
            SatelliteId: "VLEO-CRITICAL-DECAY",
            OrbitalAltitudeKm: 180.0,
            OrbitalVelocityMPerSec: 7900.0,
            AtmosphericDensityKgPerM3: 1.2e-8,
            SatelliteRamAreaM2: 4.5,
            DragCoefficient: 2.2,
            AvailableRfPowerWatts: 200000.0,
            CurrentIspSeconds: 12000.0,
            PropellantMassRemainingKg: 50.0);

        var result = _engine.AnalyzePropulsion(telemetry);

        Assert.Equal("MAX_THRUST_MAGNETIC_STORM_OVERRIDE", result.OptimalThrustMode);
        Assert.True(result.AerodynamicDragForceNewtons >= 2.0);
        Assert.Contains("SEVERE DRAG SURGE", result.ThrusterGuidanceAdvisory);
    }

    [Fact]
    public void AnalyzePropulsion_NegativeRfPower_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new VasimrPlasmaTelemetry(
            SatelliteId: "ERR-001",
            OrbitalAltitudeKm: 250.0,
            OrbitalVelocityMPerSec: 7800.0,
            AtmosphericDensityKgPerM3: 1e-10,
            SatelliteRamAreaM2: 2.0,
            DragCoefficient: 2.2,
            AvailableRfPowerWatts: -50000.0,
            CurrentIspSeconds: 10000.0,
            PropellantMassRemainingKg: 50.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzePropulsion(invalid));
    }
}
