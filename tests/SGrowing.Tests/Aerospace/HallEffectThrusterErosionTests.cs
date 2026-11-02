using System;
using SGrowing.Api.Modules.Aerospace.HallEffectThrusterErosion;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class HallEffectThrusterErosionTests
{
    private readonly HallEffectThrusterErosionEngine _engine = new();

    [Fact]
    public void AnalyzeThruster_NominalParameters_ReturnsLongLifeCruise()
    {
        var telemetry = new HallEffectThrusterTelemetry
        {
            ThrusterUnitId = "HET-5KW-01",
            PropellantType = "Xenon",
            DischargeVoltageVolts = 300.0,
            DischargeCurrentAmps = 4.8,
            AnodeMassFlowRateMgPerSec = 5.2,
            PeakBreathingModeOscillationAmps = 1.0, // 1.0 / 4.8 = 20.8% <= 35%
            WallIonCurrentDensityMaPerCm2 = 1.0,
            IonImpactAngleDegrees = 60.0,
            MeanIonImpactEnergyEv = 70.0,
            CeramicWallDensityGPerCm3 = 2.1,
            PlumeDivergenceHalfAngleDegrees = 20.0
        };

        var result = _engine.AnalyzeThruster(telemetry);

        Assert.Equal("HET-5KW-01", result.ThrusterUnitId);
        Assert.True(result.IsWallErosionWithinLifeEnvelope);
        Assert.True(result.IsBreathingModeStable);
        Assert.True(result.IsEfficiencyTargetMet);
        Assert.True(result.ProjectedWallLifeHours >= 10000.0);
        Assert.True(result.SpecificImpulseSeconds >= 1700.0);
        Assert.Equal("NOMINAL_HALL_THRUSTER_LONG_LIFE_CRUISE", result.ThrusterOperationRegime);
    }

    [Fact]
    public void AnalyzeThruster_ExcessiveWallErosion_TriggersSputteringAlert()
    {
        var telemetry = new HallEffectThrusterTelemetry
        {
            ThrusterUnitId = "HET-ERODE-02",
            PropellantType = "Xenon",
            DischargeVoltageVolts = 400.0,
            DischargeCurrentAmps = 20.0,
            AnodeMassFlowRateMgPerSec = 6.5,
            PeakBreathingModeOscillationAmps = 4.0,
            WallIonCurrentDensityMaPerCm2 = 4.5, // High ion bombardment on wall
            IonImpactAngleDegrees = 75.0, // High incidence angle
            MeanIonImpactEnergyEv = 150.0, // High impact energy
            CeramicWallDensityGPerCm3 = 2.1,
            PlumeDivergenceHalfAngleDegrees = 25.0
        };

        var result = _engine.AnalyzeThruster(telemetry);

        Assert.False(result.IsWallErosionWithinLifeEnvelope);
        Assert.Equal("ANOMALOUS_WALL_SPUTTERING_EROSION_EXCESS", result.ThrusterOperationRegime);
        Assert.Contains("violates max qualification envelope", result.OperationalGuidance);
    }

    [Fact]
    public void AnalyzeThruster_HighBreathingMode_TriggersDischargeInstability()
    {
        var telemetry = new HallEffectThrusterTelemetry
        {
            ThrusterUnitId = "HET-OSCILLATE-03",
            PropellantType = "Xenon",
            DischargeVoltageVolts = 300.0,
            DischargeCurrentAmps = 10.0,
            AnodeMassFlowRateMgPerSec = 4.0,
            PeakBreathingModeOscillationAmps = 5.0, // 5.0 / 10.0 = 50% > 35%
            WallIonCurrentDensityMaPerCm2 = 0.8,
            IonImpactAngleDegrees = 50.0,
            MeanIonImpactEnergyEv = 65.0,
            CeramicWallDensityGPerCm3 = 2.1,
            PlumeDivergenceHalfAngleDegrees = 20.0
        };

        var result = _engine.AnalyzeThruster(telemetry);

        Assert.False(result.IsBreathingModeStable);
        Assert.Equal("BREATHING_MODE_DISCHARGE_INSTABILITY", result.ThrusterOperationRegime);
        Assert.Contains("Breathing mode current oscillation", result.OperationalGuidance);
    }

    [Fact]
    public void AnalyzeThruster_KryptonPropellant_CalculatesAccurateExhaust()
    {
        var telemetry = new HallEffectThrusterTelemetry
        {
            ThrusterUnitId = "HET-KR-04",
            PropellantType = "Krypton",
            DischargeVoltageVolts = 350.0,
            DischargeCurrentAmps = 12.0,
            AnodeMassFlowRateMgPerSec = 3.5,
            PeakBreathingModeOscillationAmps = 2.0,
            WallIonCurrentDensityMaPerCm2 = 0.9,
            IonImpactAngleDegrees = 55.0,
            MeanIonImpactEnergyEv = 70.0,
            CeramicWallDensityGPerCm3 = 2.1,
            PlumeDivergenceHalfAngleDegrees = 22.0
        };

        var result = _engine.AnalyzeThruster(telemetry);

        Assert.True(result.SpecificImpulseSeconds > 2000.0); // Krypton has higher Isp due to lighter molar mass
        Assert.True(result.ThrustMilliNewtons > 0.0);
    }

    [Fact]
    public void AnalyzeThruster_ZeroOrNegativeInputs_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeThruster(null!));

        var invalidTelemetry = new HallEffectThrusterTelemetry
        {
            ThrusterUnitId = "HET-INV",
            DischargeVoltageVolts = 0.0,
            DischargeCurrentAmps = 10.0,
            AnodeMassFlowRateMgPerSec = 4.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeThruster(invalidTelemetry));
    }
}
