namespace SGrowing.Api.Modules.Energy.DirectAirCaptureTvsaSorbent;

public record DirectAirCaptureTvsaTelemetry
{
    public required string ContactorTrainId { get; init; }
    public string SolidSorbentChemistry { get; init; } = "AmineFunctionalizedSilica_APTES"; // or "MOF_MetalOrganicFramework"
    public double AmbientCo2Ppm { get; init; } = 422.0;
    public double AdsorptionCapacityMmolPerGram { get; init; } = 1.95; // mmol CO2 / g sorbent
    public double DesorptionResidualCapacityMmolPerGram { get; init; } = 0.35; // residual
    public double DesorptionTemperatureCelsius { get; init; } = 95.0; // 85 - 100 C
    public double DesorptionVacuumPressureBar { get; init; } = 0.15; // 0.10 - 0.20 bar
    public double OxygenConcentrationDuringDesorptionVolPercent { get; init; } = 0.15; // Target <= 0.5%
    public double ContactorBedPressureDropPascals { get; init; } = 125.0; // Target <= 180 Pa
    public double SpecificThermalEnergyGigaJoulesPerTonCo2 { get; init; } = 4.85; // Target <= 5.8 GJ/t
    public double SpecificElectricalEnergyGigaJoulesPerTonCo2 { get; init; } = 1.15; // Target <= 1.4 GJ/t
    public double AmineDegradationRatePercentPerCycle { get; init; } = 0.035; // Target <= 0.06%
}

public record DirectAirCaptureTvsaEvaluation
{
    public required string ContactorTrainId { get; init; }
    public double WorkingCapacityMmolPerGram { get; init; }
    public double TotalSpecificEnergyGigaJoulesPerTonCo2 { get; init; }
    public double ProjectedSorbentCycleLifespan { get; init; }
    public bool IsWorkingCapacityAdequate { get; init; }
    public bool IsEnergeticPerformanceOptimal { get; init; }
    public bool IsOxidativeDegradationSuppressed { get; init; }
    public bool IsTvsaDecarbonizationCertified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
