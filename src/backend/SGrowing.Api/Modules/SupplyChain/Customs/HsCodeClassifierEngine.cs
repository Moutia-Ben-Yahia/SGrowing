using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Autonomous multi-tier Harmonized System (HS) code classification engine.
/// Evaluates titles, categories, materials, and descriptions against WCO HS 2022/2027 standards,
/// resolves national tariff lines for Algeria (DZ 10-digit), EU (TARIC 10-digit), and GCC (12-digit),
/// and enforces broker review gating on low confidence or restricted/dual-use goods.
/// </summary>
public partial class HsCodeClassifierEngine : IHsCodeClassifierEngine
{
    private static readonly string[] RestrictedKeywords =
    [
        "chemicals", "chemical", "radio", "drone", "medical", "laser",
        "encryption", "hazardous", "explosive", "weapon", "toxic", "military"
    ];

    private record KnowledgeEntry(
        string HsCode,
        string Heading,
        string Description,
        string Category,
        string[] PrimaryKeywords,
        string[] TypicalMaterials,
        string[] CategorySynonyms,
        NationalTariffLine[] NationalTariffLines,
        decimal StandardMfnDutyRate,
        decimal StandardVatRate,
        decimal StandardExciseRate = 0m,
        bool IsRestrictedOrDualUse = false,
        string? RestrictedNotes = null);

    private static readonly IReadOnlyList<KnowledgeEntry> KnowledgeBase =
    [
        // 1. Apparel/Textiles: 6109.10 Cotton T-shirts
        new KnowledgeEntry(
            HsCode: "6109.10",
            Heading: "T-shirts, singlets and other vests, knitted or crocheted",
            Description: "Cotton T-shirts, singlets and knitted vests",
            Category: "Apparel/Textiles",
            PrimaryKeywords: ["t-shirt", "tshirt", "t shirt", "tee", "singlet", "cotton vest", "undershirt"],
            TypicalMaterials: ["cotton", "organic cotton", "combed cotton", "ring-spun cotton"],
            CategorySynonyms: ["apparel", "textile", "textiles", "clothing", "garments", "fashion"],
            NationalTariffLines:
            [
                new("DZ", "6109.10.10.00", 0.30m, 0.19m, 0.30m, "DAPS 30% on finished cotton knitwear"),
                new("EU", "6109.10.00.10", 0.12m, 0.20m, null, "EU TARIC duty 12%"),
                new("GCC", "6109.10.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 2. Apparel/Textiles: 6203.42 Men's cotton trousers
        new KnowledgeEntry(
            HsCode: "6203.42",
            Heading: "Men's or boys' suits, ensembles, jackets, blazers, trousers",
            Description: "Men's or boys' trousers, bib and brace overalls, breeches and shorts of cotton",
            Category: "Apparel/Textiles",
            PrimaryKeywords: ["trousers", "pants", "jeans", "cotton trousers", "chinos", "slacks", "cargo pants", "shorts"],
            TypicalMaterials: ["cotton", "denim", "cotton twill", "canvas"],
            CategorySynonyms: ["apparel", "textile", "textiles", "clothing", "menswear", "fashion"],
            NationalTariffLines:
            [
                new("DZ", "6203.42.00.00", 0.30m, 0.19m, 0.30m, "DAPS 30% on woven cotton trousers"),
                new("EU", "6203.42.31.00", 0.12m, 0.20m, null, "EU TARIC duty 12% on cotton denim/trousers"),
                new("GCC", "6203.42.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 3. Apparel/Textiles: 6403.99 Leather footwear
        new KnowledgeEntry(
            HsCode: "6403.99",
            Heading: "Footwear with outer soles of rubber, plastics, leather and uppers of leather",
            Description: "Footwear with leather uppers, other than sports footwear or ankle coverage",
            Category: "Apparel/Textiles",
            PrimaryKeywords: ["footwear", "shoes", "leather shoes", "boots", "loafers", "oxfords", "brogues", "moccasins"],
            TypicalMaterials: ["leather", "cowhide", "calfskin", "suede", "full grain leather"],
            CategorySynonyms: ["footwear", "shoes", "leather goods", "apparel", "fashion"],
            NationalTariffLines:
            [
                new("DZ", "6403.99.00.00", 0.30m, 0.19m, 0.30m, "DAPS 30% on imported finished footwear"),
                new("EU", "6403.99.96.00", 0.08m, 0.20m, null, "EU TARIC duty 8% on leather footwear"),
                new("GCC", "6403.99.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 4. Electronics: 8517.13 Smartphones
        new KnowledgeEntry(
            HsCode: "8517.13",
            Heading: "Telephone sets, including smartphones and other telephones for cellular networks",
            Description: "Smartphones for cellular or wireless networks",
            Category: "Electronics",
            PrimaryKeywords: ["smartphone", "smartphones", "mobile phone", "cellphone", "cellular phone", "iphone", "android phone"],
            TypicalMaterials: ["glass", "aluminum", "silicon", "electronic components"],
            CategorySynonyms: ["electronics", "telecommunications", "mobile devices", "gadgets"],
            NationalTariffLines:
            [
                new("DZ", "8517.13.00.00", 0.30m, 0.19m, 0.30m, "DAPS 30% on imported smartphones"),
                new("EU", "8517.13.00.00", 0.00m, 0.20m, null, "EU ITA duty-free 0% on cellular phones"),
                new("GCC", "8517.13.00.00.00", 0.00m, 0.05m, null, "GCC ITA duty-free 0%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 5. Electronics: 8471.30 Portable laptops
        new KnowledgeEntry(
            HsCode: "8471.30",
            Heading: "Automatic data processing machines: Portable laptops",
            Description: "Portable automatic data processing machines, weighing not more than 10 kg, consisting of at least CPU, keyboard and display",
            Category: "Electronics",
            PrimaryKeywords: ["laptop", "laptops", "notebook", "ultrabook", "macbook", "thinkpad", "portable computer"],
            TypicalMaterials: ["aluminum", "magnesium", "plastic", "silicon"],
            CategorySynonyms: ["electronics", "computers", "it equipment", "computing"],
            NationalTariffLines:
            [
                new("DZ", "8471.30.00.00", 0.05m, 0.19m, 0.00m, "DAPS exempt 0% for capital IT equipment; duty 5%"),
                new("EU", "8471.30.00.00", 0.00m, 0.20m, null, "EU ITA duty-free 0% on laptops"),
                new("GCC", "8471.30.00.00.00", 0.00m, 0.05m, null, "GCC ITA duty-free 0%")
            ],
            StandardMfnDutyRate: 0.05m,
            StandardVatRate: 0.19m),

        // 6. Electronics: 8528.52 Computer monitors
        new KnowledgeEntry(
            HsCode: "8528.52",
            Heading: "Monitors and projectors, not incorporating television reception apparatus",
            Description: "Monitors capable of directly connecting to and designed for use with an ADP machine of heading 8471",
            Category: "Electronics",
            PrimaryKeywords: ["monitor", "monitors", "computer monitor", "display screen", "lcd monitor", "led monitor", "curved monitor"],
            TypicalMaterials: ["plastic", "glass", "electronic components", "metal"],
            CategorySynonyms: ["electronics", "displays", "peripherals", "computers"],
            NationalTariffLines:
            [
                new("DZ", "8528.52.00.00", 0.30m, 0.19m, 0.30m, "DAPS 30% on imported computer monitors"),
                new("EU", "8528.52.10.00", 0.00m, 0.20m, null, "EU ITA duty-free 0% on ADP monitors"),
                new("GCC", "8528.52.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 7. Beauty/Cosmetics: 3304.99 Skincare preparations
        new KnowledgeEntry(
            HsCode: "3304.99",
            Heading: "Beauty or make-up preparations and preparations for the care of the skin",
            Description: "Skincare preparations, anti-aging creams, serums, moisturizers and cosmetic lotions",
            Category: "Beauty/Cosmetics",
            PrimaryKeywords: ["skincare", "skin cream", "moisturizer", "face cream", "facial serum", "lotion", "anti-aging cream", "sunscreen"],
            TypicalMaterials: ["cream", "serum", "emulsion", "essential oils", "glycerin"],
            CategorySynonyms: ["beauty", "cosmetics", "skincare", "personal care"],
            NationalTariffLines:
            [
                new("DZ", "3304.99.00.00", 0.30m, 0.19m, 0.60m, "DAPS 60% luxury surcharge on cosmetic creams"),
                new("EU", "3304.99.00.00", 0.00m, 0.20m, null, "EU MFN duty 0% on skincare preparations"),
                new("GCC", "3304.99.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 8. Beauty/Cosmetics: 3305.10 Shampoos
        new KnowledgeEntry(
            HsCode: "3305.10",
            Heading: "Preparations for use on the hair: Shampoos",
            Description: "Hair shampoos and cleansing preparations for hair",
            Category: "Beauty/Cosmetics",
            PrimaryKeywords: ["shampoo", "shampoos", "hair shampoo", "anti-dandruff shampoo", "hair cleanser", "cleansing shampoo"],
            TypicalMaterials: ["liquid", "gel", "surfactant", "herbal extracts"],
            CategorySynonyms: ["beauty", "hair care", "personal care", "cosmetics"],
            NationalTariffLines:
            [
                new("DZ", "3305.10.00.00", 0.30m, 0.19m, 0.30m, "DAPS 30% surcharge on imported hair care products"),
                new("EU", "3305.10.00.00", 0.00m, 0.20m, null, "EU MFN duty 0% on shampoos"),
                new("GCC", "3305.10.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 9. Beauty/Cosmetics: 3303.00 Perfumes
        new KnowledgeEntry(
            HsCode: "3303.00",
            Heading: "Perfumes and toilet waters",
            Description: "Perfumes, eau de parfum, eau de toilette and fine fragrance toilet waters",
            Category: "Beauty/Cosmetics",
            PrimaryKeywords: ["perfume", "perfumes", "fragrance", "cologne", "eau de parfum", "eau de toilette", "scent", "aftershave"],
            TypicalMaterials: ["alcohol", "essential oils", "fragrance compound", "glass bottle"],
            CategorySynonyms: ["beauty", "fragrance", "perfumery", "cosmetics", "luxury"],
            NationalTariffLines:
            [
                new("DZ", "3303.00.00.00", 0.30m, 0.19m, 0.60m, "DAPS 60% luxury surcharge on imported perfumes"),
                new("EU", "3303.00.10.00", 0.00m, 0.20m, null, "EU MFN duty 0% on perfumes"),
                new("GCC", "3303.00.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5% plus 50% selective tax on luxury fragrances")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m,
            StandardExciseRate: 0.50m),

        // 10. Food/Beverage: 0901.21 Roasted coffee
        new KnowledgeEntry(
            HsCode: "0901.21",
            Heading: "Coffee, whether or not roasted or decaffeinated: Roasted, not decaffeinated",
            Description: "Coffee, roasted, not decaffeinated beans or ground coffee",
            Category: "Food/Beverage",
            PrimaryKeywords: ["coffee", "roasted coffee", "coffee beans", "ground coffee", "espresso beans", "arabica", "robusta"],
            TypicalMaterials: ["coffee bean", "arabica", "robusta", "whole bean"],
            CategorySynonyms: ["food", "beverage", "groceries", "commodities", "agri-food"],
            NationalTariffLines:
            [
                new("DZ", "0901.21.00.00", 0.30m, 0.09m, 0.00m, "DAPS exempt 0% for essential food; reduced VAT 9%"),
                new("EU", "0901.21.00.00", 0.075m, 0.07m, null, "EU MFN duty 7.5%, reduced food VAT 7%"),
                new("GCC", "0901.21.00.00.00", 0.00m, 0.05m, null, "GCC Unified Tariff 0% on raw/roasted coffee")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.09m),

        // 11. Food/Beverage: 1806.32 Chocolate bars
        new KnowledgeEntry(
            HsCode: "1806.32",
            Heading: "Chocolate and other food preparations containing cocoa: In blocks, slabs or bars",
            Description: "Chocolate and other food preparations containing cocoa, in blocks, slabs or bars, not filled",
            Category: "Food/Beverage",
            PrimaryKeywords: ["chocolate", "chocolate bar", "dark chocolate", "milk chocolate", "cocoa bar", "confectionery"],
            TypicalMaterials: ["cocoa", "chocolate", "sugar", "milk solid", "cocoa butter"],
            CategorySynonyms: ["food", "confectionery", "sweets", "beverage", "groceries"],
            NationalTariffLines:
            [
                new("DZ", "1806.32.00.00", 0.30m, 0.19m, 0.30m, "DAPS 30% surcharge on imported confectionery"),
                new("EU", "1806.32.10.00", 0.083m, 0.07m, null, "EU TARIC duty 8.3% + agricultural component"),
                new("GCC", "1806.32.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 12. Food/Beverage: 2202.99 Non-alcoholic beverages
        new KnowledgeEntry(
            HsCode: "2202.99",
            Heading: "Waters, including mineral waters and aerated waters, containing added sugar or other sweetening matter or flavored",
            Description: "Other non-alcoholic beverages, flavored waters, juice drinks and energy drinks",
            Category: "Food/Beverage",
            PrimaryKeywords: ["beverage", "drink", "soda", "energy drink", "fruit beverage", "non-alcoholic drink", "flavored water", "iced tea"],
            TypicalMaterials: ["water", "sugar", "fruit extract", "flavoring"],
            CategorySynonyms: ["food", "beverage", "drinks", "refreshments"],
            NationalTariffLines:
            [
                new("DZ", "2202.99.00.00", 0.30m, 0.19m, 0.60m, "DAPS 60% luxury surcharge on sweetened beverages"),
                new("EU", "2202.99.19.00", 0.096m, 0.19m, null, "EU TARIC duty 9.6% on sweetened beverages"),
                new("GCC", "2202.99.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5% plus 50% excise tax on sweetened/energy drinks")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m,
            StandardExciseRate: 0.50m),

        // 13. Home/Hardware: 9403.60 Wooden furniture
        new KnowledgeEntry(
            HsCode: "9403.60",
            Heading: "Other furniture and parts thereof: Other wooden furniture",
            Description: "Wooden furniture for bedrooms, dining rooms, offices and living spaces",
            Category: "Home/Hardware",
            PrimaryKeywords: ["furniture", "wooden furniture", "wood desk", "dining table", "wooden cabinet", "chair", "bookshelf", "wardrobe", "coffee table"],
            TypicalMaterials: ["wood", "oak", "teak", "timber", "pine", "walnut", "solid wood"],
            CategorySynonyms: ["home", "furniture", "interior", "household", "living"],
            NationalTariffLines:
            [
                new("DZ", "9403.60.00.00", 0.30m, 0.19m, 0.60m, "DAPS 60% protective surcharge on imported furniture"),
                new("EU", "9403.60.10.00", 0.00m, 0.20m, null, "EU MFN duty 0% on wooden furniture"),
                new("GCC", "9403.60.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m),

        // 14. Home/Hardware: 8205.59 Hand tools
        new KnowledgeEntry(
            HsCode: "8205.59",
            Heading: "Hand tools, not elsewhere specified or included",
            Description: "Manual hand tools, wrenches, pliers, hammers, screwdrivers and assembly tools",
            Category: "Home/Hardware",
            PrimaryKeywords: ["hand tool", "tools", "wrench", "pliers", "hammer", "screwdriver", "spanner", "chisel", "clamp", "toolkit"],
            TypicalMaterials: ["steel", "carbon steel", "alloy steel", "chrome vanadium", "iron"],
            CategorySynonyms: ["hardware", "tools", "industrial", "home improvement", "workshop"],
            NationalTariffLines:
            [
                new("DZ", "8205.59.00.00", 0.15m, 0.19m, 0.00m, "DAPS exempt 0% for capital/artisan tools; duty 15%"),
                new("EU", "8205.59.80.00", 0.017m, 0.20m, null, "EU TARIC duty 1.7%"),
                new("GCC", "8205.59.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%")
            ],
            StandardMfnDutyRate: 0.15m,
            StandardVatRate: 0.19m),

        // 15. Restricted / Dual-Use: 9018.90 Medical instruments
        new KnowledgeEntry(
            HsCode: "9018.90",
            Heading: "Instruments and appliances used in medical, surgical, dental or veterinary sciences",
            Description: "Medical diagnostic, surgical and therapeutic equipment and instruments",
            Category: "Medical/Instruments",
            PrimaryKeywords: ["medical", "surgical", "medical instrument", "stethoscope", "syringe", "diagnostic equipment", "surgical kit"],
            TypicalMaterials: ["titanium", "stainless steel", "silicone", "medical grade polymer"],
            CategorySynonyms: ["medical", "healthcare", "hospital", "biomedical"],
            NationalTariffLines:
            [
                new("DZ", "9018.90.00.00", 0.05m, 0.09m, 0.00m, "DAPS exempt 0%; reduced duty 5% for health devices"),
                new("EU", "9018.90.00.00", 0.00m, 0.05m, null, "EU MFN duty 0% on medical equipment"),
                new("GCC", "9018.90.00.00.00", 0.00m, 0.05m, null, "GCC Unified Tariff 0% on medical equipment")
            ],
            StandardMfnDutyRate: 0.05m,
            StandardVatRate: 0.09m,
            IsRestrictedOrDualUse: true,
            RestrictedNotes: "Requires Ministry of Health clearance and CE/FDA medical regulatory certificates."),

        // 16. Restricted / Dual-Use: 8806.22 Unmanned aircraft / Drones
        new KnowledgeEntry(
            HsCode: "8806.22",
            Heading: "Unmanned aircraft: With maximum take-off weight more than 250 g but not more than 7 kg",
            Description: "Autonomous and remotely piloted drones, quadcopters and UAV systems",
            Category: "Aviation/Drones",
            PrimaryKeywords: ["drone", "drones", "unmanned aerial vehicle", "uav", "quadcopter", "aerial drone"],
            TypicalMaterials: ["carbon fiber", "lithium polymer", "electronic components", "avionics"],
            CategorySynonyms: ["aviation", "drones", "robotics", "dual-use"],
            NationalTariffLines:
            [
                new("DZ", "8806.22.00.00", 0.30m, 0.19m, 0.60m, "DAPS 60%; strictly subject to Ministry of National Defense import authorization"),
                new("EU", "8806.22.00.00", 0.00m, 0.20m, null, "EU TARIC duty 0% on civil UAVs"),
                new("GCC", "8806.22.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%; requires civil aviation security approval")
            ],
            StandardMfnDutyRate: 0.30m,
            StandardVatRate: 0.19m,
            IsRestrictedOrDualUse: true,
            RestrictedNotes: "Dual-use technology: requires Ministry of Defense authorization and telemetry frequency clearance."),

        // 17. Restricted / Dual-Use: 8526.91 Radio navigational equipment
        new KnowledgeEntry(
            HsCode: "8526.91",
            Heading: "Radar apparatus, radio navigational aid apparatus and radio remote control apparatus",
            Description: "Radio transmitters, radio transceivers, radar and navigational aid devices",
            Category: "Telecommunications/Radio",
            PrimaryKeywords: ["radio", "radio transmitter", "radio transceiver", "radar", "navigational aid", "rf transponder"],
            TypicalMaterials: ["electronics", "rf components", "copper", "semiconductor"],
            CategorySynonyms: ["telecommunications", "radio", "avionics", "dual-use"],
            NationalTariffLines:
            [
                new("DZ", "8526.91.00.00", 0.15m, 0.19m, 0.00m, "Requires ARPCE telecommunications homologation and security approval"),
                new("EU", "8526.91.00.00", 0.00m, 0.20m, null, "EU TARIC duty 0% on radio navigational aids"),
                new("GCC", "8526.91.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%; requires telecom authority approval")
            ],
            StandardMfnDutyRate: 0.15m,
            StandardVatRate: 0.19m,
            IsRestrictedOrDualUse: true,
            RestrictedNotes: "Subject to national frequency spectrum allocation authority and security clearance."),

        // 18. Restricted / Dual-Use: 2804.70 Industrial chemicals
        new KnowledgeEntry(
            HsCode: "2804.70",
            Heading: "Hydrogen, rare gases and other non-metals: Phosphorus and chemical elements",
            Description: "Inorganic chemicals, reactive elements, industrial chemical agents",
            Category: "Chemicals",
            PrimaryKeywords: ["chemical", "chemicals", "phosphorus", "toxic chemical", "industrial chemicals", "reactive element"],
            TypicalMaterials: ["chemical compound", "elemental substance", "phosphorus"],
            CategorySynonyms: ["chemicals", "industrial", "hazardous materials", "dual-use"],
            NationalTariffLines:
            [
                new("DZ", "2804.70.00.00", 0.05m, 0.19m, 0.00m, "Requires Ministry of Industry chemical hazard and environmental permit"),
                new("EU", "2804.70.00.00", 0.055m, 0.20m, null, "EU TARIC duty 5.5%; subject to REACH compliance"),
                new("GCC", "2804.70.00.00.00", 0.05m, 0.05m, null, "GCC Unified Tariff 5%; requires civil defense chemical approval")
            ],
            StandardMfnDutyRate: 0.05m,
            StandardVatRate: 0.19m,
            IsRestrictedOrDualUse: true,
            RestrictedNotes: "Strict hazardous materials transport restrictions and chemical security compliance required.")
    ];

    /// <inheritdoc />
    public ProductClassificationResult ClassifyProduct(ProductClassificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = (request.Title ?? string.Empty).Trim();
        var category = (request.Category ?? string.Empty).Trim();
        var material = (request.Material ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();

        var combinedText = $"{title} {category} {material} {description}".ToLowerInvariant();

        // 1. Check for restricted or dual-use triggers across all text
        string? restrictedTrigger = null;
        foreach (var keyword in RestrictedKeywords)
        {
            if (ContainsWordOrSubstring(combinedText, keyword))
            {
                restrictedTrigger = keyword;
                break;
            }
        }

        // 2. Score knowledge entries
        KnowledgeEntry? bestEntry = null;
        var highestScore = -1.0;
        var matchedKeywords = new List<string>();

        foreach (var entry in KnowledgeBase)
        {
            var (score, keywords) = ComputeScore(entry, title, category, material, description);
            if (score > highestScore)
            {
                highestScore = score;
                bestEntry = entry;
                matchedKeywords = keywords;
            }
        }

        // Fallback if no match found
        bestEntry ??= KnowledgeBase[0];
        if (highestScore < 0)
        {
            highestScore = 0.10;
        }

        // Clamp confidence score between 0.0 and 1.0
        var confidence = Math.Clamp(Math.Round(highestScore, 2), 0.0, 1.0);

        // 3. Broker review gating
        var requiresBrokerReview = false;
        string? reviewReason = null;

        if (restrictedTrigger is not null)
        {
            requiresBrokerReview = true;
            reviewReason = $"Restricted or dual-use goods trigger detected: '{restrictedTrigger}'. Mandatory customs broker review and regulatory clearance required.";
        }
        else if (bestEntry.IsRestrictedOrDualUse)
        {
            requiresBrokerReview = true;
            reviewReason = $"Item classified under restricted/dual-use HS code {bestEntry.HsCode}. {bestEntry.RestrictedNotes ?? "Mandatory customs broker review required."}";
        }
        else if (confidence < 0.80)
        {
            requiresBrokerReview = true;
            reviewReason = $"Classification confidence ({confidence:F2}) is below autonomous clearance threshold (0.80). Customs broker review required.";
        }

        // 4. Resolve national tariff lines filtered by destination if provided
        var tariffLines = ResolveTariffLines(bestEntry, request.DestinationCountry);

        return new ProductClassificationResult(
            HsCode: bestEntry.HsCode,
            Heading: bestEntry.Heading,
            Description: bestEntry.Description,
            Category: bestEntry.Category,
            ConfidenceScore: confidence,
            RequiresBrokerReview: requiresBrokerReview,
            ReviewReason: reviewReason,
            NationalTariffLines: tariffLines,
            MatchedKeywords: matchedKeywords,
            ClassifiedAtUtc: DateTime.UtcNow);
    }

    /// <inheritdoc />
    public HsCodeDetails? GetHsCodeDetails(string hsCode)
    {
        if (string.IsNullOrWhiteSpace(hsCode))
        {
            return null;
        }

        var normalized = NormalizeHs(hsCode);
        var entry = KnowledgeBase.FirstOrDefault(e => NormalizeHs(e.HsCode) == normalized);
        if (entry is null)
        {
            return null;
        }

        return new HsCodeDetails(
            HsCode: entry.HsCode,
            Heading: entry.Heading,
            Description: entry.Description,
            Category: entry.Category,
            TypicalMaterials: entry.TypicalMaterials,
            NationalTariffLines: entry.NationalTariffLines,
            StandardMfnDutyRate: entry.StandardMfnDutyRate,
            StandardVatRate: entry.StandardVatRate,
            StandardExciseRate: entry.StandardExciseRate,
            IsRestrictedOrDualUse: entry.IsRestrictedOrDualUse,
            RestrictedNotes: entry.RestrictedNotes);
    }

    /// <inheritdoc />
    public IReadOnlyList<HsCandidateSuggestion> SuggestHsCandidates(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var suggestions = new List<HsCandidateSuggestion>();

        foreach (var entry in KnowledgeBase)
        {
            var relevance = 0.0;

            if (NormalizeHs(entry.HsCode).StartsWith(NormalizeHs(normalizedQuery), StringComparison.OrdinalIgnoreCase))
            {
                relevance += 0.90;
            }
            else if (entry.Heading.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                relevance += 0.80;
            }
            else if (entry.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                relevance += 0.70;
            }
            else if (entry.PrimaryKeywords.Any(k => k.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) || normalizedQuery.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                relevance += 0.65;
            }
            else if (entry.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                relevance += 0.50;
            }

            if (relevance > 0.1)
            {
                suggestions.Add(new HsCandidateSuggestion(
                    HsCode: entry.HsCode,
                    Description: entry.Description,
                    Category: entry.Category,
                    RelevanceScore: Math.Round(relevance, 2)));
            }
        }

        return suggestions.OrderByDescending(s => s.RelevanceScore).Take(5).ToList();
    }

    #region Helper Methods

    private static (double Score, List<string> MatchedKeywords) ComputeScore(
        KnowledgeEntry entry,
        string title,
        string category,
        string material,
        string description)
    {
        var matched = new List<string>();
        var titleLower = title.ToLowerInvariant();
        var catLower = category.ToLowerInvariant();
        var matLower = material.ToLowerInvariant();
        var descLower = description.ToLowerInvariant();

        var score = 0.0;

        // 1. Primary Title Keyword match (up to 0.50)
        var primaryMatched = false;
        foreach (var kw in entry.PrimaryKeywords)
        {
            if (ContainsWordOrSubstring(titleLower, kw))
            {
                score += 0.50;
                matched.Add($"title:{kw}");
                primaryMatched = true;
                break;
            }
        }

        if (!primaryMatched)
        {
            // Partial match in title
            foreach (var kw in entry.PrimaryKeywords)
            {
                var parts = kw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => p.Length >= 4 && titleLower.Contains(p)))
                {
                    score += 0.25;
                    matched.Add($"title_part:{kw}");
                    break;
                }
            }
        }

        // 2. Material match (up to 0.25)
        var materialMatched = false;
        if (!string.IsNullOrWhiteSpace(matLower))
        {
            foreach (var mat in entry.TypicalMaterials)
            {
                if (matLower.Contains(mat) || mat.Contains(matLower))
                {
                    score += 0.25;
                    matched.Add($"material:{mat}");
                    materialMatched = true;
                    break;
                }
            }
        }

        if (!materialMatched)
        {
            // Check if material is mentioned in Title or Description
            foreach (var mat in entry.TypicalMaterials)
            {
                if (ContainsWordOrSubstring(titleLower, mat))
                {
                    score += 0.20;
                    matched.Add($"title_material:{mat}");
                    break;
                }
                if (ContainsWordOrSubstring(descLower, mat))
                {
                    score += 0.15;
                    matched.Add($"desc_material:{mat}");
                    break;
                }
            }
        }

        // 3. Category match (up to 0.20)
        if (!string.IsNullOrWhiteSpace(catLower))
        {
            if (entry.Category.Contains(catLower, StringComparison.OrdinalIgnoreCase) ||
                entry.CategorySynonyms.Any(syn => syn.Contains(catLower) || catLower.Contains(syn)))
            {
                score += 0.20;
                matched.Add($"category:{category}");
            }
        }
        else
        {
            // Check if category synonyms appear in Title
            foreach (var syn in entry.CategorySynonyms)
            {
                if (ContainsWordOrSubstring(titleLower, syn))
                {
                    score += 0.10;
                    matched.Add($"title_cat:{syn}");
                    break;
                }
            }
        }

        // 4. Description match (up to 0.05)
        if (!string.IsNullOrWhiteSpace(descLower))
        {
            foreach (var kw in entry.PrimaryKeywords)
            {
                if (ContainsWordOrSubstring(descLower, kw) && !matched.Any(m => m.EndsWith(kw)))
                {
                    score += 0.05;
                    matched.Add($"desc:{kw}");
                    break;
                }
            }
        }

        return (score, matched);
    }

    private static IReadOnlyList<NationalTariffLine> ResolveTariffLines(KnowledgeEntry entry, string? destinationCountry)
    {
        if (string.IsNullOrWhiteSpace(destinationCountry))
        {
            return entry.NationalTariffLines;
        }

        var normalizedDest = destinationCountry.Trim().ToUpperInvariant();

        // Map country codes to target tariff line system
        if (normalizedDest is "DZ" or "DZA" or "ALGERIA")
        {
            var dzLines = entry.NationalTariffLines.Where(l => l.CountryCode == "DZ").ToList();
            return dzLines.Count > 0 ? dzLines : entry.NationalTariffLines;
        }

        if (IsEuMember(normalizedDest))
        {
            var euLines = entry.NationalTariffLines.Where(l => l.CountryCode == "EU").ToList();
            return euLines.Count > 0 ? euLines : entry.NationalTariffLines;
        }

        if (IsGccMember(normalizedDest))
        {
            var gccLines = entry.NationalTariffLines.Where(l => l.CountryCode == "GCC").ToList();
            return gccLines.Count > 0 ? gccLines : entry.NationalTariffLines;
        }

        return entry.NationalTariffLines;
    }

    private static bool IsEuMember(string code) => code is
        "EU" or "FR" or "DE" or "IT" or "ES" or "NL" or "BE" or "PL" or "SE" or
        "AT" or "IE" or "PT" or "GR" or "FI" or "DK" or "CZ" or "RO" or "HU" or
        "SK" or "BG" or "HR" or "LT" or "SI" or "LV" or "EE" or "CY" or "LU" or "MT";

    private static bool IsGccMember(string code) => code is
        "GCC" or "AE" or "ARE" or "SA" or "SAU" or "QA" or "QAT" or "KW" or "KWT" or "OM" or "OMN" or "BH" or "BHR";

    private static bool ContainsWordOrSubstring(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return false;
        }

        return source.Contains(target, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHs(string code) =>
        code.Replace(".", string.Empty).Replace(" ", string.Empty).Trim();

    #endregion
}
