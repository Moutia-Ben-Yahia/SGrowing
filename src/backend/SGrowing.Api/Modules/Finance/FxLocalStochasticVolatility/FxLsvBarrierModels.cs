namespace SGrowing.Api.Modules.Finance.FxLocalStochasticVolatility;

public record FxLsvBarrierTelemetry
{
    public required string DealContractId { get; init; }
    public string CurrencyPair { get; init; } = "EURUSD";
    public double SpotExchangeRate { get; init; } = 1.0850;
    public double DomesticInterestRatePercent { get; init; } = 4.25; // USD rate
    public double ForeignInterestRatePercent { get; init; } = 3.50; // EUR rate
    public double BarrierLevel { get; init; } = 1.1200; // Up-and-out barrier
    public string BarrierType { get; init; } = "UpAndOutCall"; // UpAndOutCall, DownAndOutPut, DoubleNoTouch
    public double AtTheMoneyImpliedVolatilityPercent { get; init; } = 8.2; // 8.2%
    public double RiskReversal25DeltaPercent { get; init; } = -0.45; // -0.45% skew
    public double Butterfly25DeltaPercent { get; init; } = 0.28; // 0.28% smile
    public double StochasticVolOfVolXi { get; init; } = 0.45; // xi in Heston/SABR
    public double MeanReversionSpeedKappa { get; init; } = 1.8; // kappa
    public double OptionMaturityYears { get; init; } = 0.5; // 6 months
}

public record FxLsvBarrierEvaluation
{
    public required string DealContractId { get; init; }
    public double CalculatedBarrierOptionPricePips { get; init; }
    public double LocalStochasticLeverageFactorA { get; init; }
    public double BarrierHittingProbabilityPercent { get; init; }
    public double VanillaSmilePriceDeltaPips { get; init; }
    public bool IsLeverageFunctionCalibrated { get; init; }
    public bool IsLocalVarianceArbitrageFree { get; init; }
    public bool IsBarrierPinningRiskContained { get; init; }
    public bool IsLsvPricingReliable { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
