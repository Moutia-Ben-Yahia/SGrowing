namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.MhdBoundaryLayerFlowControl;
using Xunit;

public class MhdBoundaryLayerFlowControlTests
{
    private readonly MhdBoundaryLayerFlowControlEngine _engine = new();

    [Fact]
    public void AnalyzeMhdBoundaryLayer_NominalHypersonicCruise_ReturnsOptimalRegime()
    {
        var telemetry = new MhdBoundaryLayerTelemetry(
            HypersonicVehicleId: "WAVERIDER-MHD-01",
            FreeStreamMachNumber: 8.5,
            FlightAltitudeKilometers: 32.0,
            AppliedMagneticFieldTesla: 2.2,
            BoundaryLayerPlasmaElectricalConductivitySiemensPerM: 55.0,
            FlowInteractionLengthMeters: 2.0,
            UpstreamDynamicPressureKiloPascals: 45.0,
            MhdGeneratorLoadFactorK: 0.50, // Max power generation point
            WallSurfaceHeatFluxMegaWattsPerM2: 2.4
        );

        var result = _engine.AnalyzeMhdBoundaryLayer(telemetry);

        result.HypersonicVehicleId.Should().Be("WAVERIDER-MHD-01");
        result.MhdFlowRegime.Should().Be("OPTIMAL_HYPERSONIC_MHD_FLOW_CONTROL");
        result.IsMhdControlEffective.Should().BeTrue();
        result.MhdInteractionParameterStuartNumber.Should().BeInRange(1.0, 25.0);
        result.SkinFrictionDragReductionPercent.Should().BeGreaterThan(5.0);
        result.ExtractedMhdElectricPowerKiloWatts.Should().BeGreaterThan(100.0);
        result.InducedLorentzDecelerationForceKiloNewtons.Should().BeGreaterThan(0.5);
        result.BoundaryLayerControlGuidance.Should().ContainEquivalentOf("Skin friction drag suppressed");
    }

    [Fact]
    public void AnalyzeMhdBoundaryLayer_ExcessiveStuartNumber_ReturnsInletChokingSurge()
    {
        var telemetry = new MhdBoundaryLayerTelemetry(
            HypersonicVehicleId: "WAVERIDER-MHD-02",
            FreeStreamMachNumber: 6.0,
            FlightAltitudeKilometers: 55.0, // Low density -> Stuart number spikes
            AppliedMagneticFieldTesla: 3.5, // High B
            BoundaryLayerPlasmaElectricalConductivitySiemensPerM: 120.0,
            FlowInteractionLengthMeters: 3.0,
            UpstreamDynamicPressureKiloPascals: 20.0,
            MhdGeneratorLoadFactorK: 0.30,
            WallSurfaceHeatFluxMegaWattsPerM2: 1.8
        );

        var result = _engine.AnalyzeMhdBoundaryLayer(telemetry);

        result.MhdFlowRegime.Should().Be("HYPERSONIC_MHD_INLET_CHOKING_SURGE");
        result.IsMhdControlEffective.Should().BeFalse();
        result.MhdInteractionParameterStuartNumber.Should().BeGreaterThan(25.0);
        result.BoundaryLayerControlGuidance.Should().ContainEquivalentOf("scramjet inlet unstart");
    }

    [Fact]
    public void AnalyzeMhdBoundaryLayer_WeakIonization_ReturnsWeakCoupling()
    {
        var telemetry = new MhdBoundaryLayerTelemetry(
            HypersonicVehicleId: "WAVERIDER-MHD-03",
            FreeStreamMachNumber: 5.5,
            FlightAltitudeKilometers: 20.0, // High density -> small S
            AppliedMagneticFieldTesla: 0.8,
            BoundaryLayerPlasmaElectricalConductivitySiemensPerM: 2.0, // Very low sigma
            FlowInteractionLengthMeters: 1.5,
            UpstreamDynamicPressureKiloPascals: 65.0,
            MhdGeneratorLoadFactorK: 0.50,
            WallSurfaceHeatFluxMegaWattsPerM2: 1.2
        );

        var result = _engine.AnalyzeMhdBoundaryLayer(telemetry);

        result.MhdFlowRegime.Should().Be("WEAK_MHD_IONIZATION_INSUFFICIENT_COUPLING");
        result.IsMhdControlEffective.Should().BeFalse();
        result.MhdInteractionParameterStuartNumber.Should().BeLessThan(0.8);
        result.BoundaryLayerControlGuidance.Should().ContainEquivalentOf("seed electron-beam");
    }

    [Fact]
    public void AnalyzeMhdBoundaryLayer_HighThermalFlux_ReturnsThermalAblativeWarning()
    {
        var telemetry = new MhdBoundaryLayerTelemetry(
            HypersonicVehicleId: "WAVERIDER-MHD-04",
            FreeStreamMachNumber: 10.0,
            FlightAltitudeKilometers: 28.0,
            AppliedMagneticFieldTesla: 1.8,
            BoundaryLayerPlasmaElectricalConductivitySiemensPerM: 40.0,
            FlowInteractionLengthMeters: 2.0,
            UpstreamDynamicPressureKiloPascals: 55.0,
            MhdGeneratorLoadFactorK: 0.15,
            WallSurfaceHeatFluxMegaWattsPerM2: 4.8 // Exceeds 4.0 MW/m2
        );

        var result = _engine.AnalyzeMhdBoundaryLayer(telemetry);

        result.MhdFlowRegime.Should().Be("THERMAL_ABLATATIVE_LIMIT_WARNING");
        result.IsMhdControlEffective.Should().BeFalse();
        result.BoundaryLayerControlGuidance.Should().ContainEquivalentOf("cooling the boundary layer");
    }

    [Fact]
    public void AnalyzeMhdBoundaryLayer_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeMhdBoundaryLayer(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
