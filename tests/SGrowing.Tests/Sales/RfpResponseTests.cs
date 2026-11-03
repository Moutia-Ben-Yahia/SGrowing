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
}
