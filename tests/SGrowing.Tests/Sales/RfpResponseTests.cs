using System;
using System.Linq;
using SGrowing.Api.Modules.Sales.RfpResponse;
using Xunit;

namespace SGrowing.Tests.Sales;

public sealed class RfpResponseTests
{
    [Fact]
    public void GetOverview_ReturnsInitialQuestionnairesAndAnswers()
    {
        var engine = new RfpResponseEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalActiveQuestionnairesCount >= 2);
        Assert.True(overview.TotalQuestionsSynthesized > 0);
        Assert.True(overview.AverageConfidencePct > 90.0);
        Assert.NotEmpty(overview.Questionnaires);
        Assert.NotEmpty(overview.RecentAnswers);
    }

    [Fact]
    public void GenerateAnswer_ProducesVerifiedAttributedResponse()
    {
        var engine = new RfpResponseEngine();
        var request = new GenerateRfpAnswerRequest(
            "RFP-SEC-901",
            "What is your SLA on multi-region failover and data recovery?",
            "DISASTER_RECOVERY"
        );

        var answer = engine.GenerateAnswer(request);

        Assert.NotNull(answer);
        Assert.StartsWith("ANS-", answer.AnswerId);
        Assert.NotEmpty(answer.SynthesizedAnswer);
        Assert.True(answer.SourceAttributionScore > 0.9);
        Assert.True(answer.IsVerifiedByLeadArchitect);
    }

    [Fact]
    public void ImportQuestionnaire_RegistersNewRfpWorkspace()
    {
        var engine = new RfpResponseEngine();
        var request = new ImportQuestionnaireRequest(
            "Acme Global Telecom",
            "COMMERCIAL_RFP",
            85
        );

        var q = engine.ImportQuestionnaire(request);

        Assert.NotNull(q);
        Assert.StartsWith("RFP-", q.QuestionnaireId);
        Assert.Equal("Acme Global Telecom", q.EnterpriseAccountName);
        Assert.Equal(85, q.TotalQuestionsCount);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Questionnaires, item => item.QuestionnaireId == q.QuestionnaireId);
    }

    [Fact]
    public void GenerateAnswer_AttachesAttributionAndConfidenceCorrectly()
    {
        var engine = new RfpResponseEngine();
        var request = new GenerateRfpAnswerRequest(
            "RFP-COM-902",
            "Explain voice latency optimizations and buffering architectures.",
            "INFRASTRUCTURE"
        );

        var answer = engine.GenerateAnswer(request);

        Assert.NotNull(answer);
        Assert.Equal("RFP-COM-902", answer.QuestionnaireId);
        Assert.Contains("SGrowing implements enterprise-grade compliance", answer.SynthesizedAnswer);
        Assert.Equal(0.978, answer.SourceAttributionScore);
        Assert.Equal("Corporate Compliance Matrix & Architecture Whitepaper 2026", answer.CanonicalSourceReference);
    }

    [Fact]
    public void ImportQuestionnaire_InitializesZeroCompletedQuestionsAndInProgressStatus()
    {
        var engine = new RfpResponseEngine();
        var request = new ImportQuestionnaireRequest(
            "Vanguard Healthcare Network",
            "HIPAA_SECURITY",
            50
        );

        var q = engine.ImportQuestionnaire(request);

        Assert.NotNull(q);
        Assert.Equal(0, q.AnsweredQuestionsCount);
        Assert.Equal(96.0, q.ConfidenceScoreAvgPct);
        Assert.Equal("IN_PROGRESS", q.Status);
    }

    [Fact]
    public void GenerateAnswer_AddsAnswerToRecentAnswersCollection()
    {
        var engine = new RfpResponseEngine();
        var initialOverview = engine.GetOverview();
        var initialCount = initialOverview.RecentAnswers.Count;

        var request = new GenerateRfpAnswerRequest(
            "RFP-SEC-901",
            "What data residency guarantees do you offer for EU clients?",
            "COMPLIANCE_EU_GDPR"
        );

        var answer = engine.GenerateAnswer(request);

        var updatedOverview = engine.GetOverview();
        Assert.Equal(initialCount + 1, updatedOverview.RecentAnswers.Count);
        Assert.Contains(updatedOverview.RecentAnswers, a => a.AnswerId == answer.AnswerId);
    }

    [Fact]
    public void ImportQuestionnaire_MultipleQuestionnaires_IncrementsTotalActiveCount()
    {
        var engine = new RfpResponseEngine();
        var initialCount = engine.GetOverview().TotalActiveQuestionnairesCount;

        var q1 = engine.ImportQuestionnaire(new ImportQuestionnaireRequest("FinTech Corp", "SOC2", 40));
        var q2 = engine.ImportQuestionnaire(new ImportQuestionnaireRequest("BioHealth Inc", "HIPAA", 60));

        var updatedOverview = engine.GetOverview();
        Assert.Equal(initialCount + 2, updatedOverview.TotalActiveQuestionnairesCount);
        Assert.Contains(updatedOverview.Questionnaires, q => q.QuestionnaireId == q1.QuestionnaireId);
        Assert.Contains(updatedOverview.Questionnaires, q => q.QuestionnaireId == q2.QuestionnaireId);
    }

    [Fact]
    public void GetOverview_RecalculatesTotalQuestionsAcrossImportedWorkspaces()
    {
        var engine = new RfpResponseEngine();
        var initialTotalQuestions = engine.GetOverview().TotalQuestionsSynthesized;

        engine.ImportQuestionnaire(new ImportQuestionnaireRequest("Retail Giant", "SECURITY", 150));

        var updatedOverview = engine.GetOverview();
        Assert.Equal(initialTotalQuestions + 150, updatedOverview.TotalQuestionsSynthesized);
    }
}
