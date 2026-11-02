namespace SGrowing.Api.Modules.Quantum.SiliconSpinQubitExchange;

public record SiliconSpinQubitTelemetry
{
    public required string DotPairId { get; init; }
    public string SubstrateType { get; init; } = "Silicon28_MOS"; // or "Si/SiGe_Heterostructure"
    public double DetuningBiasMillivolts { get; init; } = 0.0; // eps at sweet spot = 0 mV
    public double InterDotTunnelCouplingMicroElectronVolts { get; init; } = 8.5; // t_c in ueV
    public double OnSiteChargingEnergyMilliElectronVolts { get; init; } = 3.2; // U in meV
    public double ValleySplittingEnergyMicroElectronVolts { get; init; } = 145.0; // Ev >= 100 ueV
    public double RmsDetuningChargeNoiseMicrovolts { get; init; } = 1.8; // sigma_eps in uV
    public double ExchangeGateDurationNanoseconds { get; init; } = 18.0; // t_gate <= 25 ns
    public double PauliSpinBlockadeReadoutFidelityPercent { get; init; } = 99.2; // >= 98.0%
    public double SpinDephasingTimeT2StarMicroseconds { get; init; } = 22.0; // T2* in us
}

public record SiliconSpinQubitEvaluation
{
    public required string DotPairId { get; init; }
    public double CalculatedExchangeCouplingMhz { get; init; }
    public double ChargeNoiseDephasingRateKhz { get; init; }
    public double TwoQubitSwapGateFidelityPercent { get; init; }
    public bool IsAtExchangeSweetSpot { get; init; }
    public bool IsValleySplittingAdequate { get; init; }
    public bool IsPauliBlockadeReadoutOptimal { get; init; }
    public bool IsTwoQubitLogicReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
