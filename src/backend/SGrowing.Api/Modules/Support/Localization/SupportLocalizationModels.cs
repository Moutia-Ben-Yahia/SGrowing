using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Support.Localization;

public enum FormalityLevel
{
    FormalExecutive,  // Japanese Keigo, German "Sie", French "Vous"
    PoliteBusiness,   // Standard respectful enterprise tone
    CasualWarm        // Conversational peer tone
}

public record GlossaryRule(
    string SourceTerm,
    string TargetTerm,
    bool KeepUntranslated
);

public record CulturalNuanceScore(
    double PolitenessIndex, // 0.0 to 100.0
    double DeEscalationSafetyScore, // 0.0 to 100.0
    bool HonorificCompliant,
    List<string> PreservedBrandTerms
);

public record LocalizedMessageResult(
    string MessageId,
    string SourceLanguage,
    string TargetLanguage,
    string OriginalText,
    string LocalizedText,
    FormalityLevel Formality,
    CulturalNuanceScore NuanceAudit,
    int LatencyMilliseconds
);

public record TranslateSupportMessageRequest(
    string MessageId,
    string SourceLanguage,
    string TargetLanguage,
    string OriginalText,
    FormalityLevel PreferredFormality
);
