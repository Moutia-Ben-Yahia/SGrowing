namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Algorithmic association rule mining engine and dynamic bundle synthesizer for cross-merchant collaborative commerce.
/// </summary>
public interface ICrossMerchantBundleSynthesizerEngine
{
    /// <summary>
    /// Total catalog SKUs indexed in memory.
    /// </summary>
    int TotalSkus { get; }

    /// <summary>
    /// Total historical customer transactions ingested.
    /// </summary>
    int TotalTransactions { get; }

    /// <summary>
    /// Registers or updates an individual SKU in the catalog index.
    /// </summary>
    void RegisterCatalogSku(SkuItem item);

    /// <summary>
    /// Registers a batch of SKUs in the catalog index.
    /// </summary>
    void RegisterCatalogSkus(IEnumerable<SkuItem> items);

    /// <summary>
    /// Retrieves a catalog SKU by ID.
    /// </summary>
    SkuItem? GetCatalogSku(string skuId);

    /// <summary>
    /// Ingests historical basket/cart transactions for affinity mining.
    /// </summary>
    void IngestTransactions(IEnumerable<TransactionRecord> transactions);

    /// <summary>
    /// Clears ingested historical transactions.
    /// </summary>
    void ClearTransactions();

    /// <summary>
    /// Mines frequent itemsets and association rules using Support, Confidence, and Lift metrics.
    /// </summary>
    IReadOnlyList<AssociationRule> MineAssociationRules(
        double minSupport = 0.01,
        double minConfidence = 0.2,
        double minLift = 2.0);

    /// <summary>
    /// Discovers cross-merchant SKU bundles maximizing joint gross margin where Lift >= minLift and merchants differ.
    /// </summary>
    IReadOnlyList<CrossMerchantSkuBundle> DiscoverCrossMerchantBundles(DiscoverBundlesRequest request);

    /// <summary>
    /// Dynamically calculates the optimal bundle discount percentage balancing customer conversion incentive with merchant gross margin preservation.
    /// </summary>
    decimal CalculateOptimalDiscount(decimal regularPrice, decimal costPrice, double lift);
}
