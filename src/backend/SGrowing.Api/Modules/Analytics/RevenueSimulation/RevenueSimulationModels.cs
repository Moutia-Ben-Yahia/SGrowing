using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Analytics.RevenueSimulation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PipelineStage
{
    Discovery,
    Qualification,
    ProposalPresented,
    ContractNegotiation,
    ClosedWon,
    ClosedLost
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ForecastCategory
{
    Pipeline,
    BestCase,
    Commit,
    Closed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SimulationDistributionModel
{
    Normal,
    LogNormal,
    Triangular
}

public record OpportunitySimulationInput(
    string Id,
    string DealName,
    string AccountName,
    double Amount,
    PipelineStage Stage,
    ForecastCategory Category,
    double WinProbability,
    DateTime TargetCloseDate,
    int DealAgeDays,
    double SlippageRiskScore,
    double HistoricalStageConversionRate,
    double MinEstimatedValue,
    double MostLikelyEstimatedValue,
    double MaxEstimatedValue,
    SimulationDistributionModel Distribution
);

public record SimulationHistogramBin(
    double RangeMin,
    double RangeMax,
    int Count,
    double DensityPercent
);

public record MonteCarloRunResult(
    int Iterations,
    double P10,
    double P50,
    double P90,
    double Mean,
    double StandardDeviation,
    double TargetQuota,
    double QuotaAttainmentProbability,
    double ExpectedValue,
    double PipelineTotalValue,
    double CommitTotalValue,
    double BestCaseTotalValue,
    IReadOnlyList<double> PercentileDistribution,
    IReadOnlyList<SimulationHistogramBin> HistogramBins,
    DateTime SimulatedAtUtc
);

public record DealSlippageRiskAlert(
    string OpportunityId,
    string DealName,
    string AccountName,
    double Amount,
    PipelineStage Stage,
    double SlippageProbability,
    string RiskLevel,
    string RiskReason,
    string RecommendedIntervention,
    int DaysStalled
);

public record RunMonteCarloSimulationRequest(
    int Iterations = 5000,
    double QuotaTarget = 1500000,
    SimulationDistributionModel DefaultDistribution = SimulationDistributionModel.Triangular,
    double MarketVolatilityIndex = 1.0,
    List<OpportunitySimulationInput>? CustomOpportunities = null
);

public record AddOpportunityInputRequest(
    string DealName,
    string AccountName,
    double Amount,
    PipelineStage Stage,
    ForecastCategory Category,
    double WinProbability,
    DateTime? TargetCloseDate = null,
    int DealAgeDays = 15,
    double MinEstimatedValue = 0,
    double MostLikelyEstimatedValue = 0,
    double MaxEstimatedValue = 0,
    SimulationDistributionModel Distribution = SimulationDistributionModel.Triangular
);

public record RevenueSimulationOverviewReport(
    string TenantId,
    DateTime GeneratedAtUtc,
    double TotalPipelineValue,
    double WeightedPipelineValue,
    int TotalOpportunitiesCount,
    MonteCarloRunResult BaselineSimulation,
    IReadOnlyList<OpportunitySimulationInput> TopOpportunities,
    IReadOnlyList<DealSlippageRiskAlert> CriticalSlippageAlerts,
    Dictionary<PipelineStage, double> StageBreakdown,
    Dictionary<ForecastCategory, double> CategoryBreakdown
);
