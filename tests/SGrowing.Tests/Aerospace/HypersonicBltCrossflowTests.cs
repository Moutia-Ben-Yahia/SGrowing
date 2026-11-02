namespace SGrowing.Tests.Aerospace;

using SGrowing.Api.Modules.Aerospace.HypersonicBltCrossflow;
using Xunit;

public class HypersonicBltCrossflowTests
{
    private readonly HypersonicBltEngine _engine = new();

    [Fact]
    public void EvaluateBoundaryLayer_NominalLaminarFlow_MaintainsCruiseAndHighMargin()
    {
        var telemetry = new HypersonicBltTelemetry(
            VehicleId: "WAVERIDER-HTV-01",
            MachNumber: 7.0,
            FreestreamDynamicPressureKpa: 40.0,
            AcousticSensorFrequencyKhz: 220.0,
            PressureFluctuationRmsRatio: 0.02, // Well below 0.05 onset
            CrossflowReynoldsNumber: 120.0,    // Well below 220
            SurfaceLaminarHeatFluxKwM2: 200.0,
            ThermalProtectionTileLimitKwM2: 1200.0);

        var result = _engine.EvaluateBoundaryLayer(telemetry);

        Assert.Equal("WAVERIDER-HTV-01", result.VehicleId);
        Assert.Equal("LAMINAR_FLOW", result.FlowRegime);
        Assert.Equal(1.0, result.TurbulentHeatFluxMultiplier);
        Assert.Equal(200.0, result.PredictedSurfaceHeatFluxKwM2);
        Assert.False(result.IsTileOverheatRiskDetected);
        Assert.Equal("NONE_MAINTAIN_CRUISE", result.ActiveFlowControlCommand);
        Assert.Contains("NOMINAL LAMINAR FLOW", result.FlightControlAdvisory);
    }

    [Fact]
    public void EvaluateBoundaryLayer_TransitionOnset_ActivatesSuctionPanels()
    {
        var telemetry = new HypersonicBltTelemetry(
            VehicleId: "GLIDER-HGV-02",
            MachNumber: 8.0,
            FreestreamDynamicPressureKpa: 55.0,
            AcousticSensorFrequencyKhz: 310.0, // Mack 2nd mode resonance
            PressureFluctuationRmsRatio: 0.07, // Between 0.05 and 0.12
            CrossflowReynoldsNumber: 240.0,
            SurfaceLaminarHeatFluxKwM2: 250.0,
            ThermalProtectionTileLimitKwM2: 1500.0);

        var result = _engine.EvaluateBoundaryLayer(telemetry);

        Assert.Equal("TRANSITION_ONSET", result.FlowRegime);
        Assert.True(result.TurbulentHeatFluxMultiplier > 1.0);
        Assert.Equal("ACTIVATE_BOUNDARY_LAYER_SUCTION", result.ActiveFlowControlCommand);
        Assert.Contains("TRANSITION WARNING", result.FlightControlAdvisory);
    }

    [Fact]
    public void EvaluateBoundaryLayer_FullyTurbulentOverheat_DeploysCoolingAndGenerators()
    {
        var telemetry = new HypersonicBltTelemetry(
            VehicleId: "SPACE-SHUTTLE-REENTRY-X",
            MachNumber: 9.5,
            FreestreamDynamicPressureKpa: 70.0,
            AcousticSensorFrequencyKhz: 180.0,
            PressureFluctuationRmsRatio: 0.16, // Turbulent > 0.12
            CrossflowReynoldsNumber: 420.0,
            SurfaceLaminarHeatFluxKwM2: 300.0,
            ThermalProtectionTileLimitKwM2: 1400.0);

        var result = _engine.EvaluateBoundaryLayer(telemetry);

        Assert.Equal("FULLY_DEVELOPED_TURBULENT", result.FlowRegime);
        Assert.True(result.TurbulentHeatFluxMultiplier >= 4.0);
        Assert.True(result.PredictedSurfaceHeatFluxKwM2 >= 1200.0);
        Assert.True(result.IsTileOverheatRiskDetected);
        Assert.Equal("DEPLOY_MICRO_VORTEX_GENERATORS_AND_COOLING", result.ActiveFlowControlCommand);
        Assert.Contains("CRITICAL AEROTHERMAL ALERT", result.FlightControlAdvisory);
    }

    [Fact]
    public void EvaluateBoundaryLayer_SubsonicMach_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new HypersonicBltTelemetry(
            VehicleId: "ERR-SUBSONIC",
            MachNumber: 0.8, // Subsonic < 1.0
            FreestreamDynamicPressureKpa: 20.0,
            AcousticSensorFrequencyKhz: 50.0,
            PressureFluctuationRmsRatio: 0.01,
            CrossflowReynoldsNumber: 50.0,
            SurfaceLaminarHeatFluxKwM2: 50.0,
            ThermalProtectionTileLimitKwM2: 500.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.EvaluateBoundaryLayer(invalid));
    }
}
