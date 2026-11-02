using SGrowing.Api.Modules.Quantum.RareEarthOpticalMemory;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class RareEarthOpticalMemoryTests
{
    private readonly RareEarthMemoryEngine _engine = new();

    [Fact]
    public void AnalyzeQuantumMemory_NominalStorage_ReturnsRepeaterReady()
    {
        var telemetry = new RareEarthMemoryTelemetry
        {
            CrystalId = "CRYSTAL-EU-001",
            DopedIonType = "Eu3+:Y2SiO5",
            CryogenicTemperatureKelvin = 2.0,
            OpticalDepth = 4.2,
            UnburnedBackgroundAbsorptionDepth = 0.05,
            CombToothSpacingMhz = 1.0,
            CombFinesse = 4.5,
            ToothJitterPercent = 2.0,
            OpticalHomogeneousLinewidthKhz = 2.0,
            SpinWaveStorageTimeSeconds = 2.0,
            InputSinglePhotonSnr = 30.0
        };

        var result = _engine.AnalyzeQuantumMemory(telemetry);

        Assert.Equal("CRYSTAL-EU-001", result.CrystalId);
        Assert.True(result.IsQuantumRepeaterReady);
        Assert.True(result.IsFinesseOptimal);
        Assert.True(result.IsBackgroundLossSuppressed);
        Assert.True(result.IsCryogenicTemperatureSafe);
        Assert.True(result.CalculatedAfcEfficiencyPercent >= 10.0);
        Assert.True(result.OutputPhotonFidelityPercent >= 92.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeQuantumMemory_LowCombFinesse_TriggersFinesseDeficit()
    {
        var telemetry = new RareEarthMemoryTelemetry
        {
            CrystalId = "CRYSTAL-PR-DEFICIT",
            CombFinesse = 2.2, // Below 3.5 threshold
            ToothJitterPercent = 8.5
        };

        var result = _engine.AnalyzeQuantumMemory(telemetry);

        Assert.False(result.IsFinesseOptimal);
        Assert.False(result.IsQuantumRepeaterReady);
        Assert.Contains("COMB_FINESSE_DEFICIT_DEPHASING", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeQuantumMemory_HighUnburnedBackground_TriggersAbsorptionLoss()
    {
        var telemetry = new RareEarthMemoryTelemetry
        {
            CrystalId = "CRYSTAL-BG-LOSS",
            UnburnedBackgroundAbsorptionDepth = 0.45 // Exceeds 0.20
        };

        var result = _engine.AnalyzeQuantumMemory(telemetry);

        Assert.False(result.IsBackgroundLossSuppressed);
        Assert.False(result.IsQuantumRepeaterReady);
        Assert.Contains("UNBURNED_BACKGROUND_ABSORPTION_LOSS", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeQuantumMemory_ElevatedCryogenicTemp_TriggersPhononDephasing()
    {
        var telemetry = new RareEarthMemoryTelemetry
        {
            CrystalId = "CRYSTAL-THERMAL",
            CryogenicTemperatureKelvin = 4.5 // Exceeds 3.5 K
        };

        var result = _engine.AnalyzeQuantumMemory(telemetry);

        Assert.False(result.IsCryogenicTemperatureSafe);
        Assert.Contains("CRYOGENIC_TEMPERATURE_THERMAL_PHONON_DEPHASING", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeQuantumMemory_SpinWaveRetention_DecaysWithStorageTime()
    {
        var telemetryPrShort = new RareEarthMemoryTelemetry
        {
            CrystalId = "CRYSTAL-PR-SHORT",
            DopedIonType = "Pr3+:Y2SiO5",
            SpinWaveStorageTimeSeconds = 10.0
        };

        var telemetryPrLong = new RareEarthMemoryTelemetry
        {
            CrystalId = "CRYSTAL-PR-LONG",
            DopedIonType = "Pr3+:Y2SiO5",
            SpinWaveStorageTimeSeconds = 120.0
        };

        var resShort = _engine.AnalyzeQuantumMemory(telemetryPrShort);
        var resLong = _engine.AnalyzeQuantumMemory(telemetryPrLong);

        Assert.True(resShort.SpinWaveRetentionPercent > resLong.SpinWaveRetentionPercent);
    }
}
