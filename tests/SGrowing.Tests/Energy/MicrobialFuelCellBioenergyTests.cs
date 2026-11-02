namespace SGrowing.Tests.Energy;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Energy.MicrobialFuelCellBioenergy;
using Xunit;

public class MicrobialFuelCellBioenergyTests
{
    private readonly MicrobialFuelCellBioenergyEngine _engine = new();

    [Fact]
    public void AnalyzeBioenergyProcess_NominalWastewaterTreatment_ReturnsOptimalRegime()
    {
        var telemetry = new MicrobialFuelCellTelemetry(
            ReactorId: "MFC-BIO-01",
            AnodeElectrodeSurfaceAreaM2: 0.50,
            InfluentCodMilligramsPerLiter: 1200.0,
            EffluentCodMilligramsPerLiter: 180.0, // 85% COD removal
            HydraulicRetentionTimeHours: 12.0,
            MeasuredOperatingCurrentMilliAmperes: 45.0,
            OpenCircuitVoltageVolts: 0.78,
            TotalInternalResistanceOhms: 15.0,
            AnodeBiofilmThicknessMicrometers: 45.0,
            SolutionPh: 7.2
        );

        var result = _engine.AnalyzeBioenergyProcess(telemetry);

        result.ReactorId.Should().Be("MFC-BIO-01");
        result.BioenergyRegime.Should().Be("OPTIMAL_EXOELECTROGENIC_BIOENERGY_GENERATION");
        result.IsBioenergyNominal.Should().BeTrue();
        result.CodRemovalEfficiencyPercent.Should().BeApproximately(85.0, 0.1);
        result.CoulombicEfficiencyPercent.Should().BeGreaterThan(20.0);
        result.MaximumPowerPointDensityMilliWattsPerM2.Should().BeGreaterThan(15.0);
        result.OperatingPowerOutputMilliWatts.Should().BeGreaterThan(0.0);
        result.BioreactorOptimizationGuidance.Should().ContainEquivalentOf("remediation and renewable bioelectricity generation optimal");
    }

    [Fact]
    public void AnalyzeBioenergyProcess_ExcessiveBiofilm_ReturnsDiffusionLimitation()
    {
        var telemetry = new MicrobialFuelCellTelemetry(
            ReactorId: "MFC-BIO-02",
            AnodeElectrodeSurfaceAreaM2: 0.50,
            InfluentCodMilligramsPerLiter: 1000.0,
            EffluentCodMilligramsPerLiter: 200.0,
            HydraulicRetentionTimeHours: 10.0,
            MeasuredOperatingCurrentMilliAmperes: 35.0,
            OpenCircuitVoltageVolts: 0.75,
            TotalInternalResistanceOhms: 18.0,
            AnodeBiofilmThicknessMicrometers: 135.0, // Exceeds 110 um
            SolutionPh: 7.0
        );

        var result = _engine.AnalyzeBioenergyProcess(telemetry);

        result.BioenergyRegime.Should().Be("BIOFILM_DIFFUSION_LIMITATION_CLOGGING");
        result.IsBioenergyNominal.Should().BeFalse();
        result.BioreactorOptimizationGuidance.Should().ContainEquivalentOf("hydrodynamic shearing pulse");
    }

    [Fact]
    public void AnalyzeBioenergyProcess_HighInternalResistance_ReturnsOhmicLossWarning()
    {
        var telemetry = new MicrobialFuelCellTelemetry(
            ReactorId: "MFC-BIO-03",
            AnodeElectrodeSurfaceAreaM2: 0.50,
            InfluentCodMilligramsPerLiter: 1100.0,
            EffluentCodMilligramsPerLiter: 220.0,
            HydraulicRetentionTimeHours: 12.0,
            MeasuredOperatingCurrentMilliAmperes: 20.0,
            OpenCircuitVoltageVolts: 0.72,
            TotalInternalResistanceOhms: 78.0, // Exceeds 60 Ohms
            AnodeBiofilmThicknessMicrometers: 50.0,
            SolutionPh: 6.8
        );

        var result = _engine.AnalyzeBioenergyProcess(telemetry);

        result.BioenergyRegime.Should().Be("EXCESSIVE_INTERNAL_RESISTANCE_OHMIC_LOSS");
        result.IsBioenergyNominal.Should().BeFalse();
        result.BioreactorOptimizationGuidance.Should().ContainEquivalentOf("backwash proton exchange membrane");
    }

    [Fact]
    public void AnalyzeBioenergyProcess_LowCodRemoval_ReturnsShortfallWarning()
    {
        var telemetry = new MicrobialFuelCellTelemetry(
            ReactorId: "MFC-BIO-04",
            AnodeElectrodeSurfaceAreaM2: 0.50,
            InfluentCodMilligramsPerLiter: 1000.0,
            EffluentCodMilligramsPerLiter: 550.0, // Only 45% removal (< 60%)
            HydraulicRetentionTimeHours: 4.0,
            MeasuredOperatingCurrentMilliAmperes: 30.0,
            OpenCircuitVoltageVolts: 0.70,
            TotalInternalResistanceOhms: 25.0,
            AnodeBiofilmThicknessMicrometers: 40.0,
            SolutionPh: 7.1
        );

        var result = _engine.AnalyzeBioenergyProcess(telemetry);

        result.BioenergyRegime.Should().Be("COD_REMEDIATION_SHORTFALL_WARNING");
        result.IsBioenergyNominal.Should().BeFalse();
        result.BioreactorOptimizationGuidance.Should().ContainEquivalentOf("Geobacter inoculum");
    }

    [Fact]
    public void AnalyzeBioenergyProcess_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeBioenergyProcess(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
