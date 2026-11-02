namespace SGrowing.Api.Modules.BioTech.NanoporeTranslocationBasecaller;

public record IonicCurrentSample(
    long SampleIndex,
    double CurrentPicoAmperes,
    double DwellTimeMicroseconds
);

public record NanoporeChannelTelemetry(
    string ChannelId,
    double AppliedVoltageBiasMilliVolts = 180.0,
    double OpenPoreCurrentPicoAmperes = 220.0,
    IReadOnlyList<IonicCurrentSample>? CurrentSignal = null,
    double TemperatureCelsius = 34.0,
    double MembraneResistanceGigaOhms = 4.5
);

public record BasecallerThresholds(
    double MaxStallDwellTimeMicroseconds = 50000.0, // 50ms stall threshold
    double PolyACurrentLevelFraction = 0.28, // Typical I/I_0 for poly-A homopolymer
    double MethylationShiftPicoAmperes = 4.2
);

public record NanoporeEvaluationRequest(
    NanoporeChannelTelemetry Telemetry,
    BasecallerThresholds Thresholds
);

public record NanoporeTranslocationDecision(
    string ChannelId,
    int TotalKmersTranslocated,
    int EstimatedPolyATailLengthNucleotides,
    int MethylatedCpGCount,
    double MeanTranslocationSpeedBps,
    bool PoreStallEventDetected,
    string ChannelState,
    string RecommendedPoreControlDirective
);
