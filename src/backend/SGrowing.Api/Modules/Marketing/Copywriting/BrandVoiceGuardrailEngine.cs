using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Marketing.Copywriting;

/// <summary>
/// Interface for brand voice guardrails, dialect classification, spam filter detection,
/// and telecom regulatory compliance.
/// </summary>
public interface IBrandVoiceGuardrailEngine
{
    /// <summary>
    /// Audits copywriting text across voice dimensions, dialect, spam triggers, and regulatory compliance.
    /// </summary>
    CopyAuditResult EvaluateCopy(CopyAuditRequest request);

    /// <summary>
    /// Classifies the text dialect (Algerian Darija Latin/Arabic, French, MSA, English) and returns confidence scores.
    /// </summary>
    DialectClassification ClassifyDialect(string text);

    /// <summary>
    /// Detects prohibited telecom spam phrases, excessive ALL CAPS screams, and excessive exclamation points.
    /// </summary>
    SpamAuditResult DetectSpamTriggers(string text);
}

/// <summary>
/// Production engine evaluating copy against brand style rules, Maghreb dialect nuances,
/// telecom spam filters, and telecom regulatory compliance.
/// </summary>
public sealed class BrandVoiceGuardrailEngine : IBrandVoiceGuardrailEngine
{
    private static readonly string[] ProhibitedSpamPhrases =
    [
        "100% GRATUIT",
        "100% FREE",
        "RICHE EN 24H",
        "ARGENT FACILE",
        "EASY MONEY",
        "GIVEAWAY IMMÉDIAT",
        "GIVEAWAY IMMEDIAT",
        "GAGNER DE L'ARGENT",
        "MILLIONNAIRE",
        "FAST CASH",
        "DEVENIR RICHE",
        "GET RICH"
    ];

    private static readonly string[] OptOutSignatures =
    [
        "STOP SMS",
        "STOP AU 38000",
        "STOP",
        "DÉSABONNER",
        "DESABONNER",
        "DÉSABONNEMENT",
        "DESABONNEMENT",
        "UNSUBSCRIBE",
        "OPT-OUT",
        "OPTOUT",
        "إلغاء الاشتراك"
    ];

    // Multi-lingual dialect dictionaries
    private static readonly string[] DarijaLatinKeywords =
    [
        "khouya", "saha", "barak allah fik", "dork", "kayen", "makanch", "bghit",
        "chouf", "wach", "hbibna", "labas", "rani", "chkoun", "koulchi", "ya3tik", "mzyan"
    ];

    private static readonly string[] DarijaArabicKeywords =
    [
        "خويا", "صحا", "بارك الله فيك", "ضرك", "كاين", "مكانش", "بغيت",
        "شوف", "واش", "حبيبنا", "لاباس", "راني", "شكون", "كلشي", "يعطيك", "مزيان"
    ];

    private static readonly string[] FrenchCommerceKeywords =
    [
        "livraison", "commande", "veuillez", "cordialement", "remise", "facture",
        "panier", "solde", "garantie", "remercie", "frais", "expédié", "service client",
        "merci", "achat", "colis", "boutique", "promotion", "offert"
    ];

    private static readonly string[] MsaCommerceKeywords =
    [
        "طلبك", "توصيل", "شحن", "فاتورة", "شكراً لتعاملكم", "يرجى", "العميل",
        "الموقر", "خصم", "الطلب", "تم تأكيد", "خدمة العملاء", "مرحباً بك", "سلة المشتريات"
    ];

    private static readonly string[] EnglishCommerceKeywords =
    [
        "order", "shipping", "delivery", "checkout", "discount", "invoice", "customer",
        "confirmed", "tracking", "package", "support", "cart", "purchase", "store"
    ];

    // Formality indicators
    private static readonly string[] FormalKeywords =
    [
        "veuillez", "cordialement", "monsieur", "madame", "nous vous prions", "agréer",
        "salutations", "respectueusement", "notre équipe", "conformément",
        "يرجى", "الموقر", "شكراً لتعاملكم", "حضرة", "السيد", "يسرنا", "عناية",
        "sincerely", "kind regards", "please be advised", "respectfully", "dear customer", "we appreciate"
    ];

    private static readonly string[] CasualSlangKeywords =
    [
        "khouya", "saha", "salut", "coucou", "yo", "hey", "wesh", "bro", "cool", "top",
        "خويا", "صحا", "حبيبنا", "يا خو", "عسلامة"
    ];

    // Urgency indicators
    private static readonly string[] UrgencyKeywords =
    [
        "urgent", "vite", "immédiat", "maintenant", "dernière chance", "expire", "dépêchez",
        "aujourd'hui seulement", "plus que", "stock limité", "immédiatement", "derniers jours",
        "now", "hurry", "last chance", "expires", "limited time", "today only", "fast", "act now",
        "عاجل", "سارع", "الآن", "الفرصة الأخيرة", "ينتهي اليوم", "كمية محدودة"
    ];

    /// <inheritdoc />
    public CopyAuditResult EvaluateCopy(CopyAuditRequest request)
    {
        string text = request.Text ?? string.Empty;
        var recommendations = new List<string>();

        // 1. Voice dimension scoring
        var voiceScores = CalculateVoiceScores(text, request.MinLength, request.MaxLength);
        if (!voiceScores.LengthCompliant && voiceScores.LengthViolation is not null)
        {
            recommendations.Add(voiceScores.LengthViolation);
        }

        if (request.MinFormality.HasValue && voiceScores.FormalityScore < request.MinFormality.Value)
        {
            recommendations.Add($"Formality score ({voiceScores.FormalityScore:F2}) is below requested minimum of {request.MinFormality.Value:F2}. Consider using more formal enterprise address.");
        }

        if (request.MaxFormality.HasValue && voiceScores.FormalityScore > request.MaxFormality.Value)
        {
            recommendations.Add($"Formality score ({voiceScores.FormalityScore:F2}) exceeds requested maximum of {request.MaxFormality.Value:F2}. Tone down formal vocabulary.");
        }

        if (request.MaxUrgency.HasValue && voiceScores.UrgencyScore > request.MaxUrgency.Value)
        {
            recommendations.Add($"Urgency score ({voiceScores.UrgencyScore:F2}) exceeds requested maximum of {request.MaxUrgency.Value:F2}. Soften urgent callouts to avoid subscriber fatigue.");
        }

        // 2. Dialect classification
        var dialect = ClassifyDialect(text);

        // 3. Telecom spam triggers
        var spamAudit = DetectSpamTriggers(text);
        foreach (string violation in spamAudit.Violations)
        {
            recommendations.Add(violation);
        }

        // 4. Regulatory compliance (opt-out footer for SMS/WhatsApp commercial messages)
        var compliance = CheckRegulatoryCompliance(text, request.Channel, request.IsCommercial);
        if (!compliance.IsCompliant && compliance.ViolationDetails is not null)
        {
            recommendations.Add(compliance.ViolationDetails);
        }

        // Approval determination
        bool isApproved = !spamAudit.IsSpamSuspected
            && compliance.IsCompliant
            && voiceScores.LengthCompliant
            && (!request.MinFormality.HasValue || voiceScores.FormalityScore >= request.MinFormality.Value)
            && (!request.MaxFormality.HasValue || voiceScores.FormalityScore <= request.MaxFormality.Value)
            && (!request.MaxUrgency.HasValue || voiceScores.UrgencyScore <= request.MaxUrgency.Value);

        return new CopyAuditResult(
            IsApproved: isApproved,
            VoiceScores: voiceScores,
            Dialect: dialect,
            SpamAudit: spamAudit,
            Compliance: compliance,
            Recommendations: recommendations);
    }

    /// <inheritdoc />
    public DialectClassification ClassifyDialect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DialectClassification("Unknown", 0.0, [], new Dictionary<string, double>());
        }

        string normalized = text.ToLowerInvariant();
        List<string> detected = [];

        int darijaLatinCount = CountAndCollectMatches(normalized, DarijaLatinKeywords, detected);
        int darijaArabicCount = CountAndCollectMatches(text, DarijaArabicKeywords, detected);
        int frenchCount = CountAndCollectMatches(normalized, FrenchCommerceKeywords, detected);
        int msaCount = CountAndCollectMatches(text, MsaCommerceKeywords, detected);
        int englishCount = CountAndCollectMatches(normalized, EnglishCommerceKeywords, detected);

        int totalDarija = darijaLatinCount + darijaArabicCount;
        bool hasArabicScript = Regex.IsMatch(text, @"\p{IsArabic}");

        Dictionary<string, double> scores = new()
        {
            ["AlgerianDarija"] = totalDarija,
            ["French"] = frenchCount,
            ["ModernStandardArabic"] = msaCount,
            ["English"] = englishCount
        };

        string primaryDialect;
        double confidence;

        if (hasArabicScript)
        {
            if (darijaArabicCount > 0)
            {
                primaryDialect = "AlgerianDarija";
                confidence = Math.Min(1.0, 0.65 + (0.1 * darijaArabicCount));
            }
            else if (msaCount > 0)
            {
                primaryDialect = "ModernStandardArabic";
                confidence = Math.Min(1.0, 0.70 + (0.1 * msaCount));
            }
            else
            {
                primaryDialect = "ModernStandardArabic";
                confidence = 0.50;
            }
        }
        else
        {
            if (darijaLatinCount > 0)
            {
                primaryDialect = "AlgerianDarija";
                confidence = Math.Min(1.0, 0.70 + (0.1 * darijaLatinCount));
            }
            else if (frenchCount >= englishCount && frenchCount > 0)
            {
                primaryDialect = "French";
                confidence = Math.Min(1.0, 0.60 + (0.1 * frenchCount));
            }
            else if (englishCount > 0)
            {
                primaryDialect = "English";
                confidence = Math.Min(1.0, 0.60 + (0.1 * englishCount));
            }
            else
            {
                // Fallback detection based on common French/English stop words
                if (Regex.IsMatch(normalized, @"\b(le|la|les|un|une|des|est|votre|vos|pour|avec)\b"))
                {
                    primaryDialect = "French";
                    confidence = 0.45;
                }
                else
                {
                    primaryDialect = "English";
                    confidence = 0.40;
                }
            }
        }

        return new DialectClassification(
            PrimaryDialect: primaryDialect,
            Confidence: Math.Round(confidence, 2),
            DetectedKeywords: detected,
            DialectScores: scores);
    }

    /// <inheritdoc />
    public SpamAuditResult DetectSpamTriggers(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SpamAuditResult(false, [], 0.0, 0, []);
        }

        List<string> violations = [];
        List<string> matchedPhrases = [];

        // 1. Prohibited spam phrases
        foreach (string phrase in ProhibitedSpamPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                matchedPhrases.Add(phrase);
            }
        }

        if (matchedPhrases.Count > 0)
        {
            violations.Add($"Contains prohibited telecom spam phrases: {string.Join(", ", matchedPhrases)}.");
        }

        // 2. ALL CAPS screams (> 40% uppercase words with length >= 2)
        var words = Regex.Matches(text, @"\b[\p{L}\p{N}']+\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(w => w.Length >= 2)
            .ToList();

        int uppercaseWords = 0;
        foreach (string word in words)
        {
            if (word.Any(char.IsLetter) && word.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                uppercaseWords++;
            }
        }

        double capsRatio = words.Count > 0 ? (double)uppercaseWords / words.Count : 0.0;
        if (words.Count >= 3 && capsRatio > 0.40)
        {
            violations.Add($"Excessive ALL CAPS text detected ({capsRatio:P0} of words are uppercase). Exceeds telecom filter threshold of 40%.");
        }

        // 3. Excessive exclamation points (>= 3 '!!!' or total >= 3)
        int exclamationCount = text.Count(c => c == '!');
        if (text.Contains("!!!", StringComparison.Ordinal) || exclamationCount >= 3)
        {
            violations.Add($"Excessive exclamation marks detected ({exclamationCount} occurrences). Prohibited to prevent telecom spam penalization.");
        }

        bool isSpamSuspected = violations.Count > 0;

        return new SpamAuditResult(
            IsSpamSuspected: isSpamSuspected,
            Violations: violations,
            CapsRatio: Math.Round(capsRatio, 2),
            ExclamationCount: exclamationCount,
            MatchedSpamPhrases: matchedPhrases);
    }

    private static VoiceScoreResult CalculateVoiceScores(string text, int? minLength, int? maxLength)
    {
        int length = text.Length;
        bool lengthCompliant = true;
        string? violation = null;

        if (minLength.HasValue && length < minLength.Value)
        {
            lengthCompliant = false;
            violation = $"Message length ({length} chars) is below mandatory minimum of {minLength.Value} chars.";
        }
        else if (maxLength.HasValue && length > maxLength.Value)
        {
            lengthCompliant = false;
            violation = $"Message length ({length} chars) exceeds maximum permissible threshold of {maxLength.Value} chars.";
        }

        string normalized = text.ToLowerInvariant();

        // Formality scoring: baseline 0.50
        double formality = 0.50;
        foreach (string kw in FormalKeywords)
        {
            if (normalized.Contains(kw, StringComparison.OrdinalIgnoreCase) || text.Contains(kw, StringComparison.Ordinal))
            {
                formality += 0.12;
            }
        }

        foreach (string slang in CasualSlangKeywords)
        {
            if (normalized.Contains(slang, StringComparison.OrdinalIgnoreCase) || text.Contains(slang, StringComparison.Ordinal))
            {
                formality -= 0.15;
            }
        }

        formality = Math.Clamp(formality, 0.0, 1.0);

        // Urgency scoring: baseline 0.0
        double urgency = 0.0;
        foreach (string ukw in UrgencyKeywords)
        {
            if (normalized.Contains(ukw, StringComparison.OrdinalIgnoreCase) || text.Contains(ukw, StringComparison.Ordinal))
            {
                urgency += 0.28;
            }
        }

        int exclamationCount = text.Count(c => c == '!');
        urgency += exclamationCount * 0.08;
        urgency = Math.Clamp(urgency, 0.0, 1.0);

        return new VoiceScoreResult(
            FormalityScore: Math.Round(formality, 2),
            UrgencyScore: Math.Round(urgency, 2),
            CharacterCount: length,
            LengthCompliant: lengthCompliant,
            LengthViolation: violation);
    }

    private static RegulatoryComplianceResult CheckRegulatoryCompliance(string text, string channel, bool isCommercial)
    {
        bool isSmsOrWhatsApp = channel.Equals("SMS", StringComparison.OrdinalIgnoreCase)
                            || channel.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase);

        if (!isCommercial || !isSmsOrWhatsApp)
        {
            return new RegulatoryComplianceResult(
                IsCompliant: true,
                RequiresOptOut: false,
                HasOptOut: false,
                ViolationDetails: null);
        }

        bool hasOptOut = false;
        foreach (string sig in OptOutSignatures)
        {
            if (text.Contains(sig, StringComparison.OrdinalIgnoreCase))
            {
                hasOptOut = true;
                break;
            }
        }

        // Also check if text has an opt-out link like http(s)://...unsubscribe or ...stop
        if (!hasOptOut && Regex.IsMatch(text, @"https?://[^\s]+(?:unsubscribe|desabonner|stop)", RegexOptions.IgnoreCase))
        {
            hasOptOut = true;
        }

        if (hasOptOut)
        {
            return new RegulatoryComplianceResult(
                IsCompliant: true,
                RequiresOptOut: true,
                HasOptOut: true,
                ViolationDetails: null);
        }

        return new RegulatoryComplianceResult(
            IsCompliant: false,
            RequiresOptOut: true,
            HasOptOut: false,
            ViolationDetails: "Commercial SMS/WhatsApp copywriting requires a mandatory opt-out mechanism (e.g. 'STOP SMS', 'STOP au 38000', or unsubscribe link).");
    }

    private static int CountAndCollectMatches(string text, string[] keywords, List<string> collected)
    {
        int matches = 0;
        foreach (string kw in keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                matches++;
                if (!collected.Contains(kw, StringComparer.OrdinalIgnoreCase))
                {
                    collected.Add(kw);
                }
            }
        }
        return matches;
    }
}
