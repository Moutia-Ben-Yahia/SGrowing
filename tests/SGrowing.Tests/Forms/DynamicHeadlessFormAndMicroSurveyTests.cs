using FluentAssertions;
using SGrowing.Api.Modules.Forms;
using Xunit;

namespace SGrowing.Tests.Forms;

public class DynamicHeadlessFormAndMicroSurveyTests
{
    private readonly InAppMicroSurveyEngine _surveyEngine;
    private readonly ProgressiveProfilingEngine _progressiveEngine;
    private readonly ConditionalBranchingEvaluator _branchingEvaluator;
    private readonly FormSpamDefenseService _spamDefense;
    private readonly Guid _defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DynamicHeadlessFormAndMicroSurveyTests()
    {
        _surveyEngine = new InAppMicroSurveyEngine();
        _progressiveEngine = new ProgressiveProfilingEngine();
        _branchingEvaluator = new ConditionalBranchingEvaluator();
        _spamDefense = new FormSpamDefenseService();
    }

    #region 1. In-App Micro-Survey Targeting & Evaluation Tests

    [Fact]
    public async Task EvaluateTrigger_WhenUrlMatchesGlobPattern_ShouldTriggerMatchingSurvey()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/app/projects/settings",
            VisitCount = 5,
            DwellSeconds = 45,
            CartValue = 0,
            IsExitIntent = false
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        result.Should().NotBeNull();
        result.ShouldTrigger.Should().BeTrue();
        result.SurveyType.Should().Be(MicroSurveyType.NPS);
        result.SurveyTitle.Should().Contain("recommend SGrowing");
        result.Survey.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateTrigger_WhenUrlDoesNotMatch_ShouldReturnShouldTriggerFalse()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/public/terms-of-service",
            VisitCount = 10,
            DwellSeconds = 120,
            CartValue = 0,
            IsExitIntent = false
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        result.Should().NotBeNull();
        result.ShouldTrigger.Should().BeFalse();
        result.SurveyId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateTrigger_WhenMinVisitCountUnmet_ShouldNotTrigger()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/app/dashboard",
            VisitCount = 1, // NPS requires MinVisitCount = 3
            DwellSeconds = 60,
            CartValue = 0,
            IsExitIntent = false
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        // Should not trigger NPS because VisitCount < 3
        if (result.ShouldTrigger)
        {
            result.SurveyType.Should().NotBe(MicroSurveyType.NPS);
        }
    }

    [Fact]
    public async Task EvaluateTrigger_WhenMinDwellSecondsUnmet_ShouldNotTrigger()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/app/analytics",
            VisitCount = 10,
            DwellSeconds = 5, // NPS requires 30s
            CartValue = 0,
            IsExitIntent = false
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        result.ShouldTrigger.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateTrigger_WhenCartValueAndExitIntentMatch_ShouldTriggerExitIntentSurvey()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/checkout/step-2",
            VisitCount = 2,
            DwellSeconds = 10,
            CartValue = 120.0m, // Exit survey requires >= 50.0m
            IsExitIntent = true
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        result.Should().NotBeNull();
        result.ShouldTrigger.Should().BeTrue();
        result.SurveyType.Should().Be(MicroSurveyType.ExitIntent);
        result.SurveyTitle.Should().Contain("Before you leave");
    }

    [Fact]
    public async Task EvaluateTrigger_WhenUserSegmentMatches_ShouldTriggerEnterpriseSurvey()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/features/integrations",
            VisitCount = 6, // requires >= 5
            DwellSeconds = 20,
            UserSegment = "enterprise",
            IsExitIntent = false
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        result.Should().NotBeNull();
        result.ShouldTrigger.Should().BeTrue();
        result.SurveyType.Should().Be(MicroSurveyType.ProductFeedback);
        result.SurveyTitle.Should().Contain("Enterprise Feature");
    }

    [Fact]
    public async Task EvaluateTrigger_WhenUserSegmentDoesNotMatch_ShouldNotTriggerEnterpriseSurvey()
    {
        var request = new SurveyTriggerEvaluationRequest
        {
            TenantId = _defaultTenantId,
            CurrentPagePath = "/features/integrations",
            VisitCount = 6,
            DwellSeconds = 20,
            UserSegment = "starter_plan",
            IsExitIntent = false
        };

        var result = await _surveyEngine.EvaluateTriggerAsync(request);

        result.ShouldTrigger.Should().BeFalse();
    }

    #endregion

    #region 2. NPS Classification & Sentiment Scoring Tests

    [Theory]
    [InlineData(9, NpsCategory.Promoter)]
    [InlineData(10, NpsCategory.Promoter)]
    public void ClassifyNps_PromoterScores_ReturnsPromoter(int rating, NpsCategory expected)
    {
        var category = _surveyEngine.ClassifyNps(rating);
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData(7, NpsCategory.Passive)]
    [InlineData(8, NpsCategory.Passive)]
    public void ClassifyNps_PassiveScores_ReturnsPassive(int rating, NpsCategory expected)
    {
        var category = _surveyEngine.ClassifyNps(rating);
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, NpsCategory.Detractor)]
    [InlineData(3, NpsCategory.Detractor)]
    [InlineData(6, NpsCategory.Detractor)]
    public void ClassifyNps_DetractorScores_ReturnsDetractor(int rating, NpsCategory expected)
    {
        var category = _surveyEngine.ClassifyNps(rating);
        category.Should().Be(expected);
    }

    [Fact]
    public void ClassifyNps_NullOrOutOfBounds_ReturnsNull()
    {
        _surveyEngine.ClassifyNps(null).Should().BeNull();
        _surveyEngine.ClassifyNps(-1).Should().BeNull();
        _surveyEngine.ClassifyNps(11).Should().BeNull();
    }

    [Fact]
    public void AnalyzeSentiment_PositiveFeedback_ReturnsPositiveLabelAndScore()
    {
        string feedback = "I love this product! The interface is amazing, super fast and intuitive.";
        var (score, label) = _surveyEngine.AnalyzeSentiment(feedback);

        score.Should().BeGreaterThan(0.2);
        label.Should().Be("Positive");
    }

    [Fact]
    public void AnalyzeSentiment_NegativeFeedback_ReturnsNegativeLabelAndScore()
    {
        string feedback = "Terrible experience. The system is extremely slow, buggy, and broken.";
        var (score, label) = _surveyEngine.AnalyzeSentiment(feedback);

        score.Should().BeLessThan(-0.2);
        label.Should().Be("Negative");
    }

    [Fact]
    public void AnalyzeSentiment_NegatedPositiveFeedback_InvertsSentiment()
    {
        string feedback = "This is not good and not fast at all.";
        var (score, label) = _surveyEngine.AnalyzeSentiment(feedback);

        score.Should().BeLessThanOrEqualTo(0.0);
        label.Should().NotBe("Positive");
    }

    [Fact]
    public void AnalyzeSentiment_NeutralFeedback_ReturnsNeutralLabel()
    {
        string feedback = "I visited the settings page today at 2 PM.";
        var (score, label) = _surveyEngine.AnalyzeSentiment(feedback);

        score.Should().BeInRange(-0.15, 0.15);
        label.Should().Be("Neutral");
    }

    [Fact]
    public async Task SubmitResponse_CalculatesBothNpsAndSentiment_AndStoresResponse()
    {
        var surveyId = Guid.Parse("11111111-2222-3333-4444-555555555001"); // NPS survey
        var request = new SubmitSurveyResponseRequest
        {
            TenantId = _defaultTenantId,
            SurveyId = surveyId,
            RespondentId = "user_test_99",
            NumericRating = 10,
            TextComment = "Fantastic support team and brilliant automation engine!",
            Metadata = new() { ["browser"] = "Chrome", ["os"] = "macOS" }
        };

        var response = await _surveyEngine.SubmitResponseAsync(request);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.NpsCategory.Should().Be(NpsCategory.Promoter);
        response.SentimentLabel.Should().Be("Positive");
        response.SentimentScore.Should().BeGreaterThan(0.2);

        var allResponses = await _surveyEngine.GetResponsesAsync(surveyId);
        allResponses.Should().Contain(r => r.Id == response.ResponseId);
    }

    #endregion

    #region 3. Progressive Profiling Engine Tests

    [Fact]
    public async Task GetNextUnansweredAttribute_FreshContact_ReturnsFirstTier1Attribute()
    {
        var contactId = Guid.NewGuid(); // Fresh contact

        var nextAttr = await _progressiveEngine.GetNextUnansweredAttributeAsync(_defaultTenantId, contactId);

        nextAttr.Should().NotBeNull();
        nextAttr!.Tier.Should().Be(1);
        nextAttr.Key.Should().Be("Email");
        nextAttr.Priority.Should().Be(1);
    }

    [Fact]
    public async Task GetNextUnansweredAttribute_WhenTier1Complete_ReturnsFirstTier2Attribute()
    {
        var contactId = Guid.NewGuid();

        // Answer all Tier 1 attributes
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Email", "ceo@acme.com");
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Name", "Wile E. Coyote");
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Phone", "+1 555-0100");

        var nextAttr = await _progressiveEngine.GetNextUnansweredAttributeAsync(_defaultTenantId, contactId);

        nextAttr.Should().NotBeNull();
        nextAttr!.Tier.Should().Be(2);
        nextAttr.Key.Should().Be("CompanySize");
    }

    [Fact]
    public async Task GetNextUnansweredAttribute_WhenTier1And2Complete_ReturnsFirstTier3Attribute()
    {
        var contactId = Guid.NewGuid();

        // Answer Tier 1 & Tier 2
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Email", "user@saas.com");
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Name", "Jane Doe");
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Phone", "+1 555-0199");
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "CompanySize", "51-200");
        await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Industry", "SaaS & Software");

        var nextAttr = await _progressiveEngine.GetNextUnansweredAttributeAsync(_defaultTenantId, contactId);

        nextAttr.Should().NotBeNull();
        nextAttr!.Tier.Should().Be(3);
        nextAttr.Key.Should().Be("Budget");
    }

    [Fact]
    public async Task GetNextUnansweredAttribute_WhenAllComplete_ReturnsNull()
    {
        var completeContactId = Guid.Parse("00000000-0000-0000-0000-000000000012"); // seeded complete contact

        var nextAttr = await _progressiveEngine.GetNextUnansweredAttributeAsync(_defaultTenantId, completeContactId);

        nextAttr.Should().BeNull();

        var profile = await _progressiveEngine.GetContactProfileAsync(_defaultTenantId, completeContactId);
        profile.IsComplete.Should().BeTrue();
        profile.CompletenessPercentage.Should().Be(100.0);
    }

    [Fact]
    public async Task SaveProgressiveAnswer_UpdatesAttribute_AndCalculatesAccurateCompleteness()
    {
        var contactId = Guid.NewGuid();

        // 1st answer -> 1/8 = 12.5%
        var res1 = await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Email", "test@domain.com");
        res1.CompletenessPercentage.Should().Be(12.5);
        res1.IsProfileComplete.Should().BeFalse();
        res1.NextAttribute?.Key.Should().Be("Name");

        // 2nd answer -> 2/8 = 25.0%
        var res2 = await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "Name", "Alice Cooper");
        res2.CompletenessPercentage.Should().Be(25.0);
    }

    [Fact]
    public async Task SaveProgressiveAnswer_IsCaseInsensitiveForAttributeKeys()
    {
        var contactId = Guid.NewGuid();

        var result = await _progressiveEngine.SaveProgressiveAnswerAsync(_defaultTenantId, contactId, "eMaIL", "test@lower.com");
        result.AttributeKey.Should().Be("Email");

        var profile = await _progressiveEngine.GetContactProfileAsync(_defaultTenantId, contactId);
        profile.Attributes.Should().ContainKey("Email");
        profile.Attributes["email"].Should().Be("test@lower.com");
    }

    #endregion

    #region 4. Conditional Branching Evaluator Tests

    [Fact]
    public void Evaluate_EqualsOperator_ShowsTargetField()
    {
        var request = new BranchingEvaluationRequest
        {
            Fields =
            [
                new() { Name = "has_referral", IsRequired = true },
                new() { Name = "referral_code", IsRequired = false }
            ],
            Rules =
            [
                new()
                {
                    TargetField = "referral_code",
                    Condition = new()
                    {
                        LogicalOperator = LogicalOperator.And,
                        Rules =
                        [
                            new() { FieldName = "has_referral", Operator = RuleOperator.Equals, Value = "yes" }
                        ]
                    },
                    Action = FieldAction.Show,
                    ElseAction = FieldAction.Hide
                }
            ],
            CurrentAnswers = new() { ["has_referral"] = "yes" }
        };

        var response = _branchingEvaluator.Evaluate(request);

        response.FieldStates["referral_code"].IsVisible.Should().BeTrue();
        response.VisibleFields.Should().Contain("referral_code");
    }

    [Fact]
    public void Evaluate_NotEqualsOperator_AppliesActionWhenConditionMet()
    {
        var request = new BranchingEvaluationRequest
        {
            Fields =
            [
                new() { Name = "country", IsRequired = true },
                new() { Name = "vat_number", IsRequired = false }
            ],
            Rules =
            [
                new()
                {
                    TargetField = "vat_number",
                    Condition = new()
                    {
                        Rules =
                        [
                            new() { FieldName = "country", Operator = RuleOperator.NotEquals, Value = "US" }
                        ]
                    },
                    Action = FieldAction.Require
                }
            ],
            CurrentAnswers = new() { ["country"] = "DE" }
        };

        var response = _branchingEvaluator.Evaluate(request);

        response.FieldStates["vat_number"].IsRequired.Should().BeTrue();
        response.RequiredFields.Should().Contain("vat_number");
    }

    [Fact]
    public void Evaluate_GreaterThanOperator_RequiresTargetField()
    {
        var request = new BranchingEvaluationRequest
        {
            Fields =
            [
                new() { Name = "expected_seats", IsRequired = true },
                new() { Name = "dedicated_account_manager", IsRequired = false }
            ],
            Rules =
            [
                new()
                {
                    TargetField = "dedicated_account_manager",
                    Condition = new()
                    {
                        Rules =
                        [
                            new() { FieldName = "expected_seats", Operator = RuleOperator.GreaterThan, Value = 50 }
                        ]
                    },
                    Action = FieldAction.Require
                }
            ],
            CurrentAnswers = new() { ["expected_seats"] = 100 }
        };

        var response = _branchingEvaluator.Evaluate(request);

        response.FieldStates["dedicated_account_manager"].IsRequired.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LessThanOperator_DisablesTargetField()
    {
        var request = new BranchingEvaluationRequest
        {
            Fields =
            [
                new() { Name = "budget", IsRequired = true },
                new() { Name = "custom_sla_tier", IsRequired = false }
            ],
            Rules =
            [
                new()
                {
                    TargetField = "custom_sla_tier",
                    Condition = new()
                    {
                        Rules =
                        [
                            new() { FieldName = "budget", Operator = RuleOperator.LessThan, Value = 10000 }
                        ]
                    },
                    Action = FieldAction.Disable
                }
            ],
            CurrentAnswers = new() { ["budget"] = 5000 }
        };

        var response = _branchingEvaluator.Evaluate(request);

        response.FieldStates["custom_sla_tier"].IsDisabled.Should().BeTrue();
        response.DisabledFields.Should().Contain("custom_sla_tier");
    }

    [Fact]
    public void Evaluate_ContainsAndInOperators_EvaluatesAccurately()
    {
        var request = new BranchingEvaluationRequest
        {
            Fields =
            [
                new() { Name = "tech_stack", IsRequired = true },
                new() { Name = "cloud_provider", IsRequired = true },
                new() { Name = "aws_account_id", IsRequired = false }
            ],
            Rules =
            [
                new()
                {
                    TargetField = "aws_account_id",
                    Condition = new()
                    {
                        LogicalOperator = LogicalOperator.And,
                        Rules =
                        [
                            new() { FieldName = "tech_stack", Operator = RuleOperator.Contains, Value = ".NET" },
                            new() { FieldName = "cloud_provider", Operator = RuleOperator.In, Value = "AWS,Azure,GCP" }
                        ]
                    },
                    Action = FieldAction.Show
                }
            ],
            CurrentAnswers = new()
            {
                ["tech_stack"] = "Modern C# and .NET 10 Microservices",
                ["cloud_provider"] = "AWS"
            }
        };

        var response = _branchingEvaluator.Evaluate(request);

        response.FieldStates["aws_account_id"].IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_IsEmptyAndIsNotEmptyOperators_EvaluatesAccurately()
    {
        var emptyRule = new ConditionRule { FieldName = "notes", Operator = RuleOperator.IsEmpty };
        var notEmptyRule = new ConditionRule { FieldName = "email", Operator = RuleOperator.IsNotEmpty };

        var answers = new Dictionary<string, object?>
        {
            ["notes"] = "   ",
            ["email"] = "developer@sgrowing.com"
        };

        _branchingEvaluator.EvaluateRule(emptyRule, answers).Should().BeTrue();
        _branchingEvaluator.EvaluateRule(notEmptyRule, answers).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LogicalOrGroup_RequiresAtLeastOneConditionTrue()
    {
        var group = new RuleGroup
        {
            LogicalOperator = LogicalOperator.Or,
            Rules =
            [
                new() { FieldName = "role", Operator = RuleOperator.Equals, Value = "CEO" },
                new() { FieldName = "role", Operator = RuleOperator.Equals, Value = "CTO" }
            ]
        };

        var matchingAnswers = new Dictionary<string, object?> { ["role"] = "CTO" };
        var nonMatchingAnswers = new Dictionary<string, object?> { ["role"] = "Intern" };

        _branchingEvaluator.EvaluateRuleGroup(group, matchingAnswers).Should().BeTrue();
        _branchingEvaluator.EvaluateRuleGroup(group, nonMatchingAnswers).Should().BeFalse();
    }

    #endregion

    #region 5. Form Spam Defense Tests

    [Fact]
    public async Task CheckSpam_WhenHoneypotFieldFilled_RejectsWith422AndHoneypotReason()
    {
        var request = new FormSpamCheckRequest
        {
            ClientIp = "192.168.1.100",
            FormFields = new()
            {
                ["email"] = "victim@company.com",
                ["_hp_trap"] = "http://spam-link.bot" // bot filled hidden honeypot
            },
            SubmissionDurationSeconds = 5.0
        };

        var result = await _spamDefense.CheckSpamAsync(request);

        result.IsAllowed.Should().BeFalse();
        result.IsSpam.Should().BeTrue();
        result.DefenseTriggered.Should().Be("Honeypot");
        result.StatusCode.Should().Be(422);
        result.RiskScore.Should().Be(1.0);
    }

    [Fact]
    public async Task CheckSpam_WhenSubmissionTooFast_RejectsWith422SubmissionSpeed()
    {
        var request = new FormSpamCheckRequest
        {
            ClientIp = "192.168.1.101",
            FormFields = new() { ["name"] = "Fast Bot" },
            SubmissionDurationSeconds = 0.45 // Completed in < 1.5 seconds
        };

        var result = await _spamDefense.CheckSpamAsync(request);

        result.IsAllowed.Should().BeFalse();
        result.IsSpam.Should().BeTrue();
        result.DefenseTriggered.Should().Be("SubmissionSpeed");
        result.StatusCode.Should().Be(422);
        result.RiskScore.Should().Be(0.95);
    }

    [Fact]
    public async Task CheckSpam_WhenSubmissionDurationValid_AllowsSubmission()
    {
        var request = new FormSpamCheckRequest
        {
            ClientIp = "192.168.1.102",
            FormFields = new() { ["name"] = "Real Human" },
            SubmissionDurationSeconds = 4.2 // Completed in > 1.5s
        };

        var result = await _spamDefense.CheckSpamAsync(request);

        result.IsAllowed.Should().BeTrue();
        result.IsSpam.Should().BeFalse();
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task CheckSpam_WhenCaptchaRequiredAndMissing_RejectsWith422Captcha()
    {
        var request = new FormSpamCheckRequest
        {
            ClientIp = "192.168.1.103",
            FormFields = new() { ["name"] = "No Captcha" },
            SubmissionDurationSeconds = 3.0,
            RequireCaptcha = true,
            CaptchaToken = null
        };

        var result = await _spamDefense.CheckSpamAsync(request);

        result.IsAllowed.Should().BeFalse();
        result.DefenseTriggered.Should().Be("CaptchaVerification");
        result.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task CheckSpam_WhenCaptchaTokenInvalid_RejectsWith422Captcha()
    {
        var request = new FormSpamCheckRequest
        {
            ClientIp = "192.168.1.104",
            FormFields = new() { ["name"] = "Invalid Token" },
            SubmissionDurationSeconds = 3.0,
            RequireCaptcha = true,
            CaptchaToken = "turnstile_fail_signature"
        };

        var result = await _spamDefense.CheckSpamAsync(request);

        result.IsAllowed.Should().BeFalse();
        result.DefenseTriggered.Should().Be("CaptchaVerification");
        result.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task CheckSpam_WhenValidTurnstileTokenProvided_PassesVerification()
    {
        var request = new FormSpamCheckRequest
        {
            ClientIp = "192.168.1.105",
            FormFields = new() { ["name"] = "Valid Token" },
            SubmissionDurationSeconds = 3.0,
            RequireCaptcha = true,
            CaptchaToken = "1x00000000000000000000AA" // standard passing test token
        };

        var result = await _spamDefense.CheckSpamAsync(request);

        result.IsAllowed.Should().BeTrue();
        result.IsSpam.Should().BeFalse();
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task CheckSpam_WhenIpExceedsSlidingWindowRateLimit_Returns429TooManyRequests()
    {
        var clientIp = "10.0.0.99";
        _spamDefense.ResetRateLimit(clientIp);

        int maxAllowed = 3;

        // Fire 3 valid requests
        for (int i = 0; i < maxAllowed; i++)
        {
            var allowedResult = await _spamDefense.CheckSpamAsync(new FormSpamCheckRequest
            {
                ClientIp = clientIp,
                SubmissionDurationSeconds = 3.0,
                MaxRequestsPerMinute = maxAllowed
            });
            allowedResult.IsAllowed.Should().BeTrue();
        }

        // 4th request must be rate-limited with HTTP 429
        var throttledResult = await _spamDefense.CheckSpamAsync(new FormSpamCheckRequest
        {
            ClientIp = clientIp,
            SubmissionDurationSeconds = 3.0,
            MaxRequestsPerMinute = maxAllowed
        });

        throttledResult.IsAllowed.Should().BeFalse();
        throttledResult.DefenseTriggered.Should().Be("RateLimit");
        throttledResult.StatusCode.Should().Be(429);
        throttledResult.SpamReason.Should().ContainEquivalentOf("rate limit exceeded");

        // Clean up
        _spamDefense.ResetRateLimit(clientIp);
    }

    #endregion
}
