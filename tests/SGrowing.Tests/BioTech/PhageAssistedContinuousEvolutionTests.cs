namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.PhageAssistedContinuousEvolution;
using Xunit;

public class PhageAssistedContinuousEvolutionTests
{
    private readonly PhageAssistedContinuousEvolutionEngine _engine = new();

    [Fact]
    public void AnalyzeContinuousEvolution_NominalParameters_ReturnsSteadyStateEvolution()
    {
        var telemetry = new PaceTelemetry(
            LagoonId: "LAGOON-EVO-01",
            DilutionRatePerHour: 1.6,
            PhageReplicationRatePerHour: 2.8,
            PhageTiterPfuPerMilliliter: 4.5e8,
            HostCellDensityOd600: 0.85,
            HostCellSurvivalRatePercent: 88.0,
            MutagenesisInducerConcentrationMicroMolar: 75.0,
            SelectionStringencyScore: 2.5,
            CumulativeGenerationsElapsed: 120.0
        );

        var result = _engine.AnalyzeContinuousEvolution(telemetry);

        result.LagoonId.Should().Be("LAGOON-EVO-01");
        result.EvolutionRegime.Should().Be("CONTINUOUS_STEADY_STATE_DIRECTED_EVOLUTION");
        result.IsEvolutionOptimal.Should().BeTrue();
        result.NetPopulationGrowthPerHour.Should().BeApproximately(1.2, 0.05);
        result.EstimatedMutationsPerKilobase.Should().BeGreaterThan(0.5);
        result.EvolutionaryAdaptationVelocityScore.Should().BeGreaterThan(2.0);
        result.BioreactorActionGuidance.Should().ContainEquivalentOf("steady-state evolutionary trajectory");
    }

    [Fact]
    public void AnalyzeContinuousEvolution_DilutionExceedsReplication_ReturnsWashoutRisk()
    {
        var telemetry = new PaceTelemetry(
            LagoonId: "LAGOON-EVO-02",
            DilutionRatePerHour: 3.2, // Exceeds 2.1
            PhageReplicationRatePerHour: 2.1,
            PhageTiterPfuPerMilliliter: 1.0e7,
            HostCellDensityOd600: 0.70,
            HostCellSurvivalRatePercent: 85.0,
            MutagenesisInducerConcentrationMicroMolar: 60.0,
            SelectionStringencyScore: 1.5,
            CumulativeGenerationsElapsed: 45.0
        );

        var result = _engine.AnalyzeContinuousEvolution(telemetry);

        result.EvolutionRegime.Should().Be("LAGOON_WASHOUT_EXTINCTION_RISK");
        result.IsEvolutionOptimal.Should().BeFalse();
        result.NetPopulationGrowthPerHour.Should().BeNegative();
        result.BioreactorActionGuidance.Should().ContainEquivalentOf("viral population washout");
    }

    [Fact]
    public void AnalyzeContinuousEvolution_ExcessiveInducerToxicity_ReturnsHostToxicity()
    {
        var telemetry = new PaceTelemetry(
            LagoonId: "LAGOON-EVO-03",
            DilutionRatePerHour: 1.2,
            PhageReplicationRatePerHour: 2.2,
            PhageTiterPfuPerMilliliter: 2.0e7,
            HostCellDensityOd600: 0.40,
            HostCellSurvivalRatePercent: 48.0, // Low survival < 60%
            MutagenesisInducerConcentrationMicroMolar: 320.0, // High inducer > 250 uM
            SelectionStringencyScore: 2.0,
            CumulativeGenerationsElapsed: 80.0
        );

        var result = _engine.AnalyzeContinuousEvolution(telemetry);

        result.EvolutionRegime.Should().Be("HOST_MUTAGENIC_BURDEN_TOXICITY");
        result.IsEvolutionOptimal.Should().BeFalse();
        result.BioreactorActionGuidance.Should().ContainEquivalentOf("MP6) overexpression");
    }

    [Fact]
    public void AnalyzeContinuousEvolution_LowPhageTiterCollapse_ReturnsExcessiveStringency()
    {
        var telemetry = new PaceTelemetry(
            LagoonId: "LAGOON-EVO-04",
            DilutionRatePerHour: 1.4,
            PhageReplicationRatePerHour: 1.6,
            PhageTiterPfuPerMilliliter: 4.0e4, // Critical low titer < 1e5
            HostCellDensityOd600: 0.90,
            HostCellSurvivalRatePercent: 92.0,
            MutagenesisInducerConcentrationMicroMolar: 50.0,
            SelectionStringencyScore: 8.5, // Harsh selection
            CumulativeGenerationsElapsed: 60.0
        );

        var result = _engine.AnalyzeContinuousEvolution(telemetry);

        result.EvolutionRegime.Should().Be("EXCESSIVE_SELECTION_STRINGENCY_COLLAPSE");
        result.IsEvolutionOptimal.Should().BeFalse();
        result.BioreactorActionGuidance.Should().ContainEquivalentOf("promoter stringency");
    }

    [Fact]
    public void AnalyzeContinuousEvolution_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeContinuousEvolution(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
