namespace SGrowing.Api.Modules.Energy.MicrochannelFischerTropschSaf;

public record FischerTropschSafTelemetry
{
    public required string ReactorUnitId { get; init; }
    public string CatalystType { get; init; } = "Cobalt_Alumina_Promoted_Co_Re_Al2O3";
    public double SyngasH2ToCoRatio { get; init; } = 2.06; // Target 2.0 - 2.1
    public double ReactorOperatingTemperatureCelsius { get; init; } = 215.0; // 210 - 230 C
    public double OperatingPressureBar { get; init; } = 25.0; // 20 - 30 bar
    public double BedHotspotTemperatureExcessCelsius { get; init; } = 4.5; // Target <= 8.0 C
    public double AsfChainGrowthProbabilityAlpha { get; init; } = 0.905; // Target 0.88 - 0.93 for SAF
    public double CarbonMonoxideConversionPercent { get; init; } = 76.5; // Target >= 72.0%
    public double MethaneSelectivityPercent { get; init; } = 6.8; // Target <= 8.5%
    public double SteamToHydrogenPartialPressureRatio { get; init; } = 0.85; // Target <= 1.2
    public double CatalystDeactivationRatePercentPerDay { get; init; } = 0.018; // Target <= 0.025%
}

public record FischerTropschSafEvaluation
{
    public required string ReactorUnitId { get; init; }
    public double JetFuelKeroseneYieldPercent { get; init; }
    public bool IsIsothermalHeatRemovalStable { get; init; }
    public bool IsChainGrowthOptimalForSaf { get; init; }
    public bool IsCatalystOxidationPrevented { get; init; }
    public bool IsFischerTropschSafCertified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
