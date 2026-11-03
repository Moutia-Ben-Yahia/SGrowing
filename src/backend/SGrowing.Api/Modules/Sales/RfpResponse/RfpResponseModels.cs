using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Sales.RfpResponse;

public sealed record RfpQuestionnaireRecord(
    string QuestionnaireId,
    string EnterpriseAccountName,
    string QuestionnaireType, // SECURITY_SOC2_ISO, TECHNICAL_ARCHITECTURE, COMMERCIAL_RFP
    int TotalQuestionsCount,
    int AnsweredQuestionsCount,
    double ConfidenceScoreAvgPct,
    string Status // IN_PROGRESS, READY_FOR_REVIEW, EXPORTED
);

public sealed record RfpAnswerRecord(
    string AnswerId,
    string QuestionnaireId,
    string QuestionPrompt,
    string SynthesizedAnswer,
    double SourceAttributionScore,
    string CanonicalSourceReference,
    bool IsVerifiedByLeadArchitect
);

public sealed record RfpOverview(
    int TotalActiveQuestionnairesCount,
    int TotalQuestionsSynthesized,
    double AverageConfidencePct,
    double TimeSavedHoursEstimated,
    IReadOnlyList<RfpQuestionnaireRecord> Questionnaires,
    IReadOnlyList<RfpAnswerRecord> RecentAnswers
);

public sealed record GenerateRfpAnswerRequest(
    string QuestionnaireId,
    string QuestionPrompt,
    string Category
);

public sealed record ImportQuestionnaireRequest(
    string EnterpriseAccountName,
    string QuestionnaireType,
    int QuestionsCount
);
