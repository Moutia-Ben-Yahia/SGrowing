using System;
using SGrowing.Api.Modules.Energy.MoltenSaltCspNitrateDecomposition;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class MoltenSaltCspDecompositionTests
{
    private readonly MoltenSaltCspDecompositionEngine _engine = new();

    [Fact]
    public void AnalyzeMoltenSaltStorage_OptimalSolarSalt_ReturnsStorageStable()
    {
        var telemetry = new MoltenSaltCspTelemetry
        {
            StorageTankId = "CSP-SALT-TANK-01",
            SaltMixtureComposition = "Solar-Salt-60NaNO3-40KNO3",
            HotTankTemperatureCelsius = 555.0,
            ColdTankTemperatureCelsius = 290.0,
            NitriteIonConcentrationWeightPercent = 4.0,
            OxideIonConcentrationPpm = 15.0,
            UllageCoverGasOxygenPartialPressureAtm = 0.21,
            ContainmentAlloyCorrosionRateMicronsPerYear = 8.0,
            FreezingPointCelsius = 220.0
        };

        var result = _engine.AnalyzeMoltenSaltStorage(telemetry);

        Assert.Equal("CSP-SALT-TANK-01", result.StorageTankId);
        Assert.True(result.IsFreezeMarginSafe);
        Assert.True(result.IsThermalDecompositionControlled);
        Assert.True(result.IsTankCorrosionWithinLimit);
        Assert.True(result.ColdTankFreezeSafetyMarginCelsius >= 40.0);
        Assert.True(result.ProjectedCorrosionRateMicronsPerYear <= 15.0);
        Assert.Equal("OPTIMAL_MOLTEN_SALT_THERMAL_STORAGE_STABLE", result.MoltenSaltThermalRegime);
    }

    [Fact]
    public void AnalyzeMoltenSaltStorage_LowColdTemperature_TriggersFreezePluggingRisk()
    {
        var telemetry = new MoltenSaltCspTelemetry
        {
            StorageTankId = "CSP-FREEZE-RISK-02",
            HotTankTemperatureCelsius = 550.0,
            ColdTankTemperatureCelsius = 245.0, // Only 25 deg C margin over 220 C freeze
            NitriteIonConcentrationWeightPercent = 3.5,
            OxideIonConcentrationPpm = 10.0,
            FreezingPointCelsius = 220.0
        };

        var result = _engine.AnalyzeMoltenSaltStorage(telemetry);

        Assert.False(result.IsFreezeMarginSafe);
        Assert.Equal("COLD_TANK_SALT_FREEZE_PLUGGING_RISK", result.MoltenSaltThermalRegime);
        Assert.Contains("electrical heat tracing", result.StorageOperationProtocol);
    }

    [Fact]
    public void AnalyzeMoltenSaltStorage_ExtremeOverTemperature_TriggersRunawayDecomposition()
    {
        var telemetry = new MoltenSaltCspTelemetry
        {
            StorageTankId = "CSP-HOT-RUNAWAY-03",
            HotTankTemperatureCelsius = 595.0, // Extreme over-temp > 585 C
            ColdTankTemperatureCelsius = 290.0,
            NitriteIonConcentrationWeightPercent = 5.0,
            OxideIonConcentrationPpm = 20.0
        };

        var result = _engine.AnalyzeMoltenSaltStorage(telemetry);

        Assert.False(result.IsThermalDecompositionControlled);
        Assert.Equal("RUNAWAY_SECONDARY_DECOMPOSITION_NOX_VENTING", result.MoltenSaltThermalRegime);
        Assert.Contains("Quench solar receiver flux", result.StorageOperationProtocol);
    }

    [Fact]
    public void AnalyzeMoltenSaltStorage_HighOxideContent_TriggersAcceleratedCorrosion()
    {
        var telemetry = new MoltenSaltCspTelemetry
        {
            StorageTankId = "CSP-OXIDE-CORRODE-04",
            HotTankTemperatureCelsius = 565.0,
            ColdTankTemperatureCelsius = 290.0,
            NitriteIonConcentrationWeightPercent = 4.5,
            OxideIonConcentrationPpm = 120.0 // Heavy oxide build-up > 50 ppm
        };

        var result = _engine.AnalyzeMoltenSaltStorage(telemetry);

        Assert.False(result.IsTankCorrosionWithinLimit);
        Assert.Equal("ACCELERATED_OXIDE_FLUXING_TANK_CORROSION", result.MoltenSaltThermalRegime);
        Assert.Contains("magnesium salt chemical scavengers", result.StorageOperationProtocol);
    }

    [Fact]
    public void AnalyzeMoltenSaltStorage_ZeroOrNegativeTemperature_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeMoltenSaltStorage(null!));

        var invalidTelemetry = new MoltenSaltCspTelemetry
        {
            StorageTankId = "INV-CSP",
            HotTankTemperatureCelsius = 0.0,
            ColdTankTemperatureCelsius = 290.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeMoltenSaltStorage(invalidTelemetry));
    }
}
