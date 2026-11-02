using System;
using SGrowing.Api.Modules.Energy.Sco2BraytonCompressorSurge;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class Sco2BraytonCompressorSurgeTests
{
    private readonly Sco2BraytonCompressorSurgeEngine _engine = new();

    [Fact]
    public void AnalyzeCompressorSurge_NominalOperatingState_ReturnsStableTrajectory()
    {
        var telemetry = new Sco2CompressorTelemetry(
            CycleId: "DESERTEC-SCO2-TOWER-01",
            OperatingMassFlowRateKgPerSec: 46.0,
            SurgeMassFlowRateKgPerSec: 35.0,
            InletPressureMpa: 7.8,
            InletTemperatureCelsius: 32.5,
            RotorTipSpeedMPerSec: 220.0,
            NearCriticalAcousticVelocityMPerSec: 280.0,
            PlenumVolumeM3: 1.2,
            InletDuctAreaM2: 0.08,
            EquivalentDuctLengthMeters: 3.0,
            CurrentBypassValveOpeningPercent: 5.0);

        var result = _engine.AnalyzeCompressorSurge(telemetry);

        Assert.Equal("NOMINAL_STABLE", result.CompressorOperatingRegime);
        Assert.Equal("MAINTAIN_OPTIMAL_COMPRESSOR_MAP_TRAJECTORY", result.RecommendedControlAction);
        Assert.False(result.IsSurgeImminent);
        Assert.True(result.CompressorSurgeMarginPercent >= 15.0);
        Assert.Equal(0.0, result.RequiredBypassFlowKgPerSec);
    }

    [Fact]
    public void AnalyzeCompressorSurge_SurgeWarningCondition_RecommendsValveTrim()
    {
        // Operating 38 kg/s vs 35 kg/s surge -> margin = (38 - 35)/35 = 8.57%
        // B = (180 / (2 * 300)) * sqrt(0.5 / (0.1 * 3.0)) = 0.3 * sqrt(1.666) = 0.387 < 0.70
        var telemetry = new Sco2CompressorTelemetry(
            CycleId: "DESERTEC-SCO2-TOWER-02",
            OperatingMassFlowRateKgPerSec: 38.0,
            SurgeMassFlowRateKgPerSec: 35.0,
            InletPressureMpa: 7.5,
            InletTemperatureCelsius: 31.8,
            RotorTipSpeedMPerSec: 180.0,
            NearCriticalAcousticVelocityMPerSec: 300.0,
            PlenumVolumeM3: 0.5,
            InletDuctAreaM2: 0.1,
            EquivalentDuctLengthMeters: 3.0,
            CurrentBypassValveOpeningPercent: 0.0);

        var result = _engine.AnalyzeCompressorSurge(telemetry);

        Assert.Equal("SURGE_WARNING", result.CompressorOperatingRegime);
        Assert.Equal("MODULATE_ANTI_SURGE_VALVE_TRIM", result.RecommendedControlAction);
        Assert.False(result.IsSurgeImminent);
        Assert.True(result.CompressorSurgeMarginPercent < 15.0 && result.CompressorSurgeMarginPercent >= 5.0);
        Assert.True(result.RequiredBypassFlowKgPerSec > 0.0);
        Assert.True(result.RecommendedAntiSurgeValveOpeningPercent > 0.0);
    }

    [Fact]
    public void AnalyzeCompressorSurge_CriticalSurgeCondition_TriggersRapidValveOpening()
    {
        // Operating 35.5 kg/s vs 35.0 kg/s surge -> margin = (35.5 - 35)/35 = 1.43% < 5.0%
        var telemetry = new Sco2CompressorTelemetry(
            CycleId: "DESERTEC-SCO2-TOWER-03",
            OperatingMassFlowRateKgPerSec: 35.5,
            SurgeMassFlowRateKgPerSec: 35.0,
            InletPressureMpa: 7.4,
            InletTemperatureCelsius: 31.2,
            RotorTipSpeedMPerSec: 250.0,
            NearCriticalAcousticVelocityMPerSec: 250.0,
            PlenumVolumeM3: 1.5,
            InletDuctAreaM2: 0.08,
            EquivalentDuctLengthMeters: 2.5,
            CurrentBypassValveOpeningPercent: 10.0);

        var result = _engine.AnalyzeCompressorSurge(telemetry);

        Assert.Equal("SURGE_MARGIN_CRITICAL", result.CompressorOperatingRegime);
        Assert.Equal("RAPID_OPEN_ANTI_SURGE_BYPASS_VALVE_50_PERCENT", result.RecommendedControlAction);
        Assert.True(result.IsSurgeImminent);
        Assert.True(result.RecommendedAntiSurgeValveOpeningPercent >= 50.0);
    }

    [Fact]
    public void AnalyzeCompressorSurge_HighBParameterWithModerateMargin_ElevatesToCritical()
    {
        // Operating 37.5 kg/s vs 35.0 kg/s surge -> margin = 7.14% (< 10%)
        // RotorTipSpeed = 350 m/s, AcousticVel = 200 m/s -> factor = 0.875
        // Plenum = 5.0 m3, DuctArea = 0.05 m2, DuctLength = 2.0 m -> ductVol = 0.1 -> sqrt(50) = 7.07
        // B = 0.875 * 7.07 = 6.18 >= 0.70
        var telemetry = new Sco2CompressorTelemetry(
            CycleId: "DESERTEC-SCO2-TOWER-04",
            OperatingMassFlowRateKgPerSec: 37.5,
            SurgeMassFlowRateKgPerSec: 35.0,
            InletPressureMpa: 7.39,
            InletTemperatureCelsius: 31.05,
            RotorTipSpeedMPerSec: 350.0,
            NearCriticalAcousticVelocityMPerSec: 200.0,
            PlenumVolumeM3: 5.0,
            InletDuctAreaM2: 0.05,
            EquivalentDuctLengthMeters: 2.0,
            CurrentBypassValveOpeningPercent: 12.0);

        var result = _engine.AnalyzeCompressorSurge(telemetry);

        Assert.Equal("SURGE_MARGIN_CRITICAL", result.CompressorOperatingRegime);
        Assert.True(result.GreitzerBParameter >= 0.70);
        Assert.True(result.IsSurgeImminent);
    }
}
