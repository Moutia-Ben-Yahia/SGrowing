using System;
using SGrowing.Api.Modules.Energy.HempSubstationProtection;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class HempSubstationProtectionTests
{
    private readonly HempSubstationProtectionEngine _engine = new();

    [Fact]
    public void AnalyzeHempThreat_NormalGridOperation_MaintainsContinuousMonitoring()
    {
        var telemetry = new SubstationHempTelemetry(
            SubstationId: "SUBSTATION-PJM-500KV-01",
            GridNominalVoltageKv: 500.0,
            MeasuredDvDtKilovoltsPerNanosecond: 0.25,
            PeakSurgeVoltageKilovolts: 5.0,
            NeutralGeomagneticallyInducedCurrentAmperes: 3.2,
            TransformerReactivePowerSurgeMvar: 4.5,
            TransformerTopOilTempCelsius: 65.0,
            FastInterceptionMovConductingCurrentAmperes: 0.0,
            DcBlockingCapacitorEngaged: false,
            FaradayShieldIntegrityPercent: 99.5);

        var result = _engine.AnalyzeHempThreat(telemetry);

        Assert.Equal("GRID_SUBSTATION_HARDENED_NORMAL", result.HempTransientThreatLevel);
        Assert.Equal("MAINTAIN_CONTINUOUS_OPTICAL_GIC_MONITORING", result.FastInterceptionActionRecommendation);
        Assert.True(result.IsSubstationProtected);
        Assert.True(result.EstimatedCoreTemperatureRiseRateCelsiusPerMinute < 0.1);
    }

    [Fact]
    public void AnalyzeHempThreat_E1NanosecondFastTransient_InterceptsAndIsolatesRelays()
    {
        // dv/dt = 18.0 kV/ns >= 10.0 kV/ns, Peak = 48.0 kV >= 40.0 kV
        var telemetry = new SubstationHempTelemetry(
            SubstationId: "SUBSTATION-PJM-500KV-02",
            GridNominalVoltageKv: 500.0,
            MeasuredDvDtKilovoltsPerNanosecond: 18.0,
            PeakSurgeVoltageKilovolts: 48.0,
            NeutralGeomagneticallyInducedCurrentAmperes: 5.0,
            TransformerReactivePowerSurgeMvar: 8.0,
            TransformerTopOilTempCelsius: 68.0,
            FastInterceptionMovConductingCurrentAmperes: 2400.0,
            DcBlockingCapacitorEngaged: false,
            FaradayShieldIntegrityPercent: 98.0);

        var result = _engine.AnalyzeHempThreat(telemetry);

        Assert.Equal("E1_NANOSECOND_SURGE_INTERCEPTED", result.HempTransientThreatLevel);
        Assert.Equal("ISOLATE_MICROPROCESSOR_RELAYS_AND_ACTIVATE_FARADAY_SHIELD_CONDUIT", result.FastInterceptionActionRecommendation);
        Assert.True(result.IsSubstationProtected);
        Assert.True(result.ClampedResidualVoltageKilovolts < 3.0);
    }

    [Fact]
    public void AnalyzeHempThreat_E3GicCoreSaturationUnblocked_TriggersBlockingCapacitorAlarm()
    {
        // GIC = 52.0 A >= 35.0 A, Capacitor not engaged -> Unprotected
        var telemetry = new SubstationHempTelemetry(
            SubstationId: "SUBSTATION-PJM-500KV-03",
            GridNominalVoltageKv: 500.0,
            MeasuredDvDtKilovoltsPerNanosecond: 1.5,
            PeakSurgeVoltageKilovolts: 12.0,
            NeutralGeomagneticallyInducedCurrentAmperes: 52.0,
            TransformerReactivePowerSurgeMvar: 58.0,
            TransformerTopOilTempCelsius: 82.0,
            FastInterceptionMovConductingCurrentAmperes: 0.0,
            DcBlockingCapacitorEngaged: false,
            FaradayShieldIntegrityPercent: 99.0);

        var result = _engine.AnalyzeHempThreat(telemetry);

        Assert.Equal("E3_CORE_SATURATION_GIC_ALARM", result.HempTransientThreatLevel);
        Assert.Equal("INSERT_NEUTRAL_GROUNDING_DC_BLOCKING_CAPACITOR_BANK", result.FastInterceptionActionRecommendation);
        Assert.False(result.IsSubstationProtected);
        Assert.True(result.EstimatedCoreTemperatureRiseRateCelsiusPerMinute > 2.0);
    }

    [Fact]
    public void AnalyzeHempThreat_E3GicCoreSaturationEngagedCapacitor_ConfirmsProtection()
    {
        // GIC = 52.0 A >= 35.0 A, Capacitor engaged -> Protected
        var telemetry = new SubstationHempTelemetry(
            SubstationId: "SUBSTATION-PJM-500KV-04",
            GridNominalVoltageKv: 500.0,
            MeasuredDvDtKilovoltsPerNanosecond: 1.5,
            PeakSurgeVoltageKilovolts: 12.0,
            NeutralGeomagneticallyInducedCurrentAmperes: 52.0,
            TransformerReactivePowerSurgeMvar: 58.0,
            TransformerTopOilTempCelsius: 82.0,
            FastInterceptionMovConductingCurrentAmperes: 0.0,
            DcBlockingCapacitorEngaged: true,
            FaradayShieldIntegrityPercent: 99.0);

        var result = _engine.AnalyzeHempThreat(telemetry);

        Assert.Equal("E3_CORE_SATURATION_GIC_ALARM", result.HempTransientThreatLevel);
        Assert.True(result.IsSubstationProtected);
    }
}
