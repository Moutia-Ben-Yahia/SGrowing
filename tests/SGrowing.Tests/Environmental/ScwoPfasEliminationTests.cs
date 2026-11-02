namespace SGrowing.Tests.Environmental;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Environmental.ScwoPfasElimination;
using Xunit;

public class ScwoPfasEliminationTests
{
    private readonly ScwoPfasEliminationEngine _engine = new();

    [Fact]
    public void AnalyzeScwoProcess_NominalSupercriticalConditions_ReturnsCompleteMineralization()
    {
        var telemetry = new ScwoTelemetry(
            ReactorId: "SCWO-REACTOR-01",
            ReactorCoreTemperatureCelsius: 560.0,
            ReactorOperatingPressureMegaPascals: 26.5,
            ResidenceTimeSeconds: 65.0,
            DissolvedOxygenStoichiometricRatio: 1.45,
            InfluentTotalPfasConcentrationPpm: 50.0,
            InorganicSaltPrecipitationPluggingRisk: 0.15,
            TranspiringWallWaterFlowRateLitersPerMin: 12.0
        );

        var result = _engine.AnalyzeScwoProcess(telemetry);

        result.ReactorId.Should().Be("SCWO-REACTOR-01");
        result.ScwoDestructionRegime.Should().Be("SUPERCRITICAL_COMPLETE_MINERALIZATION_OPTIMAL");
        result.IsDestructionComplete.Should().BeTrue();
        result.PfasDefluorinationEfficiencyPercent.Should().BeGreaterThan(99.90);
        result.ResidualPfasConcentrationPpb.Should().BeLessThan(25.0);
        result.TotalOrganicCarbonDestructionPercent.Should().BeGreaterThan(99.90);
        result.HydrothermalProcessControlGuidance.Should().ContainEquivalentOf("Refractory PFAS destroyed beyond 99.99%");
    }

    [Fact]
    public void AnalyzeScwoProcess_SubCriticalPressure_ReturnsSubCriticalCollapse()
    {
        var telemetry = new ScwoTelemetry(
            ReactorId: "SCWO-REACTOR-02",
            ReactorCoreTemperatureCelsius: 372.0, // Below 380 C
            ReactorOperatingPressureMegaPascals: 19.5, // Below 22.5 MPa
            ResidenceTimeSeconds: 50.0,
            DissolvedOxygenStoichiometricRatio: 1.2,
            InfluentTotalPfasConcentrationPpm: 40.0,
            InorganicSaltPrecipitationPluggingRisk: 0.20,
            TranspiringWallWaterFlowRateLitersPerMin: 10.0
        );

        var result = _engine.AnalyzeScwoProcess(telemetry);

        result.ScwoDestructionRegime.Should().Be("SUB_CRITICAL_COLLAPSE_INCOMPLETE_OXIDATION");
        result.IsDestructionComplete.Should().BeFalse();
        result.HydrothermalProcessControlGuidance.Should().ContainEquivalentOf("sub-critical thermodynamic regime");
    }

    [Fact]
    public void AnalyzeScwoProcess_SaltPluggingRisk_ReturnsPluggingHazard()
    {
        var telemetry = new ScwoTelemetry(
            ReactorId: "SCWO-REACTOR-03",
            ReactorCoreTemperatureCelsius: 540.0,
            ReactorOperatingPressureMegaPascals: 25.0,
            ResidenceTimeSeconds: 60.0,
            DissolvedOxygenStoichiometricRatio: 1.3,
            InfluentTotalPfasConcentrationPpm: 60.0,
            InorganicSaltPrecipitationPluggingRisk: 0.82, // Exceeds 0.70
            TranspiringWallWaterFlowRateLitersPerMin: 2.0 // Low sheath water
        );

        var result = _engine.AnalyzeScwoProcess(telemetry);

        result.ScwoDestructionRegime.Should().Be("HYDROTHERMAL_SALT_PLUGGING_HAZARD");
        result.IsDestructionComplete.Should().BeFalse();
        result.HydrothermalProcessControlGuidance.Should().ContainEquivalentOf("transpiring-wall supercritical sheath water");
    }

    [Fact]
    public void AnalyzeScwoProcess_LowOxygenAndResidence_ReturnsDefluorinationShortfall()
    {
        var telemetry = new ScwoTelemetry(
            ReactorId: "SCWO-REACTOR-04",
            ReactorCoreTemperatureCelsius: 410.0,
            ReactorOperatingPressureMegaPascals: 23.5,
            ResidenceTimeSeconds: 15.0, // Low residence time
            DissolvedOxygenStoichiometricRatio: 0.35, // Low oxygen
            InfluentTotalPfasConcentrationPpm: 45.0,
            InorganicSaltPrecipitationPluggingRisk: 0.25,
            TranspiringWallWaterFlowRateLitersPerMin: 10.0
        );

        var result = _engine.AnalyzeScwoProcess(telemetry);

        result.ScwoDestructionRegime.Should().Be("PFAS_DEFLUORINATION_SHORTFALL");
        result.IsDestructionComplete.Should().BeFalse();
        result.PfasDefluorinationEfficiencyPercent.Should().BeLessThan(99.90);
        result.HydrothermalProcessControlGuidance.Should().ContainEquivalentOf("Defluorination efficiency below regulatory");
    }

    [Fact]
    public void AnalyzeScwoProcess_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeScwoProcess(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
