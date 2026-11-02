using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.HypersonicWaveriderShock;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class HypersonicWaveriderShockTests
{
    private readonly HypersonicWaveriderShockEngine _engine = new();

    [Fact]
    public void EvaluateAerothermalState_NominalCruise_AttachesShockWithHighLiftToDrag()
    {
        var state = new WaveriderFlightState(
            VehicleId: "WAVERIDER-MACH8-01",
            MachNumber: 8.0,
            AltitudeKm: 34.0,
            DynamicPressureKPa: 60.0,
            AngleOfAttackDegrees: 6.0,
            LeadingEdgeTemperatureKelvin: 1820.0,
            ActiveTranspirationFlowRateGramsPerSecM2: 14.0
        );

        var env = new WaveriderSafetyEnvelope();
        var request = new HypersonicWaveriderEvaluationRequest(state, env);
        var plan = _engine.EvaluateAerothermalState(request);

        plan.VehicleId.Should().Be("WAVERIDER-MACH8-01");
        plan.ShockAttachedToLeadingEdge.Should().BeTrue();
        plan.ThermalExcursionWarningDetected.Should().BeFalse();
        plan.AerodynamicFlightRegime.Should().Be("WAVERIDER_ATTACHED_CRUISE");
        plan.LiftToDragRatio.Should().BeGreaterThan(4.5);
        plan.FlightGuidanceDirective.Should().Contain("CRUISE_NOMINAL");
    }

    [Fact]
    public void EvaluateAerothermalState_OffDesignMach_DetachesShockAndSuffersDrag()
    {
        // Mach 4.5 is below design shock attachment Mach of 6.0
        var state = new WaveriderFlightState(
            VehicleId: "WAVERIDER-TRANSONIC-OFFDESIGN",
            MachNumber: 4.5, // Below attachment Mach
            AltitudeKm: 22.0,
            DynamicPressureKPa: 55.0,
            AngleOfAttackDegrees: 5.0,
            LeadingEdgeTemperatureKelvin: 1200.0,
            ActiveTranspirationFlowRateGramsPerSecM2: 8.0
        );

        var env = new WaveriderSafetyEnvelope(DesignShockAttachmentMach: 6.0);
        var request = new HypersonicWaveriderEvaluationRequest(state, env);
        var plan = _engine.EvaluateAerothermalState(request);

        plan.ShockAttachedToLeadingEdge.Should().BeFalse();
        plan.AerodynamicFlightRegime.Should().Be("SHOCK_DETACHED_SPILLAGE_DRAG");
        plan.LiftToDragRatio.Should().BeLessThan(3.5);
        plan.FlightGuidanceDirective.Should().Contain("REALIGN_SHOCK");
    }

    [Fact]
    public void EvaluateAerothermalState_SevereAerothermalHeating_SurgesTranspirationCoolant()
    {
        // Leading edge reaches 2120 K (within 150K of 2200 K UHTC limit)
        var state = new WaveriderFlightState(
            VehicleId: "WAVERIDER-THERMAL-EXCURSION",
            MachNumber: 8.8,
            AltitudeKm: 28.0,
            DynamicPressureKPa: 80.0,
            AngleOfAttackDegrees: 7.0,
            LeadingEdgeTemperatureKelvin: 2120.0, // High temperature!
            ActiveTranspirationFlowRateGramsPerSecM2: 20.0
        );

        var env = new WaveriderSafetyEnvelope(MaxAllowableLeadingEdgeTempKelvin: 2200.0);
        var request = new HypersonicWaveriderEvaluationRequest(state, env);
        var plan = _engine.EvaluateAerothermalState(request);

        plan.ThermalExcursionWarningDetected.Should().BeTrue();
        plan.AerodynamicFlightRegime.Should().Be("AEROTHERMAL_BOUNDARY_EXCURSION");
        plan.RecommendedTranspirationCoolingFlowRate.Should().BeGreaterThan(25.0);
        plan.FlightGuidanceDirective.Should().Contain("SURGE_TRANSPIRATION_COOLING");
    }

    [Fact]
    public void EvaluateAerothermalState_DynamicPressureExceeded_CommandsClimbTrim()
    {
        // 98 kPa dynamic pressure exceeds 90 kPa limit
        var state = new WaveriderFlightState(
            VehicleId: "WAVERIDER-HIGH-Q",
            MachNumber: 7.0,
            AltitudeKm: 24.0,
            DynamicPressureKPa: 98.0, // Exceeds limit
            AngleOfAttackDegrees: 6.0,
            LeadingEdgeTemperatureKelvin: 1650.0,
            ActiveTranspirationFlowRateGramsPerSecM2: 12.0
        );

        var env = new WaveriderSafetyEnvelope(MaxAllowableDynamicPressureKPa: 90.0);
        var request = new HypersonicWaveriderEvaluationRequest(state, env);
        var plan = _engine.EvaluateAerothermalState(request);

        plan.AerodynamicFlightRegime.Should().Be("DYNAMIC_PRESSURE_CEILING_BREACH");
        plan.FlightGuidanceDirective.Should().Contain("CLIMB_CORRIDOR_TRIM");
    }
}
