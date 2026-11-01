using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Marketing.Copywriting;
using Xunit;

namespace SGrowing.Tests.Marketing;

/// <summary>
/// Comprehensive test suite for Phase 33: Autonomous Multi-Channel Spintax &amp; Generative Copywriting AI Matrix.
/// Validates recursive spintax parsing, exact combinatorial permutation spaces, Shannon entropy diversity,
/// sliding window deduplication, brand voice guardrails, Maghreb dialect classification, telecom spam detection,
/// Thompson Sampling multi-armed bandits, concurrency thread-safety, and DI registrations.
/// </summary>
public class SpintaxAndCopywritingBanditTests
{
    private readonly SpintaxMatrixEngine _spintaxEngine = new();
    private readonly BrandVoiceGuardrailEngine _guardrailEngine = new();
    private readonly MultiVariantCopyBanditService _banditService = new();

    #region 1. Spintax Combinatorial Permutation Space & Parsing

    [Fact]
    public void CalculatePermutationSpace_SingleChoiceGroup_CalculatesExactOptionsCount()
    {
        string template = "{Option A|Option B|Option C}";
        long space = _spintaxEngine.CalculatePermutationSpace(template);

        space.Should().Be(3);
    }

    [Fact]
    public void CalculatePermutationSpace_NestedChoiceGroups_CalculatesCombinatorialSum()
    {
        // Option 1: "A" (1)
        // Option 2: "B {C|D}" -> 1 * 2 = 2
        // Total sum = 1 + 2 = 3
        string template = "{A|B {C|D}}";
        long space = _spintaxEngine.CalculatePermutationSpace(template);

        space.Should().Be(3);
    }

    [Fact]
    public void CalculatePermutationSpace_SequentialChoiceGroups_CalculatesProduct()
    {
        // Group 1: 2 choices
        // Group 2: 3 choices
        // Total = 2 * 3 = 6
        string template = "{Bonjour|Salut} {cher ami|client|partenaire}";
        long space = _spintaxEngine.CalculatePermutationSpace(template);

        space.Should().Be(6);
    }

    [Fact]
    public void CalculatePermutationSpace_ComplexNestedRealWorldPromptTemplate_ReturnsExact48()
    {
        // "{Bonjour|Salut|Salam {alécoum|khouya}} {cher {client|partenaire}|ami}! {Votre commande|Votre colis} est {prêt|en cours de livraison}."
        // Choice 1: "Bonjour" (1) + "Salut" (1) + "Salam " + {alécoum|khouya} (2) = 4
        // Choice 2: "cher " + {client|partenaire} (2) + "ami" (1) = 3
        // Choice 3: "Votre commande" (1) + "Votre colis" (1) = 2
        // Choice 4: "prêt" (1) + "en cours de livraison" (1) = 2
        // Total = 4 * 3 * 2 * 2 = 48
        string template = "{Bonjour|Salut|Salam {alécoum|khouya}} {cher {client|partenaire}|ami}! {Votre commande|Votre colis} est {prêt|en cours de livraison}.";
        long space = _spintaxEngine.CalculatePermutationSpace(template);

        space.Should().Be(48);
    }

    [Fact]
    public void CalculatePermutationSpace_ArbitrarilyDeepNesting_ComputesExactSpace()
    {
        // 5 options nested deeply
        string template = "{A|{B|{C|{D|E}}}}";
        long space = _spintaxEngine.CalculatePermutationSpace(template);

        space.Should().Be(5);
    }

    [Fact]
    public void CalculatePermutationSpace_SequentialDeepNestedGroups_CalculatesAccurateProduct()
    {
        // Group 1: {A|{B|C}} = 1 + 2 = 3
        // Group 2: {X|{Y|{Z|W}}} = 1 + 1 + 2 = 4
        // Total = 3 * 4 = 12
        string template = "{A|{B|C}} and {X|{Y|{Z|W}}}";
        long space = _spintaxEngine.CalculatePermutationSpace(template);

        space.Should().Be(12);
    }

    [Fact]
    public void CalculatePermutationSpace_PlainTextOrEmpty_ReturnsOne()
    {
        _spintaxEngine.CalculatePermutationSpace("").Should().Be(1);
        _spintaxEngine.CalculatePermutationSpace("   ").Should().Be(1);
        _spintaxEngine.CalculatePermutationSpace("Static message without spintax").Should().Be(1);
    }

    [Fact]
    public void GenerateMessage_SimpleSpintax_ProducesValidVariant()
    {
        string template = "{Bonjour|Salut}";
        string message = _spintaxEngine.GenerateMessage(template, seed: 1);

        message.Should().BeOneOf("Bonjour", "Salut");
    }

    [Fact]
    public void GenerateMessage_NestedSpintax_ProducesValidResolvedCombination()
    {
        string template = "{Bonjour|Salam {khouya|sahbi}}";
        string message = _spintaxEngine.GenerateMessage(template, seed: 2);

        message.Should().BeOneOf("Bonjour", "Salam khouya", "Salam sahbi");
    }

    [Fact]
    public void GenerateMessage_WithVariables_InterpolatesVariablesCorrectly()
    {
        string template = "{Bonjour|Salut} {customer_name}, votre commande #{order_id} est prête.";
        Dictionary<string, string> variables = new()
        {
            ["customer_name"] = "Farid",
            ["order_id"] = "4810"
        };

        string message = _spintaxEngine.GenerateMessage(template, seed: 3, variables: variables);

        message.Should().Contain("Farid");
        message.Should().Contain("#4810");
        message.Should().Match(m => m.StartsWith("Bonjour") || m.StartsWith("Salut"));
    }

    [Fact]
    public void GenerateMessage_WithDoubleBracedVariables_InterpolatesCorrectly()
    {
        string template = "Salam {{name}}! {{code}} est votre code.";
        Dictionary<string, string> variables = new()
        {
            ["name"] = "Amine",
            ["code"] = "982314"
        };

        string message = _spintaxEngine.GenerateMessage(template, seed: 4, variables: variables);

        message.Should().Be("Salam Amine! 982314 est votre code.");
    }

    #endregion

    #region 2. Determinism, Sliding Window Deduplication & Shannon Entropy

    [Fact]
    public void GenerateMessage_WithSameSeed_ProducesDeterministicIdenticalOutput()
    {
        string template = "{Bonjour|Salut|Salam {alécoum|khouya}} {cher {client|partenaire}|ami}! {Votre commande|Votre colis} est {prêt|en cours de livraison}.";

        string message1 = _spintaxEngine.GenerateMessage(template, seed: 1337);
        string message2 = _spintaxEngine.GenerateMessage(template, seed: 1337);
        string message3 = _spintaxEngine.GenerateMessage(template, seed: 1337);

        message1.Should().Be(message2);
        message2.Should().Be(message3);
    }

    [Fact]
    public void GenerateMessage_WithDifferentSeeds_ProducesVariedOutputs()
    {
        string template = "{Bonjour|Salut|Salam {alécoum|khouya}} {cher {client|partenaire}|ami}! {Votre commande|Votre colis} est {prêt|en cours de livraison}.";

        HashSet<string> outputs = [];
        for (int i = 0; i < 10; i++)
        {
            outputs.Add(_spintaxEngine.GenerateMessage(template, seed: i * 37 + 5));
        }

        outputs.Count.Should().BeGreaterThan(1, "different seeds should produce distinct spintax variations");
    }

    [Fact]
    public void GenerateMessage_SlidingWindowDeduplication_GuaranteesZeroDuplicatesUntilSpaceSaturated()
    {
        string templateKey = "dedup-test-campaign-" + Guid.NewGuid();
        string template = "{A|B|C|D|E} {1|2}"; // 5 * 2 = 10 permutations
        _spintaxEngine.ResetDeduplicationWindow(templateKey);

        List<string> generated = [];
        for (int i = 0; i < 10; i++)
        {
            string msg = _spintaxEngine.GenerateMessage(template, templateKey: templateKey);
            generated.Add(msg);
        }

        // All 10 initial generated messages must be completely unique
        generated.Distinct().Count().Should().Be(10, "no duplicates should be emitted in a batch until permutation space is saturated");
    }

    [Fact]
    public void GenerateMessage_PermutationSpaceSaturated_AllowsRepetitionsWithoutCrashing()
    {
        string templateKey = "saturation-test-" + Guid.NewGuid();
        string template = "{Alpha|Beta}"; // 2 permutations
        _spintaxEngine.ResetDeduplicationWindow(templateKey);

        List<string> outputs = [];
        for (int i = 0; i < 6; i++)
        {
            outputs.Add(_spintaxEngine.GenerateMessage(template, templateKey: templateKey));
        }

        outputs.Should().HaveCount(6);
        outputs.Should().OnlyContain(m => m == "Alpha" || m == "Beta");
    }

    [Fact]
    public void ResetDeduplicationWindow_ClearsHistoryAndAllowsFreshEmission()
    {
        string templateKey = "reset-test-" + Guid.NewGuid();
        string template = "{UniqueA|UniqueB}";

        _spintaxEngine.ResetDeduplicationWindow(templateKey);
        string first = _spintaxEngine.GenerateMessage(template, templateKey: templateKey);

        _spintaxEngine.ResetDeduplicationWindow(templateKey);
        // After reset, generating again should not throw and proceeds cleanly
        string second = _spintaxEngine.GenerateMessage(template, templateKey: templateKey);

        second.Should().BeOneOf("UniqueA", "UniqueB");
    }

    [Fact]
    public void CalculateEntropy_DeterministicSinglePermutation_ReturnsZero()
    {
        string template = "Plain message without variants.";
        double entropy = _spintaxEngine.CalculateEntropy(template, sampleSize: 50);

        entropy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateEntropy_UniformDistribution_ComputesShannonEntropyAboveThreshold()
    {
        // 48 permutations
        string template = "{Bonjour|Salut|Salam {alécoum|khouya}} {cher {client|partenaire}|ami}! {Votre commande|Votre colis} est {prêt|en cours de livraison}.";
        double entropy = _spintaxEngine.CalculateEntropy(template, sampleSize: 100);

        // High diversity should yield entropy >= 3.0 bits
        entropy.Should().BeGreaterThan(3.0);
    }

    [Fact]
    public void CalculateEntropy_HigherPermutationSpace_YieldsHigherEntropyThanLowerSpace()
    {
        string smallTemplate = "{A|B}"; // Max 1.0 bit
        string largeTemplate = "{A|B|C|D|E|F|G|H}"; // Max 3.0 bits

        double smallEntropy = _spintaxEngine.CalculateEntropy(smallTemplate, sampleSize: 100);
        double largeEntropy = _spintaxEngine.CalculateEntropy(largeTemplate, sampleSize: 100);

        largeEntropy.Should().BeGreaterThan(smallEntropy);
    }

    #endregion

    #region 3. Brand Voice Guardrails & Voice Scoring

    [Fact]
    public void EvaluateCopy_UltraCasualSlang_YieldsLowFormalityScore()
    {
        var request = new CopyAuditRequest(
            Text: "yo khouya saha labas wesh bro, cool ce plan !",
            Channel: "SMS",
            IsCommercial: false);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.VoiceScores.FormalityScore.Should().BeLessThanOrEqualTo(0.35);
    }

    [Fact]
    public void EvaluateCopy_EnterpriseLegal_YieldsHighFormalityScore()
    {
        var request = new CopyAuditRequest(
            Text: "Veuillez agréer, Madame, Monsieur, nos salutations distinguées. Notre équipe vous remercie de votre confiance.",
            Channel: "Email",
            IsCommercial: false);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.VoiceScores.FormalityScore.Should().BeGreaterThanOrEqualTo(0.70);
    }

    [Fact]
    public void EvaluateCopy_InformationalText_YieldsLowUrgencyScore()
    {
        var request = new CopyAuditRequest(
            Text: "Votre relevé mensuel de facturation est disponible sur votre espace client.",
            Channel: "SMS",
            IsCommercial: false);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.VoiceScores.UrgencyScore.Should().BeLessThanOrEqualTo(0.20);
    }

    [Fact]
    public void EvaluateCopy_HighUrgencyKeywords_YieldsElevatedUrgencyScore()
    {
        var request = new CopyAuditRequest(
            Text: "Urgent ! Vite, dernière chance, l'offre expire immédiatement aujourd'hui seulement !",
            Channel: "SMS",
            IsCommercial: false);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.VoiceScores.UrgencyScore.Should().BeGreaterThanOrEqualTo(0.60);
    }

    [Fact]
    public void EvaluateCopy_LengthBounds_FlagsUnderAndOverLengthCorrectly()
    {
        var shortReq = new CopyAuditRequest("Court.", MinLength: 20, MaxLength: 50, IsCommercial: false);
        var longReq = new CopyAuditRequest(new string('X', 120), MinLength: 10, MaxLength: 50, IsCommercial: false);
        var validReq = new CopyAuditRequest("Longueur tout à fait conforme.", MinLength: 10, MaxLength: 50, IsCommercial: false);

        var shortRes = _guardrailEngine.EvaluateCopy(shortReq);
        var longRes = _guardrailEngine.EvaluateCopy(longReq);
        var validRes = _guardrailEngine.EvaluateCopy(validReq);

        shortRes.VoiceScores.LengthCompliant.Should().BeFalse();
        longRes.VoiceScores.LengthCompliant.Should().BeFalse();
        validRes.VoiceScores.LengthCompliant.Should().BeTrue();
    }

    [Fact]
    public void EvaluateCopy_WithFormalityThresholds_FlagsOutOfRangeVoice()
    {
        var request = new CopyAuditRequest(
            Text: "yo khouya saha bro !",
            Channel: "SMS",
            IsCommercial: false,
            MinFormality: 0.70);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.IsApproved.Should().BeFalse();
        result.Recommendations.Should().Contain(r => r.Contains("Formality score"));
    }

    #endregion

    #region 4. Multi-Lingual Dialect Classification

    [Fact]
    public void ClassifyDialect_AlgerianDarijaLatinArabizi_ClassifiesAccurately()
    {
        string text = "Saha khouya, kayen commande jdida bghit nchoufha, barak allah fik.";
        var classification = _guardrailEngine.ClassifyDialect(text);

        classification.PrimaryDialect.Should().Be("AlgerianDarija");
        classification.DetectedKeywords.Should().Contain(["saha", "khouya", "kayen", "bghit", "barak allah fik"]);
        classification.Confidence.Should().BeGreaterThan(0.70);
    }

    [Fact]
    public void ClassifyDialect_AlgerianDarijaArabicScript_ClassifiesAccurately()
    {
        string text = "صحا خويا كاين منتج شباب بغيت نطلب ضرك بارك الله فيك";
        var classification = _guardrailEngine.ClassifyDialect(text);

        classification.PrimaryDialect.Should().Be("AlgerianDarija");
        classification.DetectedKeywords.Should().Contain(["صحا", "خويا", "كاين", "بغيت", "ضرك", "بارك الله فيك"]);
        classification.Confidence.Should().BeGreaterThan(0.70);
    }

    [Fact]
    public void ClassifyDialect_FrenchCommerce_ClassifiesAccurately()
    {
        string text = "Votre commande est expédiée. Veuillez vérifier votre panier et votre facture.";
        var classification = _guardrailEngine.ClassifyDialect(text);

        classification.PrimaryDialect.Should().Be("French");
        classification.DetectedKeywords.Should().Contain(["commande", "expédié", "veuillez", "panier", "facture"]);
        classification.Confidence.Should().BeGreaterThan(0.60);
    }

    [Fact]
    public void ClassifyDialect_ModernStandardArabicCommerce_ClassifiesAccurately()
    {
        string text = "تم تأكيد طلبك بنجاح، شكراً لتعاملكم مع خدمة العملاء. سيتم شحن وتوصيل الطلب قريباً.";
        var classification = _guardrailEngine.ClassifyDialect(text);

        classification.PrimaryDialect.Should().Be("ModernStandardArabic");
        classification.DetectedKeywords.Should().Contain(["تم تأكيد", "طلبك", "شكراً لتعاملكم", "خدمة العملاء", "شحن", "توصيل"]);
        classification.Confidence.Should().BeGreaterThan(0.70);
    }

    [Fact]
    public void ClassifyDialect_EnglishCommerce_ClassifiesAccurately()
    {
        string text = "Your order delivery and shipping tracking are confirmed. Review your invoice.";
        var classification = _guardrailEngine.ClassifyDialect(text);

        classification.PrimaryDialect.Should().Be("English");
        classification.DetectedKeywords.Should().Contain(["order", "delivery", "shipping", "tracking", "confirmed", "invoice"]);
        classification.Confidence.Should().BeGreaterThan(0.60);
    }

    #endregion

    #region 5. Telecom Spam Triggers & Regulatory Compliance

    [Fact]
    public void DetectSpamTriggers_ProhibitedSpamPhrase_FlagsSpamSuspected()
    {
        string text = "Devenez RICHE EN 24H avec notre méthode d'ARGENT FACILE ! C'est 100% GRATUIT.";
        var result = _guardrailEngine.DetectSpamTriggers(text);

        result.IsSpamSuspected.Should().BeTrue();
        result.MatchedSpamPhrases.Should().Contain(["RICHE EN 24H", "ARGENT FACILE", "100% GRATUIT"]);
    }

    [Fact]
    public void DetectSpamTriggers_ExcessiveAllCapsScreams_FlagsViolation()
    {
        string text = "OFFRE EXCEPTIONNELLE PROFITEZ EN VITE MAINTENANT POUR GAGNER";
        var result = _guardrailEngine.DetectSpamTriggers(text);

        result.IsSpamSuspected.Should().BeTrue();
        result.CapsRatio.Should().BeGreaterThan(0.40);
        result.Violations.Should().Contain(v => v.Contains("ALL CAPS"));
    }

    [Fact]
    public void DetectSpamTriggers_ExcessiveExclamationPoints_FlagsViolation()
    {
        string text = "Super offre disponible !!! Ne ratez pas ça !!!";
        var result = _guardrailEngine.DetectSpamTriggers(text);

        result.IsSpamSuspected.Should().BeTrue();
        result.ExclamationCount.Should().BeGreaterThanOrEqualTo(3);
        result.Violations.Should().Contain(v => v.Contains("exclamation marks"));
    }

    [Fact]
    public void EvaluateCopy_CommercialSmsWithOptOut_IsCompliantAndApproved()
    {
        var request = new CopyAuditRequest(
            Text: "Votre commande est disponible en boutique. STOP SMS",
            Channel: "SMS",
            IsCommercial: true);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.Compliance.IsCompliant.Should().BeTrue();
        result.Compliance.HasOptOut.Should().BeTrue();
        result.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void EvaluateCopy_CommercialSmsWithoutOptOut_FailsCompliance()
    {
        var request = new CopyAuditRequest(
            Text: "Votre commande est disponible en boutique.",
            Channel: "SMS",
            IsCommercial: true);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.Compliance.IsCompliant.Should().BeFalse();
        result.Compliance.HasOptOut.Should().BeFalse();
        result.IsApproved.Should().BeFalse();
        result.Recommendations.Should().Contain(r => r.Contains("STOP SMS"));
    }

    [Fact]
    public void EvaluateCopy_NonCommercialSmsWithoutOptOut_RemainsCompliant()
    {
        var request = new CopyAuditRequest(
            Text: "Votre code de confirmation est 481920.",
            Channel: "SMS",
            IsCommercial: false);

        var result = _guardrailEngine.EvaluateCopy(request);

        result.Compliance.IsCompliant.Should().BeTrue();
        result.Compliance.RequiresOptOut.Should().BeFalse();
        result.IsApproved.Should().BeTrue();
    }

    #endregion

    #region 6. Thompson Sampling Multi-Armed Bandit

    [Fact]
    public void SelectBestVariant_PicksFromAvailableCandidates()
    {
        string campaignId = "camp-" + Guid.NewGuid();
        string[] candidates = ["variant_A", "variant_B", "variant_C"];

        var selection = _banditService.SelectBestVariant(campaignId, candidates);

        selection.CampaignId.Should().Be(campaignId);
        candidates.Should().Contain(selection.SelectedVariantId);
        selection.SampledTheta.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void RecordFeedback_Conversions_IncreaseAlphaAndShiftExpectedValueUp()
    {
        string campaignId = "camp-" + Guid.NewGuid();
        string variantId = "var_promo";

        // Initial uninformative prior Beta(1, 1) -> E[X] = 0.5
        var initialSelect = _banditService.SelectBestVariant(campaignId, [variantId]);
        initialSelect.ExpectedValue.Should().Be(0.5);

        // Record 5 conversions (+1.0 each)
        for (int i = 0; i < 5; i++)
        {
            _banditService.RecordFeedback(
                campaignId,
                variantId,
                new CopyInteractionEvent("Converted", DateTime.UtcNow, Weight: 1.0));
        }

        var stats = _banditService.GetVariantPerformance(campaignId);
        var metric = stats.Variants.First(v => v.VariantId == variantId);

        metric.Alpha.Should().Be(6.0); // 1.0 prior + 5.0
        metric.Beta.Should().Be(1.0);  // 1.0 prior
        metric.ExpectedValue.Should().BeApproximately(6.0 / 7.0, 0.01);
    }

    [Fact]
    public void RecordFeedback_Unsubscribes_IncreaseBetaAndShiftExpectedValueDown()
    {
        string campaignId = "camp-" + Guid.NewGuid();
        string variantId = "var_spammy";

        _banditService.SelectBestVariant(campaignId, [variantId]);

        // Record 5 unsubscribes (+2.0 each)
        for (int i = 0; i < 5; i++)
        {
            _banditService.RecordFeedback(
                campaignId,
                variantId,
                new CopyInteractionEvent("Unsubscribed", DateTime.UtcNow, Weight: 1.0));
        }

        var stats = _banditService.GetVariantPerformance(campaignId);
        var metric = stats.Variants.First(v => v.VariantId == variantId);

        metric.Alpha.Should().Be(1.0); // 1.0 prior
        metric.Beta.Should().Be(11.0); // 1.0 prior + (5 * 2.0)
        metric.ExpectedValue.Should().BeApproximately(1.0 / 12.0, 0.01);
    }

    [Fact]
    public void GetVariantPerformance_ComputesCorrectExpectedValueAndEmpiricalRegret()
    {
        string campaignId = "camp-" + Guid.NewGuid();
        string goodVariant = "good_variant";
        string poorVariant = "poor_variant";

        _banditService.SelectBestVariant(campaignId, [goodVariant, poorVariant]);

        // Boost good variant
        _banditService.RecordFeedback(campaignId, goodVariant, new CopyInteractionEvent("Converted", DateTime.UtcNow));
        _banditService.RecordFeedback(campaignId, goodVariant, new CopyInteractionEvent("Converted", DateTime.UtcNow));

        // Penalize poor variant
        _banditService.RecordFeedback(campaignId, poorVariant, new CopyInteractionEvent("Unsubscribed", DateTime.UtcNow));

        var stats = _banditService.GetVariantPerformance(campaignId);

        stats.BestVariantId.Should().Be(goodVariant);
        stats.Variants.First(v => v.VariantId == goodVariant).Regret.Should().Be(0.0);
        stats.Variants.First(v => v.VariantId == poorVariant).Regret.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void ThompsonSampling_OverRepeatedTrials_PicksHighPerformingArmSignificantlyMoreOften()
    {
        string campaignId = "camp-" + Guid.NewGuid();
        string winningArm = "winner";
        string losingArm = "loser";

        _banditService.SelectBestVariant(campaignId, [winningArm, losingArm]);

        // Train winning arm with 30 conversions
        for (int i = 0; i < 30; i++)
        {
            _banditService.RecordFeedback(campaignId, winningArm, new CopyInteractionEvent("Converted", DateTime.UtcNow));
        }

        // Train losing arm with 30 unsubscribes
        for (int i = 0; i < 30; i++)
        {
            _banditService.RecordFeedback(campaignId, losingArm, new CopyInteractionEvent("Unsubscribed", DateTime.UtcNow));
        }

        int winnerCount = 0;
        int trials = 100;

        for (int i = 0; i < trials; i++)
        {
            var selection = _banditService.SelectBestVariant(campaignId, [winningArm, losingArm]);
            if (selection.SelectedVariantId == winningArm)
            {
                winnerCount++;
            }
        }

        // Thompson sampling should strongly exploit the dominant winning arm
        winnerCount.Should().BeGreaterThanOrEqualTo(90, "the winning arm should be selected > 90% of the time");
    }

    #endregion

    #region 7. Concurrency & ServiceCollection Registration (Rule 5 Compliance)

    [Fact]
    public async Task SpintaxMatrixEngine_ConcurrentAccess_IsThreadSafe()
    {
        string template = "{Bonjour|Salut|Salam} {mon cher ami|partenaire} {1|2|3|4|5}";
        List<Task> tasks = [];
        ConcurrentBag<string> bag = [];

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 20; j++)
                {
                    string msg = _spintaxEngine.GenerateMessage(template);
                    bag.Add(msg);
                }
            }));
        }

        await Task.WhenAll(tasks);

        bag.Count.Should().Be(1000);
        bag.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m));
    }

    [Fact]
    public async Task MultiVariantCopyBanditService_ConcurrentAccess_IsThreadSafe()
    {
        string campaignId = "concurrent-camp-" + Guid.NewGuid();
        string[] candidates = ["arm1", "arm2", "arm3", "arm4"];
        List<Task> tasks = [];

        for (int i = 0; i < 40; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 25; j++)
                {
                    var selection = _banditService.SelectBestVariant(campaignId, candidates);
                    _banditService.RecordFeedback(
                        campaignId,
                        selection.SelectedVariantId,
                        new CopyInteractionEvent("Delivered", DateTime.UtcNow));
                }
            }));
        }

        await Task.WhenAll(tasks);

        var perf = _banditService.GetVariantPerformance(campaignId);
        perf.Variants.Should().HaveCount(4);
        perf.TotalInteractions.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddCopywritingServices_RegistersRequiredServicesWithCorrectLifetimes()
    {
        // Assert on ServiceCollection descriptors directly without calling BuildServiceProvider (Rule 5)
        var services = new ServiceCollection();
        services.AddCopywritingServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISpintaxMatrixEngine) &&
            d.ImplementationType == typeof(SpintaxMatrixEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IBrandVoiceGuardrailEngine) &&
            d.ImplementationType == typeof(BrandVoiceGuardrailEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IMultiVariantCopyBanditService) &&
            d.ImplementationType == typeof(MultiVariantCopyBanditService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
