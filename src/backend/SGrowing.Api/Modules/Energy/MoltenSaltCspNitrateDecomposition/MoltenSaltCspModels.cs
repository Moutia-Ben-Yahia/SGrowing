namespace SGrowing.Api.Modules.Energy.MoltenSaltCspNitrateDecomposition;

public sealed record MoltenSaltCspTelemetry
{
    public required string StorageTankId { get; init; }
    public string SaltMixtureComposition { get; init; } = "Solar-Salt-60NaNO3-40KNO3";
    public double HotTankTemperatureCelsius { get; init; } = 560.0; // T_hot (deg C, design max 565 C)
    public double ColdTankTemperatureCelsius { get; init; } = 290.0; // T_cold (deg C, min 260 C)
    public double NitriteIonConcentrationWeightPercent { get; init; } = 4.2; // NO2- wt% (limit <= 6.5%)
    public double OxideIonConcentrationPpm { get; init; } = 18.0; // O2- ppm (limit <= 50 ppm)
    public double UllageCoverGasOxygenPartialPressureAtm { get; init; } = 0.21; // P_O2 (atm)
    public double ContainmentAlloyCorrosionRateMicronsPerYear { get; init; } = 8.5; // corrosion (target <= 15 um/yr)
    public double FreezingPointCelsius { get; init; } = 220.0; // eutectic freeze point (deg C)
}

public sealed record MoltenSaltCspAnalysisResult
{
    public required string StorageTankId { get; init; }
    public required double ColdTankFreezeSafetyMarginCelsius { get; init; }
    public required double ThermalNitrateDecompositionEquilibriumRatio { get; init; }
    public required double ProjectedCorrosionRateMicronsPerYear { get; init; }
    public required double OxideInducedScaleFluxingFactor { get; init; }
    public required bool IsFreezeMarginSafe { get; init; }
    public required bool IsThermalDecompositionControlled { get; init; }
    public required bool IsTankCorrosionWithinLimit { get; init; }
    public required string MoltenSaltThermalRegime { get; init; }
    public required string StorageOperationProtocol { get; init; }
}
