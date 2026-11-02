using System;

namespace SGrowing.Api.Modules.BioTech.ApiPolymorphContinuousCrystallization;

public sealed record ApiCrystallizerTelemetry(
    string CrystallizerBatchId,
    string ApiMoleculeName,
    double SlurryTemperatureCelsius,
    double SolutionConcentrationGramsPerLiter,
    double EquilibriumFormISolubilityGramsPerLiter,
    double MeasuredMetastableFormIiMassFractionPercent,
    double MeanCrystalSizeD50Micrometers,
    double CrystallizerResidenceTimeMinutes,
    double ImpellerTorqueSurgePercent,
    double FilterDifferentialPressureBar);

public sealed record ApiCrystallizerAnalysisResult(
    string CrystallizerBatchId,
    double SupersaturationRatioS,
    double FormIPurityFractionPercent,
    double OstwaldTransformationRatePercentPerMinute,
    string CrystallizationQualityRegime,
    string PbeSeedingAndProcessRecommendation,
    bool IsBatchSpecificationCompliant,
    DateTimeOffset Timestamp);
