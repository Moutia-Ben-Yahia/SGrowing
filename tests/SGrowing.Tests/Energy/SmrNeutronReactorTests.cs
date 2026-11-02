namespace SGrowing.Tests.Energy;

using FluentAssertions;
using SGrowing.Api.Modules.Energy.SmrNeutronReactor;
using Xunit;

public class SmrNeutronReactorTests
{
    private readonly SmrNeutronReactorEngine _engine = new();

    [Fact]
    public void EvaluateTransient_PowerDown_PredictsXenonPeakAndWithdrawsControlRods()
    {
        var coreState = new SmrReactorCoreState(
            ReactorId: "SMR-NUSCALE-01",
            CurrentThermalPowerMw: 100.0,
            RatedThermalPowerMw: 100.0,
            ThermalNeutronFluxNPerCm2S: 2.5e13,
            FastNeutronFluxNPerCm2S: 4.1e13,
            CoreCoolantInletTempCelsius: 290.0,
            CoreCoolantOutletTempCelsius: 320.0,
            CurrentBoronConcentrationPpm: 600.0,
            ControlRodBankInsertionPercent: 35.0,
            Iodine135InventoryAtomsPerCm3: 1.8e15,
            Xenon135InventoryAtomsPerCm3: 1.2e15,
            ShutdownMarginPcm: 2000.0
        );

        // Power drop from 100 MWth to 20 MWth over a 16-hour lookahead
        var request = new XenonTransientEvaluationRequest(coreState, TargetThermalPowerMw: 20.0, LookaheadHours: 16.0);
        var decision = _engine.EvaluateTransient(request);

        decision.ReactorId.Should().Be("SMR-NUSCALE-01");
        decision.PeakNegativeReactivityPcm.Should().BeLessThan(-2800.0);
        decision.TimeToPeakNegativeReactivityHours.Should().BeInRange(4.0, 12.0);
        decision.TransientTrajectory.Should().HaveCount(13);
        decision.OperatingAction.Should().Contain("Withdraw control bank");
    }

    [Fact]
    public void EvaluateTransient_SevereNegativeXenonReactivityExceedingMargins_FlagsDeadlockRisk()
    {
        // Minimal boron and nearly withdrawn rods leave zero margin for xenon transient
        var coreState = new SmrReactorCoreState(
            ReactorId: "SMR-WESTINGHOUSE-SMR",
            CurrentThermalPowerMw: 100.0,
            RatedThermalPowerMw: 100.0,
            ThermalNeutronFluxNPerCm2S: 2.8e13,
            FastNeutronFluxNPerCm2S: 4.5e13,
            CoreCoolantInletTempCelsius: 285.0,
            CoreCoolantOutletTempCelsius: 315.0,
            CurrentBoronConcentrationPpm: 10.0,
            ControlRodBankInsertionPercent: 2.0,
            Iodine135InventoryAtomsPerCm3: 2.2e15,
            Xenon135InventoryAtomsPerCm3: 1.5e15,
            ShutdownMarginPcm: 500.0
        );

        var request = new XenonTransientEvaluationRequest(coreState, TargetThermalPowerMw: 10.0, LookaheadHours: 12.0);
        var decision = _engine.EvaluateTransient(request);

        decision.XenonDeadlockRisk.Should().BeTrue();
        decision.OperatingAction.Should().Contain("CRITICAL_XENON_DEADLOCK_PREVENT");
    }

    [Fact]
    public void EvaluateTransient_PowerRampUp_MaintainsStableTrim()
    {
        var coreState = new SmrReactorCoreState(
            ReactorId: "SMR-ROLLS-ROYCE-01",
            CurrentThermalPowerMw: 50.0,
            RatedThermalPowerMw: 100.0,
            ThermalNeutronFluxNPerCm2S: 1.2e13,
            FastNeutronFluxNPerCm2S: 2.0e13,
            CoreCoolantInletTempCelsius: 280.0,
            CoreCoolantOutletTempCelsius: 305.0,
            CurrentBoronConcentrationPpm: 450.0,
            ControlRodBankInsertionPercent: 20.0,
            Iodine135InventoryAtomsPerCm3: 0.9e15,
            Xenon135InventoryAtomsPerCm3: 0.7e15,
            ShutdownMarginPcm: 2500.0
        );

        var request = new XenonTransientEvaluationRequest(coreState, TargetThermalPowerMw: 80.0, LookaheadHours: 8.0);
        var decision = _engine.EvaluateTransient(request);

        decision.XenonDeadlockRisk.Should().BeFalse();
        decision.OperatingAction.Should().Contain("STABLE_TRANSIENT");
    }

    [Fact]
    public void EvaluateTransient_TrajectoryPoints_HaveAccurateStepProgression()
    {
        var coreState = new SmrReactorCoreState(
            ReactorId: "SMR-TEST-01",
            CurrentThermalPowerMw: 100.0,
            RatedThermalPowerMw: 100.0,
            ThermalNeutronFluxNPerCm2S: 2.0e13,
            FastNeutronFluxNPerCm2S: 3.5e13,
            CoreCoolantInletTempCelsius: 290.0,
            CoreCoolantOutletTempCelsius: 318.0,
            CurrentBoronConcentrationPpm: 500.0,
            ControlRodBankInsertionPercent: 25.0,
            Iodine135InventoryAtomsPerCm3: 1.5e15,
            Xenon135InventoryAtomsPerCm3: 1.0e15,
            ShutdownMarginPcm: 1800.0
        );

        var request = new XenonTransientEvaluationRequest(coreState, TargetThermalPowerMw: 40.0, LookaheadHours: 24.0);
        var decision = _engine.EvaluateTransient(request);

        decision.TransientTrajectory.First().HoursPostTransition.Should().Be(0.0);
        decision.TransientTrajectory.Last().HoursPostTransition.Should().Be(24.0);
        decision.TransientTrajectory.Should().OnlyContain(s => s.EstimatedXenonReactivityPcm < 0);
    }
}
