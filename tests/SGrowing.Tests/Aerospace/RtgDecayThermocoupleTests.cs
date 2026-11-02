using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.RtgDecayThermocouple;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class RtgDecayThermocoupleTests
{
    private readonly RtgDecayThermocoupleEngine _engine = new();

    [Fact]
    public void EvaluateRtgPower_EarlyFlightPhase_MaintainsAmplePowerMargin()
    {
        var tel = new RtgHardwareTelemetry(
            RtgId: "VOYAGER-INTERSTELLAR-01",
            InitialThermalPowerWatts: 4400.0,
            InitialElectricalPowerWatts: 295.0,
            YearsSinceFuelEncapsulation: 8.0, // 8 years post launch
            ThermocoupleMaterial: "SiGe",
            SpacecraftTotalLoadDemandWatts: 185.0
        );

        var request = new RtgEvaluationRequest(tel, new MissionPowerLimits());
        var assessment = _engine.EvaluateRtgPower(request);

        assessment.RtgId.Should().Be("VOYAGER-INTERSTELLAR-01");
        assessment.LoadSheddingMandatoryDetected.Should().BeFalse();
        assessment.PowerMarginStatus.Should().Be("POWER_BUDGET_SECURE_NOMINAL");
        assessment.CurrentAvailableElectricalPowerWatts.Should().BeGreaterThan(240.0);
        assessment.CurrentThermalPowerWatts.Should().BeGreaterThan(4000.0);
        assessment.ProjectedPowerWattsInTenYears.Should().BeGreaterThan(200.0);
        assessment.PowerManagementDirective.Should().Contain("POWER_NOMINAL");
    }

    [Fact]
    public void EvaluateRtgPower_DecadesInFlight_DemandsEmergencyLoadShedding()
    {
        // 42 years post launch: Pu-238 alpha decay and thermocouple degradation drop power below 180 We
        var tel = new RtgHardwareTelemetry(
            RtgId: "NEW-HORIZONS-KUIPER",
            InitialThermalPowerWatts: 4400.0,
            InitialElectricalPowerWatts: 295.0,
            YearsSinceFuelEncapsulation: 42.0, // 42 years in flight!
            ThermocoupleMaterial: "SiGe",
            SpacecraftTotalLoadDemandWatts: 195.0 // Exceeds available output!
        );

        var limits = new MissionPowerLimits(MinAllowableBusPowerWatts: 180.0);
        var request = new RtgEvaluationRequest(tel, limits);
        var assessment = _engine.EvaluateRtgPower(request);

        assessment.LoadSheddingMandatoryDetected.Should().BeTrue();
        assessment.PowerMarginStatus.Should().Be("NEGATIVE_POWER_MARGIN_CRITICAL");
        assessment.CurrentAvailableElectricalPowerWatts.Should().BeLessThan(195.0);
        assessment.PowerManagementDirective.Should().Contain("EMERGENCY_LOAD_SHEDDING");
    }

    [Fact]
    public void EvaluateRtgPower_MarginalHeadroom_FlagsConservationModeWarning()
    {
        var tel = new RtgHardwareTelemetry(
            RtgId: "DRAGONFLY-TITAN-MISSION",
            InitialThermalPowerWatts: 4400.0,
            InitialElectricalPowerWatts: 295.0,
            YearsSinceFuelEncapsulation: 24.0,
            SpacecraftTotalLoadDemandWatts: 190.0
        );

        var limits = new MissionPowerLimits(CriticalLoadSheddingThresholdWatts: 215.0);
        var request = new RtgEvaluationRequest(tel, limits);
        var assessment = _engine.EvaluateRtgPower(request);

        assessment.LoadSheddingMandatoryDetected.Should().BeFalse();
        assessment.PowerMarginStatus.Should().Be("MARGINAL_POWER_HEADROOM_WARNING");
        assessment.PowerManagementDirective.Should().Contain("PREPARE_CONSERVATION_MODE");
    }

    [Fact]
    public void EvaluateRtgPower_CalculatesThermalDecayAccurately()
    {
        // At t = 87.7 years (1 half life), thermal power should be exactly 50% of initial
        var tel = new RtgHardwareTelemetry("HALF-LIFE-CHECK", InitialThermalPowerWatts: 4000.0, YearsSinceFuelEncapsulation: 87.7);
        var request = new RtgEvaluationRequest(tel, new MissionPowerLimits());
        var assessment = _engine.EvaluateRtgPower(request);

        assessment.CurrentThermalPowerWatts.Should().BeInRange(1990.0, 2010.0);
    }
}
