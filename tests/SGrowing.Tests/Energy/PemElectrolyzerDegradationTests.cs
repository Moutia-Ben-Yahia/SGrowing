using FluentAssertions;
using SGrowing.Api.Modules.Energy.PemElectrolyzerDegradation;
using Xunit;

namespace SGrowing.Tests.Energy;

public class PemElectrolyzerDegradationTests
{
    private readonly PemElectrolyzerDegradationEngine _engine = new();

    [Fact]
    public void AssessDegradation_NominalMWStack_ProducesHighFaradayYield()
    {
        var tel = new PemElectrolyzerCellTelemetry(
            StackId: "PEM-MW-STACK-01",
            CellCount: 120,
            CurrentDensityAmpsPerCm2: 2.0,
            OperatingTemperatureCelsius: 70.0,
            OperatingPressureBar: 30.0,
            CellVoltageVolts: 1.88,
            HydrogenInOxygenPercent: 0.35,
            FluorineEmissionRateMicrogramsPerCm2Hour: 0.10,
            CumulativeOperatingHours: 1200.0
        );

        var request = new PemElectrolyzerEvaluationRequest(tel, new PemDegradationThresholds());
        var assessment = _engine.AssessDegradation(request);

        assessment.StackId.Should().Be("PEM-MW-STACK-01");
        assessment.FlammabilityCrossoverRiskDetected.Should().BeFalse();
        assessment.StackDegradationStatus.Should().Be("NOMINAL_ELECTROLYSIS_HEALTH");
        assessment.FaradayEfficiencyPercent.Should().BeGreaterThan(98.0);
        assessment.HydrogenProductionRateNm3PerHour.Should().BeGreaterThan(40.0);
        assessment.EstimatedRemainingMembraneLifeHours.Should().BeGreaterThan(30000.0);
    }

    [Fact]
    public void AssessDegradation_HighHydrogenCrossover_TriggersEmergencyShutdown()
    {
        // 2.2% H2 in O2 exceeds the 1.8% LEL safety threshold
        var tel = new PemElectrolyzerCellTelemetry(
            StackId: "PEM-STACK-PINHOLE-DANGER",
            CellCount: 100,
            CurrentDensityAmpsPerCm2: 1.5,
            OperatingTemperatureCelsius: 65.0,
            OperatingPressureBar: 35.0,
            CellVoltageVolts: 2.05,
            HydrogenInOxygenPercent: 2.2, // Hazard!
            FluorineEmissionRateMicrogramsPerCm2Hour: 0.45,
            CumulativeOperatingHours: 18000.0
        );

        var thresh = new PemDegradationThresholds(MaxAllowableHydrogenInOxygenPercent: 1.8);
        var request = new PemElectrolyzerEvaluationRequest(tel, thresh);
        var assessment = _engine.AssessDegradation(request);

        assessment.FlammabilityCrossoverRiskDetected.Should().BeTrue();
        assessment.StackDegradationStatus.Should().Be("CRITICAL_CROSSOVER_SHUTDOWN");
        assessment.RecommendedMitigationDirective.Should().Contain("EMERGENCY_CATHODE_PURGE");
    }

    [Fact]
    public void AssessDegradation_AcceleratedMembraneThinning_RecommendsCurrentThrottle()
    {
        // Severe fluorine release rate (0.95 ug/cm2-h) and elevated voltage (2.25 V)
        var tel = new PemElectrolyzerCellTelemetry(
            StackId: "PEM-STACK-DEGRADED-MEMBRANE",
            CellCount: 80,
            CurrentDensityAmpsPerCm2: 2.2,
            OperatingTemperatureCelsius: 80.0,
            OperatingPressureBar: 25.0,
            CellVoltageVolts: 2.25,
            HydrogenInOxygenPercent: 1.2,
            FluorineEmissionRateMicrogramsPerCm2Hour: 0.95,
            CumulativeOperatingHours: 28000.0
        );

        var thresh = new PemDegradationThresholds();
        var request = new PemElectrolyzerEvaluationRequest(tel, thresh);
        var assessment = _engine.AssessDegradation(request);

        assessment.FlammabilityCrossoverRiskDetected.Should().BeFalse();
        assessment.StackDegradationStatus.Should().Be("ACCELERATED_MEMBRANE_THINNING");
        assessment.RecommendedMitigationDirective.Should().Contain("THROTTLE_CURRENT_AND_COOL");
    }

    [Fact]
    public void AssessDegradation_CalculatesOhmicResistanceConsistently()
    {
        var tel = new PemElectrolyzerCellTelemetry(
            StackId: "PEM-OHMIC-CHECK",
            CellCount: 50,
            CurrentDensityAmpsPerCm2: 1.0,
            OperatingTemperatureCelsius: 60.0,
            OperatingPressureBar: 20.0,
            CellVoltageVolts: 1.80
        );

        var request = new PemElectrolyzerEvaluationRequest(tel, new PemDegradationThresholds());
        var assessment = _engine.AssessDegradation(request);

        assessment.OhmicResistanceMilliOhmCm2.Should().BeGreaterThan(50.0);
        assessment.OhmicResistanceMilliOhmCm2.Should().BeLessThan(500.0);
    }
}
