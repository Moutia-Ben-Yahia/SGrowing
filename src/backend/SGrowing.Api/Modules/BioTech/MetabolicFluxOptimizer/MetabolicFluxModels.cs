namespace SGrowing.Api.Modules.BioTech.MetabolicFluxOptimizer;

public record BioreactorTelemetry(
    string VesselId,
    double BiomassConcentrationGPerL,
    double GlucoseConcentrationGPerL,
    double DissolvedOxygenPercent,
    double TemperatureCelsius,
    double Ph,
    double CultivationHours);

public record MetabolicKineticParameters(
    double MaxSpecificGrowthRateMuMax = 0.45,
    double MonodConstantKs = 0.15,
    double SubstrateInhibitionKi = 80.0,
    double YieldBiomassOnSubstrateYxs = 0.50,
    double TargetDissolvedOxygenPercent = 30.0);

public record FedBatchOptimizationRequest(
    BioreactorTelemetry Telemetry,
    MetabolicKineticParameters? Parameters = null,
    double FeedStockConcentrationGPerL = 500.0,
    double CurrentVesselVolumeLiters = 100.0);

public record FedBatchOptimizationResult(
    string VesselId,
    double CurrentGrowthRateMu,
    double OptimalFeedRateLitersPerHour,
    double OxygenEnrichmentPercent,
    double PredictedBiomassAccumulationGPerL,
    string GrowthPhase,
    string MetabolicWarning);
