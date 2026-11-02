using System;
using SGrowing.Api.Modules.BioTech.ApiPolymorphContinuousCrystallization;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class ApiPolymorphContinuousCrystallizationTests
{
    private readonly ApiPolymorphContinuousCrystallizationEngine _engine = new();

    [Fact]
    public void AnalyzeCrystallizationState_OptimalSteadyState_MaintainsFormIPurity()
    {
        var telemetry = new ApiCrystallizerTelemetry(
            CrystallizerBatchId: "MSMPR-PHARMA-RUN-501",
            ApiMoleculeName: "Ritonavir-Form-I",
            SlurryTemperatureCelsius: 24.5,
            SolutionConcentrationGramsPerLiter: 42.0,
            EquilibriumFormISolubilityGramsPerLiter: 30.0,
            MeasuredMetastableFormIiMassFractionPercent: 1.2,
            MeanCrystalSizeD50Micrometers: 92.0,
            CrystallizerResidenceTimeMinutes: 60.0,
            ImpellerTorqueSurgePercent: 4.5,
            FilterDifferentialPressureBar: 0.35);

        var result = _engine.AnalyzeCrystallizationState(telemetry);

        Assert.Equal("FORM_I_STEADY_STATE_HIGH_PURITY", result.CrystallizationQualityRegime);
        Assert.Equal("MAINTAIN_CONTINUOUS_MSMPR_WITHDRAWAL_CADENCE", result.PbeSeedingAndProcessRecommendation);
        Assert.True(result.IsBatchSpecificationCompliant);
        Assert.True(result.FormIPurityFractionPercent >= 98.0);
        Assert.True(result.SupersaturationRatioS >= 1.10 && result.SupersaturationRatioS <= 1.65);
    }

    [Fact]
    public void AnalyzeCrystallizationState_MetastableFormIiSurge_PulsesUltrasonicSeeding()
    {
        // Form II = 8.5% > 5.0%
        var telemetry = new ApiCrystallizerTelemetry(
            CrystallizerBatchId: "MSMPR-PHARMA-RUN-502",
            ApiMoleculeName: "Ritonavir-Form-I",
            SlurryTemperatureCelsius: 22.0,
            SolutionConcentrationGramsPerLiter: 40.0,
            EquilibriumFormISolubilityGramsPerLiter: 30.0,
            MeasuredMetastableFormIiMassFractionPercent: 8.5,
            MeanCrystalSizeD50Micrometers: 60.0,
            CrystallizerResidenceTimeMinutes: 45.0,
            ImpellerTorqueSurgePercent: 6.0,
            FilterDifferentialPressureBar: 0.40);

        var result = _engine.AnalyzeCrystallizationState(telemetry);

        Assert.Equal("UNDESIRED_POLYMORPH_METASTABLE_SURGE", result.CrystallizationQualityRegime);
        Assert.Equal("PULSE_ULTRASONIC_SEEDING_AND_INCREASE_SLURRY_RESIDENCE_TIME", result.PbeSeedingAndProcessRecommendation);
        Assert.False(result.IsBatchSpecificationCompliant);
    }

    [Fact]
    public void AnalyzeCrystallizationState_ExcessiveSupersaturation_FlagsMetastableSurge()
    {
        // Conc = 62.0 vs Sol = 30.0 -> S = 2.067 > 1.80
        var telemetry = new ApiCrystallizerTelemetry(
            CrystallizerBatchId: "MSMPR-PHARMA-RUN-503",
            ApiMoleculeName: "Ritonavir-Form-I",
            SlurryTemperatureCelsius: 20.0,
            SolutionConcentrationGramsPerLiter: 62.0,
            EquilibriumFormISolubilityGramsPerLiter: 30.0,
            MeasuredMetastableFormIiMassFractionPercent: 3.0,
            MeanCrystalSizeD50Micrometers: 55.0,
            CrystallizerResidenceTimeMinutes: 50.0,
            ImpellerTorqueSurgePercent: 5.0,
            FilterDifferentialPressureBar: 0.45);

        var result = _engine.AnalyzeCrystallizationState(telemetry);

        Assert.Equal("UNDESIRED_POLYMORPH_METASTABLE_SURGE", result.CrystallizationQualityRegime);
        Assert.False(result.IsBatchSpecificationCompliant);
        Assert.True(result.SupersaturationRatioS > 1.80);
    }

    [Fact]
    public void AnalyzeCrystallizationState_FilterFoulingEncrustation_TriggersSolventFlush()
    {
        var telemetry = new ApiCrystallizerTelemetry(
            CrystallizerBatchId: "MSMPR-PHARMA-RUN-504",
            ApiMoleculeName: "Ritonavir-Form-I",
            SlurryTemperatureCelsius: 25.0,
            SolutionConcentrationGramsPerLiter: 42.0,
            EquilibriumFormISolubilityGramsPerLiter: 30.0,
            MeasuredMetastableFormIiMassFractionPercent: 1.5,
            MeanCrystalSizeD50Micrometers: 90.0,
            CrystallizerResidenceTimeMinutes: 60.0,
            ImpellerTorqueSurgePercent: 8.0,
            FilterDifferentialPressureBar: 1.85); // Exceeds 1.5 bar

        var result = _engine.AnalyzeCrystallizationState(telemetry);

        Assert.Equal("CRYSTALLIZER_FOULING_ENCRUSTATION", result.CrystallizationQualityRegime);
        Assert.Equal("INITIATE_SOLVENT_FLUSH_AND_REDUCE_COOLING_JACKET_RATE", result.PbeSeedingAndProcessRecommendation);
        Assert.False(result.IsBatchSpecificationCompliant);
    }
}
