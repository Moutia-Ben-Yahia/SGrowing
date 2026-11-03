using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.VoiceDialect;

public sealed record RegionalDialectProfileRecord(
    string DialectCode, // en-US-TEXAS, en-GB-SCOTTISH, es-MX-NORTE, de-AT-VIENNA
    string RegionName,
    string LinguisticCadenceDescription,
    double PitchAdjustmentSemiTones,
    double SpeechRateMultiplier,
    IReadOnlyList<string> LocalIdiomMappings
);

public sealed record AdaptedPhraseRecord(
    string AdaptationId,
    string DialectCode,
    string StandardPhrase,
    string CulturallyAdaptedPhrase,
    double NaturalnessScore,
    DateTime Timestamp
);

public sealed record VoiceDialectOverview(
    int SupportedDialectsCount,
    int TotalPhrasesAdaptedCount,
    double AverageNaturalnessScore,
    IReadOnlyList<RegionalDialectProfileRecord> Profiles,
    IReadOnlyList<AdaptedPhraseRecord> RecentAdaptations
);

public sealed record AdaptPhraseRequest(
    string DialectCode,
    string StandardPhrase,
    string ConversationContext
);

public sealed record RegisterDialectProfileRequest(
    string DialectCode,
    string RegionName,
    string LinguisticCadenceDescription,
    double PitchAdjustmentSemiTones,
    double SpeechRateMultiplier,
    IReadOnlyList<string> LocalIdiomMappings
);
