namespace SGrowing.Api.Modules.Finance.SviVolatilitySurfaceCalibration;

public record SviVolatilityTelemetry
{
    public required string OptionSurfaceUid { get; init; }
    public string UnderlyingAsset { get; init; } = "SPX_Index_Options";
    public double ExpiryTenorYears { get; init; } = 0.50; // 6 months
    public double SviParameterA { get; init; } = 0.04; // Baseline variance level
    public double SviParameterB { get; init; } = 0.18; // Wing slope b >= 0
    public double SviParameterRho { get; init; } = -0.65; // Skew rho in (-1, 1)
    public double SviParameterM { get; init; } = 0.02; // Translation
    public double SviParameterSigma { get; init; } = 0.22; // Smoothness sigma > 0
    public double MinimumButterflyDensityG { get; init; } = 0.025; // Target >= 0
    public double CalibrationRmseBasisPoints { get; init; } = 12.4; // Target <= 18 bps
    public bool IsCalendarSpreadMonotonic { get; init; } = true;
}

public record SviVolatilityEvaluation
{
    public required string OptionSurfaceUid { get; init; }
    public double AsymptoticPutWingSlope { get; init; }
    public double AsymptoticCallWingSlope { get; init; }
    public bool IsSviParametersAdmissible { get; init; }
    public bool IsButterflyArbitrageFree { get; init; }
    public bool IsCalendarSpreadArbitrageFree { get; init; }
    public bool IsVolatilitySurfaceCalibrated { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
