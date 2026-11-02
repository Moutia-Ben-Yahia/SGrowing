using System;
using SGrowing.Api.Modules.Energy.CompressedGasGeothermalEnergyStorage;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class CompressedGasGeothermalEnergyStorageTests
{
    private readonly CompressedGasGeothermalEnergyStorageEngine _engine = new();

    [Fact]
    public void AnalyzeGeothermalStorage_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeGeothermalStorage(null!));
    }

    [Fact]
    public void AnalyzeGeothermalStorage_NegativeDepth_ThrowsArgumentException()
    {
        var telemetry = new CompressedGasGeothermalTelemetry(
            StorageAquiferWellId: "GEO-WELL-01",
            AquiferDepthMeters: -1500.0, // Negative!
            AmbientGeothermalTemperatureCelsius: 110.0,
            InjectedFluidTemperatureCelsius: 40.0,
            InjectionCompressorPowerMegawatts: 20.0,
            ExpansionTurbinePowerMegawatts: 25.0,
            CurrentAquiferPorePressureBar: 180.0,
            FormationFracturePressureBar: 280.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeGeothermalStorage(telemetry));
    }

    [Fact]
    public void AnalyzeGeothermalStorage_ExergySurplus_ReturnsExergySurplusRegime()
    {
        // Compressor: 20 MW, Turbine: 26 MW -> Round-trip = 130% >= 115%
        // Fracture: 300 bar, Pore: 210 bar -> margin = 30% >= 20%
        var telemetry = new CompressedGasGeothermalTelemetry(
            StorageAquiferWellId: "CGES-PARIS-BASIN-AQUIFER",
            AquiferDepthMeters: 2200.0,
            AmbientGeothermalTemperatureCelsius: 120.0,
            InjectedFluidTemperatureCelsius: 45.0,
            InjectionCompressorPowerMegawatts: 20.0,
            ExpansionTurbinePowerMegawatts: 26.0,
            CurrentAquiferPorePressureBar: 210.0,
            FormationFracturePressureBar: 300.0
        );

        var result = _engine.AnalyzeGeothermalStorage(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CGES-PARIS-BASIN-AQUIFER", result.StorageAquiferWellId);
        Assert.True(result.RoundTripExergyEfficiencyPercent >= 115.0);
        Assert.True(result.CaprockFractureSafetyMarginPercent >= 20.0);
        Assert.True(result.IsSubsurfaceStorageSafe);
        Assert.Equal("OPTIMAL_GEOTHERMALLY_ENHANCED_EXERGY_SURPLUS_REGIME", result.GeoStorageRegime);
        Assert.Contains("Harvesting deep geothermal heat surplus", result.SubsurfaceReservoirGuidance);
    }

    [Fact]
    public void AnalyzeGeothermalStorage_HydrofracturePressure_ReturnsHydrofractureHazard()
    {
        // Fracture: 250 bar, Pore: 235 bar -> 235 >= 250 * 0.90 = 225 bar
        var telemetry = new CompressedGasGeothermalTelemetry(
            StorageAquiferWellId: "CGES-OVERPRESSURE-02",
            AquiferDepthMeters: 1800.0,
            AmbientGeothermalTemperatureCelsius: 100.0,
            InjectedFluidTemperatureCelsius: 40.0,
            InjectionCompressorPowerMegawatts: 25.0,
            ExpansionTurbinePowerMegawatts: 30.0,
            CurrentAquiferPorePressureBar: 235.0,
            FormationFracturePressureBar: 250.0
        );

        var result = _engine.AnalyzeGeothermalStorage(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CRITICAL_CAPROCK_HYDROFRACTURE_PRESSURE_HAZARD", result.GeoStorageRegime);
        Assert.False(result.IsSubsurfaceStorageSafe);
        Assert.Contains("throttle back surface compressor", result.SubsurfaceReservoirGuidance);
    }

    [Fact]
    public void AnalyzeGeothermalStorage_ColdAquifer_ReturnsInsufficientHeatGradient()
    {
        var telemetry = new CompressedGasGeothermalTelemetry(
            StorageAquiferWellId: "CGES-SHALLOW-COLD-03",
            AquiferDepthMeters: 600.0,
            AmbientGeothermalTemperatureCelsius: 45.0, // < 65 C!
            InjectedFluidTemperatureCelsius: 35.0,
            InjectionCompressorPowerMegawatts: 10.0,
            ExpansionTurbinePowerMegawatts: 8.0,
            CurrentAquiferPorePressureBar: 60.0,
            FormationFracturePressureBar: 120.0
        );

        var result = _engine.AnalyzeGeothermalStorage(telemetry);

        Assert.NotNull(result);
        Assert.Equal("INSUFFICIENT_GEOTHERMAL_HEAT_GRADIENT", result.GeoStorageRegime);
        Assert.False(result.IsSubsurfaceStorageSafe);
        Assert.Contains("inadequate to achieve thermodynamic exergy surplus", result.SubsurfaceReservoirGuidance);
    }
}
