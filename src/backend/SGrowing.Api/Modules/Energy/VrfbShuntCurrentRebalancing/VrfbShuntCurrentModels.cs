namespace SGrowing.Api.Modules.Energy.VrfbShuntCurrentRebalancing;

public record VrfbShuntCurrentTelemetry
{
    public required string VrfbStorageSystemId { get; init; }
    public int StackCellCount { get; init; } = 40; // 20 - 80 cells
    public double RatedStackPowerKilowatts { get; init; } = 150.0;
    public double MeasuredShuntCurrentLossKilowatts { get; init; } = 3.8; // Target <= 3.5% of rated (<= 5.25 kW)
    public double PositiveTankStateOfChargePercent { get; init; } = 64.5;
    public double NegativeTankStateOfChargePercent { get; init; } = 63.2; // Diff <= 2.5%
    public double VanadiumCrossoverRateMolesPerDay { get; init; } = 0.0085; // Target <= 0.015 mol/day
    public double PositiveElectrolyteTemperatureCelsius { get; init; } = 28.5; // Target 15 - 38 C
    public double NegativeElectrolyteTemperatureCelsius { get; init; } = 27.8; // Target 12 - 35 C
    public double RoundTripEnergyEfficiencyPercent { get; init; } = 83.5; // Target >= 82.0%
    public double CoulombicEfficiencyPercent { get; init; } = 97.2; // Target >= 96.5%
}

public record VrfbShuntCurrentEvaluation
{
    public required string VrfbStorageSystemId { get; init; }
    public double ShuntCurrentPowerLossFractionPercent { get; init; }
    public double StateOfChargeAsymmetryPercent { get; init; }
    public bool IsShuntCurrentLossAcceptable { get; init; }
    public bool IsStateOfChargeSymmetric { get; init; }
    public bool IsElectrolyteTemperatureSafe { get; init; }
    public bool IsVrfbStackHealthCertified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
