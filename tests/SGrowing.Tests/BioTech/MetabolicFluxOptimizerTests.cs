namespace SGrowing.Tests.BioTech;

using FluentAssertions;
using SGrowing.Api.Modules.BioTech.MetabolicFluxOptimizer;
using Xunit;

public class MetabolicFluxOptimizerTests
{
    private readonly MetabolicFluxOptimizerEngine _engine = new();

    [Fact]
    public void OptimizeFedBatch_BalancedExponentialGrowth_CalculatesFeedRateAndPositiveGrowth()
    {
        var telemetry = new BioreactorTelemetry(
            VesselId: "BIO-FERM-01",
            BiomassConcentrationGPerL: 15.0,
            GlucoseConcentrationGPerL: 0.80, // Healthy fed-batch level
            DissolvedOxygenPercent: 35.0,
            TemperatureCelsius: 37.0,
            Ph: 7.0,
            CultivationHours: 18.0
        );

        var request = new FedBatchOptimizationRequest(telemetry, CurrentVesselVolumeLiters: 100.0);
        var result = _engine.OptimizeFedBatch(request);

        result.VesselId.Should().Be("BIO-FERM-01");
        result.CurrentGrowthRateMu.Should().BeGreaterThan(0.20);
        result.OptimalFeedRateLitersPerHour.Should().BeGreaterThan(0.0);
        result.OxygenEnrichmentPercent.Should().Be(0.0); // dO2 is 35% > 30%
        result.PredictedBiomassAccumulationGPerL.Should().BeGreaterThan(15.0);
        result.GrowthPhase.Should().Be("Exponential Growth Phase");
        result.MetabolicWarning.Should().Contain("Metabolic Equilibrium");
    }

    [Fact]
    public void OptimizeFedBatch_HighGlucoseOverflow_DetectsCrabtreeEffectWarning()
    {
        var telemetry = new BioreactorTelemetry(
            VesselId: "BIO-CRABTREE-02",
            BiomassConcentrationGPerL: 8.0,
            GlucoseConcentrationGPerL: 12.5, // High glucose surge (>5.0 g/L)
            DissolvedOxygenPercent: 28.0,
            TemperatureCelsius: 37.0,
            Ph: 6.8,
            CultivationHours: 6.0
        );

        var request = new FedBatchOptimizationRequest(telemetry);
        var result = _engine.OptimizeFedBatch(request);

        result.GrowthPhase.Should().Be("Substrate Overload");
        result.MetabolicWarning.Should().Contain("Crabtree Effect Alert");
    }

    [Fact]
    public void OptimizeFedBatch_DepletedOxygen_RampsOxygenEnrichmentAndWarnsAnoxia()
    {
        var telemetry = new BioreactorTelemetry(
            VesselId: "BIO-ANOXIA-03",
            BiomassConcentrationGPerL: 40.0,
            GlucoseConcentrationGPerL: 0.50,
            DissolvedOxygenPercent: 10.0, // Anoxic strain (<15%)
            TemperatureCelsius: 37.0,
            Ph: 6.9,
            CultivationHours: 24.0
        );

        var request = new FedBatchOptimizationRequest(telemetry);
        var result = _engine.OptimizeFedBatch(request);

        result.OxygenEnrichmentPercent.Should().BeGreaterThan(25.0);
        result.GrowthPhase.Should().Be("Micro-Aerobic Strain");
        result.MetabolicWarning.Should().Contain("Anoxia Warning");
    }

    [Fact]
    public void OptimizeFedBatch_GlucoseDepleted_ClassifiesSubstrateStarvation()
    {
        var telemetry = new BioreactorTelemetry(
            VesselId: "BIO-STARVE-04",
            BiomassConcentrationGPerL: 20.0,
            GlucoseConcentrationGPerL: 0.01, // Depleted (<0.05 g/L)
            DissolvedOxygenPercent: 50.0,
            TemperatureCelsius: 37.0,
            Ph: 7.1,
            CultivationHours: 12.0
        );

        var request = new FedBatchOptimizationRequest(telemetry);
        var result = _engine.OptimizeFedBatch(request);

        result.GrowthPhase.Should().Be("Substrate Starvation");
        result.MetabolicWarning.Should().Contain("Carbon Starvation");
    }
}
