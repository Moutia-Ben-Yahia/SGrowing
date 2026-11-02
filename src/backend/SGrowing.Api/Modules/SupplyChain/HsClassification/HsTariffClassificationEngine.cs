namespace SGrowing.Api.Modules.SupplyChain.HsClassification;

/// <summary>
/// Autonomous Harmonized System (HS / Tariff) classification engine
/// utilizing semantic tokenization and keyword matching to identify 6-digit HS codes.
/// </summary>
public sealed class HsTariffClassificationEngine
{
    private readonly List<HsTariffEntry> _catalog =
    [
        new(
            HsCode: "8471.30",
            Chapter: "84",
            Heading: "8471",
            Subheading: "30",
            Description: "Portable automatic data processing machines, weighing not more than 10 kg, consisting of at least a central processing unit, a keyboard and a display (laptops, notebooks, tablets)",
            Keywords: ["laptop", "notebook", "computer", "portable", "tablet", "pc", "processor", "display"],
            StandardMfnDutyRatePercent: 0.0m, // Information Technology Agreement (ITA) zero-tariff
            VatRatePercent: 19.0m
        ),
        new(
            HsCode: "8471.50",
            Chapter: "84",
            Heading: "8471",
            Subheading: "50",
            Description: "Processing units other than those of subheading 8471.30, whether or not containing in the same housing storage units, input units or output units (servers, desktop towers)",
            Keywords: ["server", "rackmount", "workstation", "desktop", "tower", "datacenter", "compute", "unit"],
            StandardMfnDutyRatePercent: 0.0m,
            VatRatePercent: 19.0m
        ),
        new(
            HsCode: "1509.20",
            Chapter: "15",
            Heading: "1509",
            Subheading: "20",
            Description: "Extra virgin olive oil obtained solely from the fruit of the olive tree solely by mechanical or other physical means",
            Keywords: ["olive", "oil", "extra", "virgin", "edible", "cold-pressed", "culinary", "cooking"],
            StandardMfnDutyRatePercent: 15.0m,
            VatRatePercent: 9.0m, // Reduced agro-food VAT
            RequiresPhytosanitaryPermit: true,
            RequiresHalalCertificate: true
        ),
        new(
            HsCode: "0804.10",
            Chapter: "08",
            Heading: "0804",
            Subheading: "10",
            Description: "Dates, fresh or dried (Deglet Nour, Medjool, conditioned dates)",
            Keywords: ["dates", "deglet", "nour", "medjool", "fruit", "dried", "fresh", "palms"],
            StandardMfnDutyRatePercent: 20.0m,
            VatRatePercent: 9.0m,
            RequiresPhytosanitaryPermit: true
        ),
        new(
            HsCode: "3004.20",
            Chapter: "30",
            Heading: "3004",
            Subheading: "20",
            Description: "Medicaments containing other antibiotics, put up in measured doses or in forms or packings for retail sale",
            Keywords: ["medicine", "medicament", "antibiotic", "pharma", "pharmaceutical", "capsule", "tablet", "injection"],
            StandardMfnDutyRatePercent: 5.0m,
            VatRatePercent: 0.0m, // Zero-rated essential pharmaceuticals
            RequiresPhytosanitaryPermit: false
        ),
        new(
            HsCode: "5208.11",
            Chapter: "52",
            Heading: "5208",
            Subheading: "11",
            Description: "Woven fabrics of cotton, containing 85% or more by weight of cotton, unbleached, plain weave, weighing not more than 100 g/m2",
            Keywords: ["cotton", "fabric", "textile", "woven", "plain", "unbleached", "yarn"],
            StandardMfnDutyRatePercent: 12.0m,
            VatRatePercent: 19.0m
        ),
        new(
            HsCode: "8703.80",
            Chapter: "87",
            Heading: "8703",
            Subheading: "80",
            Description: "Motor cars and other motor vehicles principally designed for the transport of persons, with only electric motor for propulsion (BEV / EV)",
            Keywords: ["electric", "car", "vehicle", "automobile", "ev", "battery", "propulsion", "motor"],
            StandardMfnDutyRatePercent: 5.0m, // Preferential green automotive tariff
            VatRatePercent: 19.0m,
            ExciseTaxRatePercent: 0.0m
        )
    ];

    public IReadOnlyList<HsClassificationCandidate> ClassifyProduct(string productDescription, int maxCandidates = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productDescription);

        var tokens = Tokenize(productDescription);
        var scored = new List<(HsTariffEntry Entry, double Score)>();

        foreach (var entry in _catalog)
        {
            var matchCount = 0;
            foreach (var token in tokens)
            {
                if (entry.Keywords.Any(k => k.Contains(token, StringComparison.OrdinalIgnoreCase) || token.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    matchCount++;
                }
            }

            var totalKeywords = entry.Keywords.Count;
            var score = totalKeywords > 0 ? (double)matchCount / Math.Max(tokens.Count, totalKeywords) : 0.0;

            if (matchCount > 0)
            {
                // Boost if chapter/heading words appear directly in description
                if (entry.Description.Contains(productDescription, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.3;
                }
                scored.Add((entry, Math.Min(1.0, score)));
            }
        }

        if (scored.Count == 0)
        {
            // Default fallback candidate
            var fallback = _catalog[0];
            return [new HsClassificationCandidate(fallback.HsCode, fallback.Description, 0.1, fallback.StandardMfnDutyRatePercent, fallback.VatRatePercent)];
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(maxCandidates)
            .Select(s => new HsClassificationCandidate(
                HsCode: s.Entry.HsCode,
                Description: s.Entry.Description,
                ConfidenceScore: Math.Round(s.Score, 3),
                StandardDutyRatePercent: s.Entry.StandardMfnDutyRatePercent,
                VatRatePercent: s.Entry.VatRatePercent
            ))
            .ToList();
    }

    public HsTariffEntry? GetEntry(string hsCode) =>
        _catalog.FirstOrDefault(c => string.Equals(c.HsCode, hsCode, StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> Tokenize(string text)
    {
        var delimiters = new[] { ' ', ',', '.', ';', ':', '-', '(', ')', '/', '\\' };
        var rawTokens = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "an", "the", "and", "or", "for", "with", "in", "of", "to", "by", "on" };

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in rawTokens)
        {
            var clean = t.Trim().ToLowerInvariant();
            if (clean.Length >= 2 && !stopWords.Contains(clean))
            {
                result.Add(clean);
            }
        }
        return result;
    }
}
