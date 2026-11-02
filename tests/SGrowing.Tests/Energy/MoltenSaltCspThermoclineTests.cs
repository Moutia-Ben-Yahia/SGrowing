using FluentAssertions;
using SGrowing.Api.Modules.Energy.MoltenSaltCspThermocline;
using Xunit;

namespace SGrowing.Tests.Energy;

public class MoltenSaltCspThermoclineTests
{
    private readonly MoltenSaltCspThermoclineEngine _engine = new();

    [Fact]
    public void EvaluateThermocline_NominalStratifiedTank_YieldsHighCapacityUtilization()
    {
        var tel = new ThermoclineTankTelemetry(
            TankId: "CSP-NOOR-OUARZAZATE-01",
            TankHeightMeters: 14.0,
            TankDiameterMeters: 28.0,
            HotZoneTemperatureCelsius: 565.0,
            ColdZoneTemperatureCelsius: 290.0,
            MeasuredThermoclineThicknessMeters: 2.0, // Sharp 2m thermocline
            SaltMassFlowRateKgPerSec: 150.0,
            IsCharging: true
        );

        var lim = new ThermoclineOperationalLimits();
        var request = new ThermoclineEvaluationRequest(tel, lim);
        var plan = _engine.EvaluateThermocline(request);

        plan.TankId.Should().Be("CSP-NOOR-OUARZAZATE-01");
        plan.ThermoclineDegradationDetected.Should().BeFalse();
        plan.FreezePrecursorRiskDetected.Should().BeFalse();
        plan.StratificationStatus.Should().Be("SHARP_STRATIFIED_STORAGE_HEALTHY");
        plan.EffectiveCapacityUtilizationPercent.Should().BeGreaterThan(80.0);
        plan.StoredThermalEnergyMegaWattHours.Should().BeGreaterThan(1000.0);
        plan.RecommendedFlowAdjustmentDirective.Should().Contain("NOMINAL_DISPATCH");
    }

    [Fact]
    public void EvaluateThermocline_DegradedThermocline_RecommendsMassFlowThrottling()
    {
        // 5.2m thermocline exceeds 4.5m limit
        var tel = new ThermoclineTankTelemetry(
            TankId: "CSP-TANK-DEGRADED",
            TankHeightMeters: 14.0,
            TankDiameterMeters: 28.0,
            HotZoneTemperatureCelsius: 550.0,
            ColdZoneTemperatureCelsius: 300.0,
            MeasuredThermoclineThicknessMeters: 5.2, // Wide dispersed thermocline!
            SaltMassFlowRateKgPerSec: 220.0
        );

        var lim = new ThermoclineOperationalLimits(MaxAllowableThermoclineThicknessMeters: 4.5);
        var request = new ThermoclineEvaluationRequest(tel, lim);
        var plan = _engine.EvaluateThermocline(request);

        plan.ThermoclineDegradationDetected.Should().BeTrue();
        plan.StratificationStatus.Should().Be("THERMOCLINE_DEGRADATION_MIXING");
        plan.RecommendedFlowAdjustmentDirective.Should().Contain("THROTTLE_MASS_FLOW");
    }

    [Fact]
    public void EvaluateThermocline_NearSaltFreezingPoint_FiresAuxHeatingAlarm()
    {
        // Cold salt drops to 245°C (near 240°C safety freeze limit)
        var tel = new ThermoclineTankTelemetry(
            TankId: "CSP-COLD-WEATHER-ALERT",
            HotZoneTemperatureCelsius: 500.0,
            ColdZoneTemperatureCelsius: 245.0, // Freeze danger!
            MinColdHeadSafetyLimitCelsius: 240.0
        );

        var request = new ThermoclineEvaluationRequest(tel, new ThermoclineOperationalLimits());
        var plan = _engine.EvaluateThermocline(request);

        plan.FreezePrecursorRiskDetected.Should().BeTrue();
        plan.StratificationStatus.Should().Be("CRYSTALLIZATION_FREEZE_PRECURSOR");
        plan.RecommendedFlowAdjustmentDirective.Should().Contain("ENGAGE_AUX_HEATING");
    }

    [Fact]
    public void EvaluateThermocline_CalculatesThermalLossesSensibly()
    {
        var tel = new ThermoclineTankTelemetry("CSP-HEAT-LOSS", HotZoneTemperatureCelsius: 560.0, ColdZoneTemperatureCelsius: 290.0);
        var request = new ThermoclineEvaluationRequest(tel, new ThermoclineOperationalLimits());
        var plan = _engine.EvaluateThermocline(request);

        plan.ThermalLossRateKiloWatts.Should().BeGreaterThan(0.0);
    }
}
