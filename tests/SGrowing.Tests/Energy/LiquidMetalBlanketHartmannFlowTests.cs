namespace SGrowing.Tests.Energy;

using SGrowing.Api.Modules.Energy.LiquidMetalBlanketHartmannFlow;
using Xunit;

public class LiquidMetalBlanketHartmannFlowTests
{
    private readonly LiquidMetalBlanketHartmannFlowEngine _engine = new();

    [Fact]
    public void AnalyzeBlanketChannel_NominalOperatingFlow_MaintainsHighMarginAndNominalRegime()
    {
        // B = 4.0 T, a = 0.04 m, L = 1.5 m, u0 = 0.08 m/s, cw = 0.03
        // Low velocity and modest B -> low pressure drop (~0.05 MPa)
        var telemetry = new LiquidMetalMhdTelemetry(
            BlanketModuleId: "ITER-TBM-HCLL-01",
            ToroidalMagneticFieldTesla: 4.0,
            ChannelHalfWidthMeters: 0.04,
            ChannelLengthMeters: 1.5,
            LiquidMetalFlowVelocityMPerSec: 0.08,
            WallConductanceRatio: 0.03,
            TritiumPermeationReductionFactor: 120.0,
            MeasuredMhdPressureDropMpa: 0.06,
            MaxAllowableStructuralPressureDropMpa: 1.5);

        var result = _engine.AnalyzeBlanketChannel(telemetry);

        Assert.Equal("ITER-TBM-HCLL-01", result.BlanketModuleId);
        Assert.Equal("NOMINAL_MHD_BLANKET_FLOW", result.FlowRegimeStatus);
        Assert.Equal("MAINTAIN_NOMINAL_BREEDING_FLOW", result.FlowControlRecommendation);
        Assert.False(result.IsMhdPressureLimitExceeded);
        Assert.False(result.IsTritiumPermeationBarrierDegraded);
        Assert.True(result.PressureSafetyMarginPercent > 70.0);
        Assert.True(result.HartmannNumber > 1000.0);
    }

    [Fact]
    public void AnalyzeBlanketChannel_HighMhdPressureDropNearLimit_TriggersThrottlingWarning()
    {
        // B = 7.0 T, u0 = 0.45 m/s, L = 2.5 m, cw = 0.05 -> pressure drop ~ 1.35 MPa vs 1.5 MPa limit
        // margin < 20%
        var telemetry = new LiquidMetalMhdTelemetry(
            BlanketModuleId: "DEMO-WCLL-MODULE-04",
            ToroidalMagneticFieldTesla: 7.0,
            ChannelHalfWidthMeters: 0.05,
            ChannelLengthMeters: 2.5,
            LiquidMetalFlowVelocityMPerSec: 0.45,
            WallConductanceRatio: 0.05,
            TritiumPermeationReductionFactor: 80.0,
            MeasuredMhdPressureDropMpa: 1.30,
            MaxAllowableStructuralPressureDropMpa: 2.2);

        var result = _engine.AnalyzeBlanketChannel(telemetry);

        Assert.Equal("MHD_PRESSURE_THROTTLING_WARNING", result.FlowRegimeStatus);
        Assert.Equal("THROTTLE_LIQUID_METAL_PUMP_SPEED_25_PERCENT", result.FlowControlRecommendation);
        Assert.False(result.IsMhdPressureLimitExceeded);
        Assert.True(result.PressureSafetyMarginPercent < 20.0);
    }

    [Fact]
    public void AnalyzeBlanketChannel_OverpressureExceeded_TriggersEmergencyBypass()
    {
        // High B = 8.5 T, high velocity 0.80 m/s -> pressure drop > 2.5 MPa, exceeding 2.0 MPa limit
        var telemetry = new LiquidMetalMhdTelemetry(
            BlanketModuleId: "CRITICAL-MHD-CHOKE",
            ToroidalMagneticFieldTesla: 8.5,
            ChannelHalfWidthMeters: 0.05,
            ChannelLengthMeters: 3.0,
            LiquidMetalFlowVelocityMPerSec: 0.80,
            WallConductanceRatio: 0.06,
            TritiumPermeationReductionFactor: 30.0, // PRF < 50 degraded
            MeasuredMhdPressureDropMpa: 2.8,
            MaxAllowableStructuralPressureDropMpa: 2.0);

        var result = _engine.AnalyzeBlanketChannel(telemetry);

        Assert.True(result.IsMhdPressureLimitExceeded);
        Assert.True(result.IsTritiumPermeationBarrierDegraded);
        Assert.Equal("CRITICAL_MHD_OVERPRESSURE_TRIP", result.FlowRegimeStatus);
        Assert.Equal("EMERGENCY_FLOW_BYPASS_AND_FIELD_RAMP_COORDINATION", result.FlowControlRecommendation);
        Assert.Contains("CRITICAL MHD HAZARD", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeBlanketChannel_ZeroMagneticField_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new LiquidMetalMhdTelemetry(
            BlanketModuleId: "ERR-001",
            ToroidalMagneticFieldTesla: 0.0,
            ChannelHalfWidthMeters: 0.05,
            ChannelLengthMeters: 1.0,
            LiquidMetalFlowVelocityMPerSec: 0.1,
            WallConductanceRatio: 0.03,
            TritiumPermeationReductionFactor: 100.0,
            MeasuredMhdPressureDropMpa: 0.0,
            MaxAllowableStructuralPressureDropMpa: 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeBlanketChannel(invalid));
    }
}
