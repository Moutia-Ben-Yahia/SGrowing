using System.Threading;

namespace SGrowing.Api.Modules.Commerce.CoOp;

/// <summary>
/// Autonomous engine for mining frequent cross-merchant itemsets, evaluating association rules,
/// and synthesizing dynamic SKU bundles with optimal margin-preserving discounts.
/// </summary>
public class CrossMerchantBundleSynthesizerEngine : ICrossMerchantBundleSynthesizerEngine
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, SkuItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TransactionRecord> _transactions = [];

    public int TotalSkus
    {
        get
        {
            lock (_lock)
            {
                return _catalog.Count;
            }
        }
    }

    public int TotalTransactions
    {
        get
        {
            lock (_lock)
            {
                return _transactions.Count;
            }
        }
    }

    public void RegisterCatalogSku(SkuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.SkuId))
            throw new ArgumentException("SkuId cannot be empty.", nameof(item));

        lock (_lock)
        {
            _catalog[item.SkuId] = item;
        }
    }

    public void RegisterCatalogSkus(IEnumerable<SkuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        lock (_lock)
        {
            foreach (var item in items)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.SkuId))
                {
                    _catalog[item.SkuId] = item;
                }
            }
        }
    }

    public SkuItem? GetCatalogSku(string skuId)
    {
        if (string.IsNullOrWhiteSpace(skuId)) return null;

        lock (_lock)
        {
            return _catalog.GetValueOrDefault(skuId);
        }
    }

    public void IngestTransactions(IEnumerable<TransactionRecord> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        lock (_lock)
        {
            foreach (var tx in transactions)
            {
                if (tx != null && tx.SkuIds.Count > 0)
                {
                    _transactions.Add(tx);
                }
            }
        }
    }

    public void ClearTransactions()
    {
        lock (_lock)
        {
            _transactions.Clear();
        }
    }

    public IReadOnlyList<AssociationRule> MineAssociationRules(
        double minSupport = 0.01,
        double minConfidence = 0.2,
        double minLift = 2.0)
    {
        List<TransactionRecord> txSnapshot;
        lock (_lock)
        {
            txSnapshot = [.. _transactions];
        }

        int n = txSnapshot.Count;
        if (n == 0) return [];

        // 1. Calculate 1-itemset frequencies
        var itemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var tx in txSnapshot)
        {
            var distinctSkus = tx.SkuIds.Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var sku in distinctSkus)
            {
                itemCounts[sku] = itemCounts.GetValueOrDefault(sku) + 1;
            }
        }

        int minSupportCount = Math.Max(1, (int)Math.Ceiling(minSupport * n));

        // Filter frequent 1-itemsets
        var frequentItems = itemCounts
            .Where(kvp => kvp.Value >= minSupportCount)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (frequentItems.Count < 2) return [];

        // 2. Count 2-itemsets
        var pairCounts = new Dictionary<(string A, string B), int>();
        foreach (var tx in txSnapshot)
        {
            var items = tx.SkuIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(frequentItems.Contains)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    var pair = (items[i], items[j]);
                    pairCounts[pair] = pairCounts.GetValueOrDefault(pair) + 1;
                }
            }
        }

        // 3. Extract association rules satisfying minSupport, minConfidence, minLift
        var rules = new List<AssociationRule>();

        foreach (var (pair, count) in pairCounts)
        {
            if (count < minSupportCount) continue;

            double supportPair = (double)count / n;
            double supportA = (double)itemCounts[pair.A] / n;
            double supportB = (double)itemCounts[pair.B] / n;

            // Lift = Support(A u B) / (Support(A) * Support(B))
            double lift = supportPair / (supportA * supportB);
            if (lift < minLift) continue;

            // Rule A => B
            double confidenceAtoB = supportPair / supportA;
            if (confidenceAtoB >= minConfidence)
            {
                rules.Add(new AssociationRule(
                    AntecedentSkus: [pair.A],
                    ConsequentSkus: [pair.B],
                    Support: Math.Round(supportPair, 4),
                    Confidence: Math.Round(confidenceAtoB, 4),
                    Lift: Math.Round(lift, 4),
                    TransactionCount: count));
            }

            // Rule B => A
            double confidenceBtoA = supportPair / supportB;
            if (confidenceBtoA >= minConfidence)
            {
                rules.Add(new AssociationRule(
                    AntecedentSkus: [pair.B],
                    ConsequentSkus: [pair.A],
                    Support: Math.Round(supportPair, 4),
                    Confidence: Math.Round(confidenceBtoA, 4),
                    Lift: Math.Round(lift, 4),
                    TransactionCount: count));
            }
        }

        return rules
            .OrderByDescending(r => r.Lift)
            .ThenByDescending(r => r.Support)
            .ToList();
    }

    public IReadOnlyList<CrossMerchantSkuBundle> DiscoverCrossMerchantBundles(DiscoverBundlesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<TransactionRecord> txSnapshot;
        Dictionary<string, SkuItem> catalogSnapshot;

        lock (_lock)
        {
            txSnapshot = [.. _transactions];
            catalogSnapshot = new Dictionary<string, SkuItem>(_catalog, StringComparer.OrdinalIgnoreCase);
        }

        int n = txSnapshot.Count;
        if (n == 0 || catalogSnapshot.Count < 2) return [];

        var rules = MineAssociationRules(request.MinSupport, minConfidence: 0.1, request.MinLift);

        var merchantFilter = request.MerchantFilter != null && request.MerchantFilter.Count > 0
            ? new HashSet<string>(request.MerchantFilter, StringComparer.OrdinalIgnoreCase)
            : null;

        var discoveredBundles = new List<CrossMerchantSkuBundle>();
        var seenBundleSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            var skuAId = rule.AntecedentSkus[0];
            var skuBId = rule.ConsequentSkus[0];

            if (!catalogSnapshot.TryGetValue(skuAId, out var skuA) ||
                !catalogSnapshot.TryGetValue(skuBId, out var skuB))
            {
                continue;
            }

            // Filter for true cross-merchant collaboration: SKUs must originate from different merchants
            if (string.Equals(skuA.MerchantId, skuB.MerchantId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check merchant filter if specified: all participating merchants must be in the filter
            if (merchantFilter != null &&
                (!merchantFilter.Contains(skuA.MerchantId) ||
                 !merchantFilter.Contains(skuB.MerchantId)))
            {
                continue;
            }

            // Create canonical signature to prevent reverse duplicates
            var sortedSkuIds = new[] { skuA.SkuId, skuB.SkuId }.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            var signature = $"{sortedSkuIds[0]}::{sortedSkuIds[1]}";
            if (!seenBundleSignatures.Add(signature))
            {
                continue;
            }

            decimal regularPrice = skuA.RetailPriceDzd + skuB.RetailPriceDzd;
            decimal costPrice = skuA.CostPriceDzd + skuB.CostPriceDzd;

            decimal optimalDiscount = CalculateOptimalDiscount(regularPrice, costPrice, rule.Lift);
            decimal combinedPrice = Math.Round(regularPrice * (1m - optimalDiscount), 2, MidpointRounding.AwayFromZero);
            decimal jointMargin = combinedPrice - costPrice;

            var coOpType = !string.Equals(skuA.Category, skuB.Category, StringComparison.OrdinalIgnoreCase)
                ? BundleCoOpType.ComplementaryCategory
                : BundleCoOpType.CrossMerchantCart;

            var bundleId = $"bundle-{skuA.MerchantId}-{skuB.MerchantId}-{sortedSkuIds[0]}-{sortedSkuIds[1]}".ToLowerInvariant();
            var bundleName = $"{skuA.Name} & {skuB.Name} Collaborative Bundle";
            var justification = $"Discovered high cross-merchant affinity between '{skuA.MerchantId}' and '{skuB.MerchantId}' with Lift={rule.Lift:F2}, Support={rule.Support:P1}. Discount of {optimalDiscount:P0} yields projected joint margin of {jointMargin:N2} DZD.";

            discoveredBundles.Add(new CrossMerchantSkuBundle(
                BundleId: bundleId,
                Name: bundleName,
                CoOpType: coOpType,
                MerchantIds: [skuA.MerchantId, skuB.MerchantId],
                Skus: [skuA, skuB],
                RegularPriceDzd: regularPrice,
                BundleDiscountPct: optimalDiscount,
                CombinedPriceDzd: combinedPrice,
                LiftScore: rule.Lift,
                SupportScore: rule.Support,
                EstimatedJointMarginDzd: jointMargin,
                Justification: justification
            ));
        }

        return discoveredBundles
            .OrderByDescending(b => b.LiftScore)
            .ThenByDescending(b => b.EstimatedJointMarginDzd)
            .Take(request.MaxBundles > 0 ? request.MaxBundles : 10)
            .ToList();
    }

    public decimal CalculateOptimalDiscount(decimal regularPrice, decimal costPrice, double lift)
    {
        if (regularPrice <= 0) return 0.05m;
        if (costPrice < 0) costPrice = 0;

        decimal standaloneMargin = (regularPrice - costPrice) / regularPrice;

        // Base discount proportional to margin headroom (up to 30% of margin)
        decimal baseDiscount = standaloneMargin * 0.30m;

        // Lift adjustment:
        // When Lift >= 4.0, strong organic pull requires less discount concession.
        // Moderate Lift (2.0 - 2.5) benefits from a stronger incentive.
        decimal liftAdjustment = lift switch
        {
            >= 4.0 => -0.02m,
            >= 2.5 => 0.01m,
            _ => 0.03m
        };

        decimal calculatedDiscount = baseDiscount + liftAdjustment;
        decimal clampedDiscount = Math.Clamp(calculatedDiscount, 0.05m, 0.25m);

        // Retain at least 10% gross profit margin above combined cost
        decimal maxAllowedDiscount = regularPrice > 0
            ? Math.Max(0m, (regularPrice - (costPrice * 1.10m)) / regularPrice)
            : 0m;

        if (clampedDiscount > maxAllowedDiscount)
        {
            clampedDiscount = Math.Max(0.02m, maxAllowedDiscount);
        }

        return Math.Round(clampedDiscount, 2, MidpointRounding.AwayFromZero);
    }
}
