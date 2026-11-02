namespace SGrowing.Tests.Energy;

using SGrowing.Api.Modules.Energy.HtsRebcoCableDelamination;
using Xunit;

public class HtsRebcoCableDelaminationTests
{
    private readonly HtsRebcoCableDelaminationEngine _engine = new();

    [Fact]
    public void AnalyzeCable_NominalOperatingState_MaintainsHighMarginAndNominalState()
    {
        // 10,000 A across 40 tapes = 250 A/tape
        // B = 10 T, theta = 90 deg, w = 4 mm
        // F_L = 250 * 10 * 1 = 2500 N/m
        // Stress = 2500 / (4 * 1000) = 0.625 MPa
        // Critical = 30 MPa -> Margin = (30 - 0.625) / 30 = 97.9%
        var telemetry = new HtsRebcoCableTelemetry(
            CableId: "HTS-CABLE-ALPHA-1",
            OperatingCurrentAmps: 10000.0,
            PeakMagneticFieldTesla: 10.0,
            MagneticFieldAngleDegrees: 90.0,
            TapeWidthMm: 4.0,
            NumberOfTapesInConductor: 40,
            CriticalInterfacialYieldStrengthMpa: 30.0,
            MeasuredTapVoltageMilliVolts: 2.5,
            VoltageGrowthDurationMs: 0.0);

        var result = _engine.AnalyzeCable(telemetry);

        Assert.Equal("HTS-CABLE-ALPHA-1", result.CableId);
        Assert.Equal("NOMINAL_SUPERCONDUCTING", result.OperatingState);
        Assert.Equal("CONTINUE_SUPERCONDUCTING_OPERATION", result.RecommendedProtectionAction);
        Assert.False(result.IsDelaminationRiskDetected);
        Assert.False(result.IsQuenchRunawayDetected);
        Assert.True(result.DelaminationSafetyMarginPercent > 90.0);
    }

    [Fact]
    public void AnalyzeCable_HighFieldNearLimit_TriggersDelaminationWarning()
    {
        // 40,000 A across 20 tapes = 2000 A/tape
        // B = 22 T, theta = 90 deg, w = 4 mm
        // F_L = 2000 * 22 = 44000 N/m
        // Stress = 44000 / 4000 = 11.0 MPa
        // Critical = 13.0 MPa -> Margin = (13 - 11) / 13 = 15.38% (< 25%, > 10%)
        var telemetry = new HtsRebcoCableTelemetry(
            CableId: "HTS-CABLE-TOKAMAK-TF",
            OperatingCurrentAmps: 40000.0,
            PeakMagneticFieldTesla: 22.0,
            MagneticFieldAngleDegrees: 90.0,
            TapeWidthMm: 4.0,
            NumberOfTapesInConductor: 20,
            CriticalInterfacialYieldStrengthMpa: 13.0,
            MeasuredTapVoltageMilliVolts: 15.0,
            VoltageGrowthDurationMs: 5.0);

        var result = _engine.AnalyzeCable(telemetry);

        Assert.True(result.IsDelaminationRiskDetected);
        Assert.Equal("DELAMINATION_WARNING", result.OperatingState);
        Assert.Equal("CURRENT_THROTTLING_RECOMMENDED", result.RecommendedProtectionAction);
        Assert.Contains("Moderate mechanical stress", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeCable_TapVoltageQuenchExceeded_TriggersFastDumpResistor()
    {
        var telemetry = new HtsRebcoCableTelemetry(
            CableId: "HTS-CABLE-QUENCH-HOTSPOT",
            OperatingCurrentAmps: 15000.0,
            PeakMagneticFieldTesla: 18.0,
            MagneticFieldAngleDegrees: 90.0,
            TapeWidthMm: 4.0,
            NumberOfTapesInConductor: 40,
            CriticalInterfacialYieldStrengthMpa: 30.0,
            MeasuredTapVoltageMilliVolts: 185.0, // > 100 mV
            VoltageGrowthDurationMs: 25.0); // > 15 ms

        var result = _engine.AnalyzeCable(telemetry);

        Assert.True(result.IsQuenchRunawayDetected);
        Assert.Equal("CRITICAL_QUENCH_TRIP", result.OperatingState);
        Assert.Equal("FIRE_FAST_EXTRACTION_DUMP_RESISTOR", result.RecommendedProtectionAction);
        Assert.Contains("Quench runaway detected", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeCable_ZeroOrNegativeCurrent_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new HtsRebcoCableTelemetry(
            CableId: "HTS-ERR",
            OperatingCurrentAmps: -100.0,
            PeakMagneticFieldTesla: 10.0,
            MagneticFieldAngleDegrees: 90.0,
            TapeWidthMm: 4.0,
            NumberOfTapesInConductor: 40,
            CriticalInterfacialYieldStrengthMpa: 30.0,
            MeasuredTapVoltageMilliVolts: 0.0,
            VoltageGrowthDurationMs: 0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeCable(invalid));
    }
}
