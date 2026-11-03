namespace SGrowing.Api.Modules.Voice.VoiceIntentFastPath;

public record FastPathIntentPrediction(
    string PredictionId,
    string CallSessionId,
    string RecognizedIntent, // BILLING_DISPUTE, PRICING_CONCESSION, TECH_SUPPORT_ESCALATE, CANCELLATION_RISK, ORDER_STATUS
    double ConfidenceScore, // 0.0 to 1.0
    double ProcessingLatencyMs, // sub-65ms target
    string AcousticDialectNormalized, // SOUTHERN_US, UK_SCOTTISH, AAVE, AUS_CASUAL, STANDARD_NEUTRAL
    string RecommendedActionRoute,
    DateTime Timestamp
);

public record FastPathLatencyBenchmark(
    string BenchmarkId,
    double DirectAcousticIntentLatencyMs, // e.g. 48.2ms
    double TraditionalSttAndNluLatencyMs, // e.g. 520.0ms
    double LatencyReductionPct // e.g. 90.7%
);

public record VoiceIntentOverview(
    int TotalInFlightPredictionsCount,
    double AverageFastPathLatencyMs,
    double IntentClassificationAccuracyPct,
    int EscalationsToDeepLlmCount,
    List<FastPathIntentPrediction> RecentPredictions,
    FastPathLatencyBenchmark Benchmark
);

public record ClassifyIntentRequest(
    string CallSessionId,
    string RawAcousticFeaturesHash,
    string InputDialectTag
);

public record TuneThresholdRequest(
    double FastPathCutoffThreshold
);
