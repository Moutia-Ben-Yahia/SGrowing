namespace SGrowing.Tests.BioTech;

using SGrowing.Api.Modules.BioTech.MicrobialElectrosynthesis;
using Xunit;

public class MicrobialElectrosynthesisTests
{
    private readonly MicrobialElectrosynthesisEngine _engine = new();

    [Fact]
    public void AnalyzeBiocathode_OptimalAcetogenicMes_MaintainsCathodePoise()
    {
        // 5.5 g/L/day acetate, j = 35 A/m2 -> high Faradaic efficiency ~77%
        // E = -0.60 V vs SHE, biofilm coverage 92%, H2 off-gas 10%
        var telemetry = new MicrobialElectrosynthesisTelemetry(
            ReactorId: "MES-BIO-REACTOR-01",
            BacterialStrain: "Sporomusa ovata",
            CathodePotentialVoltsVsShe: -0.60,
            CathodeCurrentDensityAmpsPerM2: 35.0,
            DissolvedCo2MilliMolar: 30.0,
            AcetateProductionRateGramsPerLiterDay: 1.5,
            OffGasHydrogenFraction: 0.10,
            BiofilmCoveragePercent: 92.0,
            ElectrodeSurfaceAreaM2: 0.50);

        var result = _engine.AnalyzeBiocathode(telemetry);

        Assert.Equal("MES-BIO-REACTOR-01", result.ReactorId);
        Assert.Equal("Sporomusa ovata", result.BacterialStrain);
        Assert.Equal("OPTIMAL_ACETOGENIC_MES", result.BiocathodeOperatingRegime);
        Assert.Equal("MAINTAIN_CATHODE_POISE", result.CathodePotentialCommand);
        Assert.False(result.IsBiofilmDetachmentRiskDetected);
        Assert.True(result.AcetateFaradaicEfficiencyPercent > 60.0);
    }

    [Fact]
    public void AnalyzeBiocathode_HighAbioticHydrogenLoss_ShiftsPotentialAnodically()
    {
        // E = -0.72 V vs SHE, H2 off-gas = 42% (>= 35%)
        var telemetry = new MicrobialElectrosynthesisTelemetry(
            ReactorId: "MES-BIO-REACTOR-02",
            BacterialStrain: "Moorella thermoacetica",
            CathodePotentialVoltsVsShe: -0.72,
            CathodeCurrentDensityAmpsPerM2: 60.0,
            DissolvedCo2MilliMolar: 25.0,
            AcetateProductionRateGramsPerLiterDay: 0.8,
            OffGasHydrogenFraction: 0.42,
            BiofilmCoveragePercent: 78.0,
            ElectrodeSurfaceAreaM2: 0.50);

        var result = _engine.AnalyzeBiocathode(telemetry);

        Assert.Equal("HIGH_HYDROGEN_EVOLUTION_WASTE", result.BiocathodeOperatingRegime);
        Assert.Equal("SHIFT_POTENTIAL_ANODIC_50MV", result.CathodePotentialCommand);
        Assert.False(result.IsBiofilmDetachmentRiskDetected);
        Assert.True(result.AbioticHydrogenLossPercent >= 35.0);
    }

    [Fact]
    public void AnalyzeBiocathode_ExcessiveCathodicPotential_TriggersBiofilmDetachmentAlert()
    {
        // E = -0.85 V vs SHE (<= -0.80 V) or coverage collapsed
        var telemetry = new MicrobialElectrosynthesisTelemetry(
            ReactorId: "MES-SHEAR-RISK-03",
            BacterialStrain: "Clostridium ljungdahlii",
            CathodePotentialVoltsVsShe: -0.85,
            CathodeCurrentDensityAmpsPerM2: 95.0,
            DissolvedCo2MilliMolar: 20.0,
            AcetateProductionRateGramsPerLiterDay: 0.5,
            OffGasHydrogenFraction: 0.60,
            BiofilmCoveragePercent: 55.0,
            ElectrodeSurfaceAreaM2: 0.50);

        var result = _engine.AnalyzeBiocathode(telemetry);

        Assert.True(result.IsBiofilmDetachmentRiskDetected);
        Assert.Equal("BIOFILM_SHEAR_DETACHMENT_ALERT", result.BiocathodeOperatingRegime);
        Assert.Equal("EMERGENCY_POTENTIAL_RELAXATION", result.CathodePotentialCommand);
        Assert.Contains("BIOFILM DETACHMENT RISK", result.BioelectrochemicalSummary);
    }

    [Fact]
    public void AnalyzeBiocathode_NegativeCurrentDensity_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new MicrobialElectrosynthesisTelemetry(
            ReactorId: "ERR-001",
            BacterialStrain: "Sporomusa",
            CathodePotentialVoltsVsShe: -0.6,
            CathodeCurrentDensityAmpsPerM2: -10.0,
            DissolvedCo2MilliMolar: 20.0,
            AcetateProductionRateGramsPerLiterDay: 1.0,
            OffGasHydrogenFraction: 0.1,
            BiofilmCoveragePercent: 80.0,
            ElectrodeSurfaceAreaM2: 0.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeBiocathode(invalid));
    }
}
