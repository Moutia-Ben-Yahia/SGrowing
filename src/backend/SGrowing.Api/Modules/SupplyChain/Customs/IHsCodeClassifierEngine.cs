namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Autonomous multi-tier Harmonized System (HS) code classification engine.
/// Maps product metadata to 6-digit WCO codes and national tariff lines (Algeria, EU TARIC, GCC).
/// </summary>
public interface IHsCodeClassifierEngine
{
    /// <summary>
    /// Classifies a product title, category, material, and description into an HS code and national tariff lines.
    /// </summary>
    ProductClassificationResult ClassifyProduct(ProductClassificationRequest request);

    /// <summary>
    /// Retrieves comprehensive reference details for a specific 6-digit HS code.
    /// </summary>
    HsCodeDetails? GetHsCodeDetails(string hsCode);

    /// <summary>
    /// Suggests candidate HS codes matching a search query or keyword fragment.
    /// </summary>
    IReadOnlyList<HsCandidateSuggestion> SuggestHsCandidates(string query);
}
