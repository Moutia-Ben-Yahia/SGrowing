using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.CRM.SupportSwarm;

/// <summary>
/// Defines the autonomous resolution macro synthesis and feedback tracking interface.
/// </summary>
public interface IAutonomousMacroSynthesizerService
{
    AutoSynthesizedMacro SynthesizeMacroFromResolution(TicketCategory category, string title, string resolutionText, double confidence = 0.90);
    IReadOnlyList<AutoSynthesizedMacro> GetRecommendedMacros(TicketCategory category);
    AutoSynthesizedMacro RecordMacroUsage(string macroId, bool resolvedIssue);
    AutoSynthesizedMacro? GetMacro(string macroId);
    IReadOnlyList<AutoSynthesizedMacro> GetAllMacros();
}

/// <summary>
/// Autonomous macro synthesizer.
/// Learns reusable resolution macros and canned responses from high-satisfaction ticket closures,
/// automatically parameterizing variable entity placeholders ({CustomerName}, {OrderNumber}, {CarrierName}, {TrackingNumber}),
/// and iteratively optimizing conversion success rates through reinforcement feedback.
/// </summary>
public sealed partial class AutonomousMacroSynthesizerService : IAutonomousMacroSynthesizerService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AutoSynthesizedMacro> _catalog = [];

    public AutonomousMacroSynthesizerService()
    {
        SeedDefaultMacros();
    }

    /// <summary>
    /// Synthesizes a new macro from raw agent/supervisor resolution text, automatically extracting variable placeholders.
    /// </summary>
    public AutoSynthesizedMacro SynthesizeMacroFromResolution(
        TicketCategory category,
        string title,
        string resolutionText,
        double confidence = 0.90)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionText);

        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence score must be within [0.0, 1.0].");
        }

        var parameterizedText = ParameterizeResolutionText(resolutionText);
        var macroId = $"MCR-{Guid.NewGuid():N}";

        lock (_lock)
        {
            var macro = new AutoSynthesizedMacro(
                MacroId: macroId,
                Category: category,
                Title: title.Trim(),
                TemplateText: parameterizedText,
                ConfidenceScore: Math.Round(confidence, 4),
                ConversionSuccessRate: 1.0,
                UsageCount: 1,
                CreatedAtUtc: DateTime.UtcNow,
                SuccessfulResolutionsCount: 1,
                LastUsedAtUtc: DateTime.UtcNow
            );

            _catalog[macroId] = macro;
            return macro;
        }
    }

    /// <summary>
    /// Retrieves recommended macros for a category ordered by conversion success rate and confidence.
    /// </summary>
    public IReadOnlyList<AutoSynthesizedMacro> GetRecommendedMacros(TicketCategory category)
    {
        lock (_lock)
        {
            return [.. _catalog.Values
                .Where(m => m.Category == category)
                .OrderByDescending(m => m.ConversionSuccessRate)
                .ThenByDescending(m => m.ConfidenceScore)
                .ThenByDescending(m => m.UsageCount)];
        }
    }

    /// <summary>
    /// Records an outcome feedback event for a macro and recalculates its conversion success rate.
    /// </summary>
    public AutoSynthesizedMacro RecordMacroUsage(string macroId, bool resolvedIssue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(macroId);

        lock (_lock)
        {
            if (!_catalog.TryGetValue(macroId, out var existing))
            {
                throw new KeyNotFoundException($"Macro '{macroId}' was not found in catalog.");
            }

            var newUsageCount = existing.UsageCount + 1;
            var newSuccessCount = existing.SuccessfulResolutionsCount + (resolvedIssue ? 1 : 0);
            var newSuccessRate = Math.Round((double)newSuccessCount / newUsageCount, 4);

            var updated = existing with
            {
                UsageCount = newUsageCount,
                SuccessfulResolutionsCount = newSuccessCount,
                ConversionSuccessRate = newSuccessRate,
                LastUsedAtUtc = DateTime.UtcNow
            };

            _catalog[macroId] = updated;
            return updated;
        }
    }

    /// <summary>
    /// Retrieves a macro by its ID.
    /// </summary>
    public AutoSynthesizedMacro? GetMacro(string macroId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(macroId);

        lock (_lock)
        {
            return _catalog.GetValueOrDefault(macroId);
        }
    }

    /// <summary>
    /// Retrieves all synthesized macros in the catalog.
    /// </summary>
    public IReadOnlyList<AutoSynthesizedMacro> GetAllMacros()
    {
        lock (_lock)
        {
            return [.. _catalog.Values.OrderByDescending(m => m.ConversionSuccessRate)];
        }
    }

    /// <summary>
    /// Substitutes named customer mentions, order IDs, Algerian carriers, and tracking codes with standardized macro placeholders.
    /// </summary>
    public static string ParameterizeResolutionText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return rawText;
        }

        var text = rawText;

        // 1. Order Number replacement: CMD-*, ORD-*, #12345, etc.
        text = Regex.Replace(text, @"\b(CMD|ORD|ORDER|SO|COMMANDE)-[A-Za-z0-9-]+\b", "{OrderNumber}", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"#\d{4,8}\b", "{OrderNumber}");

        // 2. Tracking Number replacement: TRK-*, YAL-*, etc.
        text = Regex.Replace(text, @"\b(TRK|TRACK|YAL)-[A-Za-z0-9-]+\b", "{TrackingNumber}", RegexOptions.IgnoreCase);

        // 3. Carrier Name replacement: Yalidine, ZR Express, Maystro Delivery, Kazi Tour, DHL, FedEx, EMS
        text = Regex.Replace(
            text,
            @"(?i)\b(Yalidine(\s+Express)?|ZR\s+Express|Maystro(\s+Delivery)?|Kazi\s+Tour|EMS(\s+Alg[ée]rie)?|DHL|FedEx)\b",
            "{CarrierName}");

        // 4. Customer Name replacement: Salutations followed by person names
        text = Regex.Replace(
            text,
            @"(?i)\b(Bonjour|Bonsoir|Cher\s+client|Chère\s+cliente|Hello|Hi|Salutations)\s+(M\.|Mme\.|Monsieur|Madame)?\s*([A-ZÀ-ÿ][a-zà-ÿ]+)",
            "$1 {CustomerName}");

        return text;
    }

    private void SeedDefaultMacros()
    {
        var seedDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        var seeds = new[]
        {
            new AutoSynthesizedMacro(
                MacroId: "MCR-SEED-LOGISTICS-01",
                Category: TicketCategory.OrderDeliveryLogistics,
                Title: "Livraison en cours - Retard transporteur résolu",
                TemplateText: "Bonjour {CustomerName}, votre commande {OrderNumber} expédiée par {CarrierName} fait l'objet d'un suivi prioritaire sous la référence {TrackingNumber}.",
                ConfidenceScore: 0.95,
                ConversionSuccessRate: 0.92,
                UsageCount: 25,
                CreatedAtUtc: seedDate,
                SuccessfulResolutionsCount: 23,
                LastUsedAtUtc: seedDate
            ),
            new AutoSynthesizedMacro(
                MacroId: "MCR-SEED-BILLING-01",
                Category: TicketCategory.BillingAndRefunds,
                Title: "Remboursement traité avec succès",
                TemplateText: "Bonjour {CustomerName}, nous confirmons que le remboursement lié à votre commande {OrderNumber} a été initié sur votre moyen de paiement initial.",
                ConfidenceScore: 0.97,
                ConversionSuccessRate: 0.96,
                UsageCount: 50,
                CreatedAtUtc: seedDate,
                SuccessfulResolutionsCount: 48,
                LastUsedAtUtc: seedDate
            ),
            new AutoSynthesizedMacro(
                MacroId: "MCR-SEED-TECH-01",
                Category: TicketCategory.TechnicalProduct,
                Title: "Assistance configuration produit et garantie",
                TemplateText: "Bonjour {CustomerName}, voici le guide d'installation et de prise en main pour le matériel de votre commande {OrderNumber}.",
                ConfidenceScore: 0.91,
                ConversionSuccessRate: 0.88,
                UsageCount: 16,
                CreatedAtUtc: seedDate,
                SuccessfulResolutionsCount: 14,
                LastUsedAtUtc: seedDate
            ),
            new AutoSynthesizedMacro(
                MacroId: "MCR-SEED-SECURITY-01",
                Category: TicketCategory.AccountAndSecurity,
                Title: "Vérification et réinitialisation de sécurité",
                TemplateText: "Bonjour {CustomerName}, votre compte a été vérifié et sécurisé conformément à nos protocoles de sécurité.",
                ConfidenceScore: 0.98,
                ConversionSuccessRate: 0.97,
                UsageCount: 30,
                CreatedAtUtc: seedDate,
                SuccessfulResolutionsCount: 29,
                LastUsedAtUtc: seedDate
            ),
            new AutoSynthesizedMacro(
                MacroId: "MCR-SEED-B2B-01",
                Category: TicketCategory.WholesaleB2B,
                Title: "Tarification de gros et bon de commande B2B",
                TemplateText: "Bonjour {CustomerName}, votre commande de gros {OrderNumber} a été validée avec les remises volumiques négociées.",
                ConfidenceScore: 0.93,
                ConversionSuccessRate: 0.90,
                UsageCount: 20,
                CreatedAtUtc: seedDate,
                SuccessfulResolutionsCount: 18,
                LastUsedAtUtc: seedDate
            )
        };

        foreach (var macro in seeds)
        {
            _catalog[macro.MacroId] = macro;
        }
    }
}
