namespace SGrowing.Tests.Sustainability;

using FluentAssertions;
using SGrowing.Api.Modules.Sustainability.MineralCarbonation;
using Xunit;

public class MineralCarbonationTests
{
    private readonly MineralCarbonationReactorEngine _engine = new();

    [Fact]
    public void ProcessMineralCarbonation_HighFinenessOlivine_AchievesHighEfficiencyAndGeneratesCredits()
    {
        var feedstock = new MineralFeedstock("FEED-OLIVINE-01", "Dunite Olivine", MassTons: 100.0, ReactiveOxideFraction: 0.45, ParticleSizeMicrons: 35.0);
        var flueGas = new FlueGasStream("CementPlantKiln-01", 5000.0, 18.0, 160.0, 1.2);
        var parameters = new ReactorOperatingParameters(ReactorTemperatureCelsius: 165.0, PartialPressureCo2Bar: 30.0, ResidenceTimeHours: 2.5);

        var request = new MineralCarbonationBatchRequest("BATCH-MC-001", feedstock, flueGas, parameters);
        var result = _engine.ProcessMineralCarbonation(request);

        result.BatchId.Should().Be("BATCH-MC-001");
        result.CarbonationEfficiencyPercent.Should().BeGreaterThan(70.0);
        result.Co2SequesteredTons.Should().BeGreaterThan(30.0);
        result.NetExothermicHeatRecoveryMwh.Should().BeGreaterThan(15.0);
        result.CarbonCreditsGenerated.Should().BeGreaterThan(30.0);
        result.RegulatoryCertificationStatus.Should().Contain("ISO 14064-2 Compliant");
    }

    [Fact]
    public void ProcessMineralCarbonation_CoarseGravel_ReducesEfficiencyDueToParticleSize()
    {
        // 500 micron coarse particles -> low reactive surface area
        var coarseFeedstock = new MineralFeedstock("FEED-COARSE-02", "Basalt Quarry Fines", 100.0, 0.25, ParticleSizeMicrons: 500.0);
        var flueGas = new FlueGasStream("SteelMillBlastFurnace", 4000.0, 22.0, 200.0, 1.0);
        var lowPressureParams = new ReactorOperatingParameters(ReactorTemperatureCelsius: 110.0, PartialPressureCo2Bar: 5.0);

        var request = new MineralCarbonationBatchRequest("BATCH-COARSE", coarseFeedstock, flueGas, lowPressureParams);
        var result = _engine.ProcessMineralCarbonation(request);

        result.CarbonationEfficiencyPercent.Should().BeLessThan(70.0);
        result.RegulatoryCertificationStatus.Should().Contain("Conditional Certification");
    }

    [Fact]
    public void ProcessMineralCarbonation_ZeroFeedstockMass_ReturnsZeroSequesteredCo2()
    {
        var emptyFeedstock = new MineralFeedstock("FEED-EMPTY", "Olivine", 0.0, 0.40, 50.0);
        var flueGas = new FlueGasStream("PlantA", 1000.0, 10.0, 100.0, 1.0);
        var parameters = new ReactorOperatingParameters();

        var request = new MineralCarbonationBatchRequest("BATCH-ZERO", emptyFeedstock, flueGas, parameters);
        var result = _engine.ProcessMineralCarbonation(request);

        result.Co2SequesteredTons.Should().Be(0.0);
        result.NetExothermicHeatRecoveryMwh.Should().Be(0.0);
        result.CarbonCreditsGenerated.Should().Be(0.0);
    }

    [Fact]
    public void ProcessMineralCarbonation_HighPressureSupercriticalCo2_MaximizesEfficiencyCap()
    {
        var feedstock = new MineralFeedstock("FEED-WOLLASTONITE", "Wollastonite", 50.0, 0.40, 20.0);
        var flueGas = new FlueGasStream("DirectAirCapture", 2000.0, 95.0, 150.0, 40.0);
        var extremeParams = new ReactorOperatingParameters(180.0, PartialPressureCo2Bar: 50.0);

        var request = new MineralCarbonationBatchRequest("BATCH-SUPERCRIT", feedstock, flueGas, extremeParams);
        var result = _engine.ProcessMineralCarbonation(request);

        result.CarbonationEfficiencyPercent.Should().BeApproximately(96.0, 0.1);
    }
}
