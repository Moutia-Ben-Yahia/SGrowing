namespace SGrowing.Api.Modules.BioTech.NanoporeSquiggleBasecalling;

public record NanoporeSquiggleTelemetry
{
    public required string ChannelPoreId { get; init; }
    public string PoreChemistryType { get; init; } = "R10.4.1_CsgG"; // or "R9.4.1"
    public double OpenPoreBaselineCurrentPicoAmps { get; init; } = 105.0; // ~90 - 120 pA
    public double CurrentBaselineDriftPicoAmpsPerMin { get; init; } = 0.85; // <= 2.5 pA/min
    public double MeanTranslocationVelocityBps { get; init; } = 420.0; // ~400 - 450 bps
    public double HelicaseDwellTimeVarianceRatio { get; init; } = 1.15; // <= 1.50
    public double RawSignalSignalToNoiseRatio { get; init; } = 14.5; // SNR >= 10.0
    public double MeanPhredQualityScore { get; init; } = 21.5; // Q >= 18.0
    public double Epigenetic5mCPredictionConfidencePercent { get; init; } = 94.2; // >= 88.0%
    public double HomopolymerCompressionArtifactRatePercent { get; init; } = 1.4; // <= 3.0%
}

public record NanoporeSquiggleEvaluation
{
    public required string ChannelPoreId { get; init; }
    public double CalculatedBasecallingAccuracyPercent { get; init; }
    public double EstimatedHelicaseProcessivityScore { get; init; }
    public bool IsCurrentBaselineStable { get; init; }
    public bool IsHelicaseTranslocationRegular { get; init; }
    public bool IsEpigeneticCallingConfident { get; init; }
    public bool IsHighAccuracyBasecallingReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
