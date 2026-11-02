namespace SGrowing.Tests.Energy;

using FluentAssertions;
using SGrowing.Api.Modules.Energy.SolidStateBatteryEis;
using Xunit;

public class SolidStateBatteryEisTests
{
    private readonly SolidStateBatteryEisEngine _engine = new();

    [Fact]
    public void EvaluateEisSpectrum_HealthyCell_ReportsLowDendriteRiskAndFastCharge()
    {
        var cell = new BatteryCellCondition(
            CellId: "SSB-GARNET-01",
            ElectrolyteType: "LLZO_Garnet",
            StateOfChargePercent: 65.0,
            TemperatureCelsius: 25.0,
            NominalCapacityAh: 5.0,
            ChargingCurrentAmps: 5.0 // 1.0C
        );

        // Healthy Nyquist plot: distinct semicircles with phase angle ~ 35 deg
        var spectrum = new List<EisSpectrumPoint>
        {
            new(50000.0, RealImpedanceOhms: 0.045, ImaginaryImpedanceOhms: 0.001, PhaseAngleDegrees: 1.5),
            new(1000.0, RealImpedanceOhms: 0.180, ImaginaryImpedanceOhms: -0.065, PhaseAngleDegrees: 28.0),
            new(10.0, RealImpedanceOhms: 0.350, ImaginaryImpedanceOhms: -0.110, PhaseAngleDegrees: 34.0),
            new(0.5, RealImpedanceOhms: 0.650, ImaginaryImpedanceOhms: -0.250, PhaseAngleDegrees: 45.0)
        };

        var request = new EisEvaluationRequest(cell, spectrum);
        var assessment = _engine.EvaluateEisSpectrum(request);

        assessment.CellId.Should().Be("SSB-GARNET-01");
        assessment.DendriteNucleationDetected.Should().BeFalse();
        assessment.DendriteRiskScorePercent.Should().BeLessThan(35.0);
        assessment.HealthStatus.Should().Contain("HEALTHY_SOLID_ELECTROLYTE_INTERPHASE");
        assessment.ChargingProtocolRecommendation.Should().Contain("STANDARD_FAST_CHARGE");
    }

    [Fact]
    public void EvaluateEisSpectrum_PercolationAndPhaseCollapse_DetectsDendritesAndPrescribesPulseHealing()
    {
        var cell = new BatteryCellCondition(
            CellId: "SSB-SULFIDE-DENDRITE-RISK",
            ElectrolyteType: "Argyrodite_Sulfide",
            StateOfChargePercent: 85.0,
            TemperatureCelsius: 22.0,
            NominalCapacityAh: 5.0,
            ChargingCurrentAmps: 6.0
        );

        // Collapsed SEI semicircle and phase collapse indicating local electronic percolation
        var spectrum = new List<EisSpectrumPoint>
        {
            new(50000.0, RealImpedanceOhms: 0.035, ImaginaryImpedanceOhms: 0.0, PhaseAngleDegrees: 0.5),
            new(1000.0, RealImpedanceOhms: 0.055, ImaginaryImpedanceOhms: -0.005, PhaseAngleDegrees: 4.0), // Phase collapsed < 7 deg
            new(10.0, RealImpedanceOhms: 0.065, ImaginaryImpedanceOhms: -0.008, PhaseAngleDegrees: 5.5)
        };

        var request = new EisEvaluationRequest(cell, spectrum);
        var assessment = _engine.EvaluateEisSpectrum(request);

        assessment.DendriteNucleationDetected.Should().BeTrue();
        assessment.HealthStatus.Should().Contain("CRITICAL_LITHIUM_DENDRITE_PROPAGATION");
        assessment.ChargingProtocolRecommendation.Should().Contain("ASYMMETRIC_PULSE_HEALING");
    }

    [Fact]
    public void EvaluateEisSpectrum_EmptySpectrum_ReturnsSafeBaselineDefaults()
    {
        var cell = new BatteryCellCondition("SSB-EMPTY", "Polymer_PEO", 50.0, 30.0, 3.0, 1.5);
        var request = new EisEvaluationRequest(cell, []);

        var assessment = _engine.EvaluateEisSpectrum(request);

        assessment.DendriteNucleationDetected.Should().BeFalse();
        assessment.ChargingProtocolRecommendation.Should().Contain("STANDARD_CCCV");
    }

    [Fact]
    public void EvaluateEisSpectrum_LowTemperatureHighCurrent_IncreasesRiskScore()
    {
        var warmCell = new BatteryCellCondition("C1", "LLZO_Garnet", 50.0, TemperatureCelsius: 35.0, 5.0, ChargingCurrentAmps: 2.5);
        var freezingCell = new BatteryCellCondition("C2", "LLZO_Garnet", 50.0, TemperatureCelsius: 2.0, 5.0, ChargingCurrentAmps: 10.0); // 2C at 2 degC

        var spectrum = new List<EisSpectrumPoint>
        {
            new(50000.0, 0.05, 0.0, 1.0),
            new(100.0, 0.20, -0.05, 20.0)
        };

        var warmResult = _engine.EvaluateEisSpectrum(new EisEvaluationRequest(warmCell, spectrum));
        var coldResult = _engine.EvaluateEisSpectrum(new EisEvaluationRequest(freezingCell, spectrum));

        coldResult.DendriteRiskScorePercent.Should().BeGreaterThan(warmResult.DendriteRiskScorePercent);
    }
}
