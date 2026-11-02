namespace SGrowing.Tests.Aerospace;

using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.ScramjetThermalProtection;
using Xunit;

public class ScramjetThermalProtectionTests
{
    private readonly ScramjetThermalProtectionEngine _engine = new();

    [Fact]
    public void EvaluateThermalProtection_Mach7Nominal_CalculatesStagnationAndCoolantDemand()
    {
        var flight = new HypersonicFlightCondition(
            VehicleId: "HYPER-X-SCRAM-01",
            MachNumber: 7.0,
            AltitudeMeters: 28000.0,
            AngleOfAttackDegrees: 2.0,
            DynamicPressureKPa: 65.0,
            FreestreamVelocityMps: 2150.0
        );

        var channels = new List<CoolingChannelSegment>
        {
            new("CH-01", "LeadingEdgeNose", WallThicknessMm: 4.5, MaxAllowableTempKelvin: 2200.0, CurrentWallTempKelvin: 1600.0, CoolantMassFlowKgPerSec: 1.5),
            new("CH-02", "CowlLip", WallThicknessMm: 3.8, MaxAllowableTempKelvin: 2100.0, CurrentWallTempKelvin: 1700.0, CoolantMassFlowKgPerSec: 2.0),
            new("CH-03", "CombustorIsolator", WallThicknessMm: 5.0, MaxAllowableTempKelvin: 1900.0, CurrentWallTempKelvin: 1500.0, CoolantMassFlowKgPerSec: 1.2)
        };

        var request = new ScramjetThermalEvaluationRequest(flight, channels, AvailableTotalCoolantFlowKgPerSec: 15.0);
        var plan = _engine.EvaluateThermalProtection(request);

        plan.VehicleId.Should().Be("HYPER-X-SCRAM-01");
        plan.MachNumber.Should().Be(7.0);
        plan.StagnationTempKelvin.Should().BeGreaterThan(2200.0);
        plan.ThermalRunawayRisk.Should().BeFalse();
        plan.OverallStatus.Should().Contain("AEROTHERMAL_MARGIN_OPTIMAL");
        plan.Adjustments.Should().HaveCount(3);
    }

    [Fact]
    public void EvaluateThermalProtection_InsufficientCoolantSupply_FlagsThermalRunawayRisk()
    {
        var flight = new HypersonicFlightCondition(
            VehicleId: "HYPER-X-SCRAM-02",
            MachNumber: 8.5,
            AltitudeMeters: 24000.0,
            AngleOfAttackDegrees: 5.0,
            DynamicPressureKPa: 80.0,
            FreestreamVelocityMps: 2600.0
        );

        var channels = new List<CoolingChannelSegment>
        {
            new("CH-01", "LeadingEdgeNose", WallThicknessMm: 4.5, MaxAllowableTempKelvin: 2200.0, CurrentWallTempKelvin: 1900.0, CoolantMassFlowKgPerSec: 1.0),
            new("CH-02", "CowlLip", WallThicknessMm: 3.8, MaxAllowableTempKelvin: 2100.0, CurrentWallTempKelvin: 1950.0, CoolantMassFlowKgPerSec: 1.0)
        };

        // Available flow is capped severely at 0.5 kg/s (far below demand)
        var request = new ScramjetThermalEvaluationRequest(flight, channels, AvailableTotalCoolantFlowKgPerSec: 0.5);
        var plan = _engine.EvaluateThermalProtection(request);

        plan.ThermalRunawayRisk.Should().BeTrue();
        plan.OverallStatus.Should().Contain("CRITICAL_AEROTHERMAL_OVERLOAD");
    }

    [Fact]
    public void EvaluateThermalProtection_HighAngleOfAttack_AmplifiesShockInteractionFactor()
    {
        var lowAoA = new HypersonicFlightCondition("V1", 6.0, 26000.0, AngleOfAttackDegrees: 1.0, 50.0, 1850.0);
        var highAoA = new HypersonicFlightCondition("V1", 6.0, 26000.0, AngleOfAttackDegrees: 8.0, 50.0, 1850.0);

        var channels = new List<CoolingChannelSegment>
        {
            new("CH-01", "CowlLip", 4.0, 2100.0, 1500.0, 2.0)
        };

        var planLow = _engine.EvaluateThermalProtection(new ScramjetThermalEvaluationRequest(lowAoA, channels, 10.0));
        var planHigh = _engine.EvaluateThermalProtection(new ScramjetThermalEvaluationRequest(highAoA, channels, 10.0));

        planHigh.ShockInteractionAmplificationFactor.Should().BeGreaterThan(planLow.ShockInteractionAmplificationFactor);
    }

    [Fact]
    public void EvaluateThermalProtection_CowlLipLocation_RequiresHigherCoolantFlow()
    {
        var flight = new HypersonicFlightCondition("V2", 6.5, 27000.0, 2.0, 55.0, 2000.0);

        var channels = new List<CoolingChannelSegment>
        {
            new("CH-NOZZLE", "NozzleExpansionRamp", 5.0, 2000.0, 1200.0, 1.0),
            new("CH-COWL", "CowlLip", 5.0, 2000.0, 1200.0, 1.0)
        };

        var plan = _engine.EvaluateThermalProtection(new ScramjetThermalEvaluationRequest(flight, channels, 10.0));
        var cowlAdj = plan.Adjustments.First(a => a.SegmentId == "CH-COWL");
        var nozzleAdj = plan.Adjustments.First(a => a.SegmentId == "CH-NOZZLE");

        cowlAdj.RequiredCoolantFlowKgPerSec.Should().BeGreaterThan(nozzleAdj.RequiredCoolantFlowKgPerSec);
    }
}
