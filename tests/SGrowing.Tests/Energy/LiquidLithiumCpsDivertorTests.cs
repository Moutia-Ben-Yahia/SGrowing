using FluentAssertions;
using SGrowing.Api.Modules.Energy.LiquidLithiumCpsDivertor;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class LiquidLithiumCpsDivertorTests
{
    private readonly LiquidLithiumCpsDivertorEngine _engine = new();

    [Fact]
    public void AnalyzeLiquidMetalDivertor_HighHeatFlux_ReturnsVaporShieldingOptimal()
    {
        var telemetry = new LiquidLithiumDivertorTelemetry(
            DivertorCassetteId: "DIVERTOR-SPARC-LITHIUM-01",
            PeakIncidentPlasmaHeatFluxMwPerM2: 18.5,
            LithiumSurfaceTemperatureCelsius: 580.0,
            MeshPoreRadiusMicrons: 25.0,
            CapillaryReplenishmentPressureKPa: 32.0,
            TransverseMagneticFieldTesla: 5.0,
            CorePlasmaEffectiveChargeZeff: 1.15,
            ThermoelectricMhdReplenishmentPumpActive: true
        );

        var result = _engine.AnalyzeLiquidMetalDivertor(telemetry);

        result.DivertorProtectionRegime.Should().Be("VAPOR_SHIELDING_REGULATED_OPTIMAL");
        result.IsDivertorTargetProtected.Should().BeTrue();
        result.VaporShieldingAttenuationRatio.Should().BeGreaterThan(0.40);
        result.NetTargetHeatFluxMwPerM2.Should().BeLessThan(18.5);
        result.CapillaryToMhdHeadSafetyMargin.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void AnalyzeLiquidMetalDivertor_HighZeffContamination_ReturnsContaminationAlert()
    {
        var telemetry = new LiquidLithiumDivertorTelemetry(
            DivertorCassetteId: "DIVERTOR-CONTAMINATED-02",
            PeakIncidentPlasmaHeatFluxMwPerM2: 22.0,
            LithiumSurfaceTemperatureCelsius: 750.0, // High temperature > 720C
            MeshPoreRadiusMicrons: 25.0,
            CapillaryReplenishmentPressureKPa: 35.0,
            TransverseMagneticFieldTesla: 5.5,
            CorePlasmaEffectiveChargeZeff: 1.48, // Core dilution > 1.35
            ThermoelectricMhdReplenishmentPumpActive: true
        );

        var result = _engine.AnalyzeLiquidMetalDivertor(telemetry);

        result.DivertorProtectionRegime.Should().Be("CORE_PLASMA_LITHIUM_CONTAMINATION_ALERT");
        result.IsDivertorTargetProtected.Should().BeFalse();
        result.DivertorCoolingCommand.Should().Contain("Core plasma effective charge Zeff");
    }

    [Fact]
    public void AnalyzeLiquidMetalDivertor_PumpOfflineLowCapillaryHead_ReturnsDryoutHazard()
    {
        var telemetry = new LiquidLithiumDivertorTelemetry(
            DivertorCassetteId: "DIVERTOR-DRYOUT-03",
            PeakIncidentPlasmaHeatFluxMwPerM2: 15.0,
            LithiumSurfaceTemperatureCelsius: 540.0,
            MeshPoreRadiusMicrons: 45.0,
            CapillaryReplenishmentPressureKPa: 12.0, // Low pressure -> margin = 12 / 21 = 0.57 < 1.0
            TransverseMagneticFieldTesla: 5.0,
            CorePlasmaEffectiveChargeZeff: 1.20,
            ThermoelectricMhdReplenishmentPumpActive: false // Pump offline
        );

        var result = _engine.AnalyzeLiquidMetalDivertor(telemetry);

        result.DivertorProtectionRegime.Should().Be("CAPILLARY_DRYOUT_MELTDOWN_HAZARD");
        result.IsDivertorTargetProtected.Should().BeFalse();
        result.DivertorCoolingCommand.Should().Contain("Capillary replenishment head below MHD drag");
    }

    [Fact]
    public void AnalyzeLiquidMetalDivertor_LowHeatFlux_ReturnsSubVaporizationConvective()
    {
        var telemetry = new LiquidLithiumDivertorTelemetry(
            DivertorCassetteId: "DIVERTOR-CONVECTIVE-04",
            PeakIncidentPlasmaHeatFluxMwPerM2: 7.5, // Low heat flux < 12.0 MW/m2
            LithiumSurfaceTemperatureCelsius: 420.0,
            MeshPoreRadiusMicrons: 25.0,
            CapillaryReplenishmentPressureKPa: 30.0,
            TransverseMagneticFieldTesla: 4.5,
            CorePlasmaEffectiveChargeZeff: 1.10,
            ThermoelectricMhdReplenishmentPumpActive: true
        );

        var result = _engine.AnalyzeLiquidMetalDivertor(telemetry);

        result.DivertorProtectionRegime.Should().Be("SUB_VAPORIZATION_CONVECTIVE_STABLE");
        result.IsDivertorTargetProtected.Should().BeTrue();
        result.VaporShieldingAttenuationRatio.Should().BeLessThan(0.15);
    }
}
