namespace SGrowing.Api.Modules.Aerospace.DualCombSpectroscopyGreenhouseFlux;

public sealed record DualCombSpectroscopyTelemetry
{
    public required string FlightMissionId { get; init; }
    public string TargetGasSpecies { get; init; } = "CO2"; // e.g. "CO2", "CH4", "N2O"
    public double CombRepetitionRateMhz { get; init; } = 100.0; // f_r1 = 100 MHz
    public double RepetitionRateOffsetKhz { get; init; } = 2.5; // Delta f_r = 2.5 kHz
    public double OpticalPathLengthMeters { get; init; } = 3500.0; // 3.5 km atmospheric open path
    public double MeasuredSnrRatio { get; init; } = 1850.0; // spectroscopic SNR
    public double AllanDeviationAt100s { get; init; } = 1.2e-13; // frequency comb stability
    public double RetrievedColumnAverageDryAirMoleFraction { get; init; } = 421.5; // X_CO2 (ppm) or X_CH4 (ppb)
    public double AmbientAtmosphericPressureHpa { get; init; } = 1013.25; // ambient pressure (hPa)
    public double AmbientAtmosphericTemperatureKelvin { get; init; } = 288.15; // ambient temperature (K)
    public double BaselineExpectedMoleFraction { get; init; } = 420.0; // baseline expected background (ppm)
}

public sealed record DualCombSpectroscopyAnalysisResult
{
    public required string FlightMissionId { get; init; }
    public required double DownconversionMultiheterodyneScaleFactor { get; init; }
    public required double NonAliasedOpticalBandwidthThz { get; init; }
    public required double RetrievedGasMoleFraction { get; init; }
    public required double AnomalyDeltaFromBaseline { get; init; }
    public required double MeasurementUncertainty { get; init; }
    public required bool IsSnrSufficientForRetrieval { get; init; }
    public required bool IsCombPhaseCoherenceStable { get; init; }
    public required bool IsAtmosphericFluxSignificant { get; init; }
    public required string SpectroscopyRetrievalRegime { get; init; }
    public required string FluxSurveillanceRecommendation { get; init; }
}
