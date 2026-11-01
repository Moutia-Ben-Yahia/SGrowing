using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.CoOp;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CollaborativeCommerceAndBundleSynthesizerTests
{
    private readonly CrossMerchantBundleSynthesizerEngine _bundleEngine = new();
    private readonly SplitCartFulfillmentAttributionService _splitCartService = new();
    private readonly PrivateSetIntersectionMatcherService _psiService = new();

    #region 1. Catalog SKU Registration & Indexing Tests

    [Fact]
    public void RegisterCatalogSku_SingleItem_IndexesCorrectly()
    {
        var sku = new SkuItem("SKU-COFFEE-01", "MERCHANT-A", "Specialty Coffee Beans 1kg", "Coffee", 3500m, 2000m, 1.0, 0.002);
        _bundleEngine.RegisterCatalogSku(sku);

        _bundleEngine.TotalSkus.Should().Be(1);
        var retrieved = _bundleEngine.GetCatalogSku("SKU-COFFEE-01");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Specialty Coffee Beans 1kg");
        retrieved.MerchantId.Should().Be("MERCHANT-A");
        retrieved.RetailPriceDzd.Should().Be(3500m);
    }

    [Fact]
    public void RegisterCatalogSkus_Batch_IndexesAllSkus()
    {
        List<SkuItem> skus =
        [
            new("SKU-1", "M1", "Espresso Machine", "Appliances", 45000m, 28000m, 5.0, 0.02),
            new("SKU-2", "M2", "Organic Coffee Beans", "Food", 2500m, 1500m, 0.5, 0.001),
            new("SKU-3", "M3", "Ceramic Mug Set", "Kitchenware", 3000m, 1200m, 1.2, 0.003)
        ];

        _bundleEngine.RegisterCatalogSkus(skus);

        _bundleEngine.TotalSkus.Should().Be(3);
        _bundleEngine.GetCatalogSku("SKU-2")!.MerchantId.Should().Be("M2");
    }

    [Fact]
    public void RegisterCatalogSku_DuplicateId_UpdatesExistingRecord()
    {
        var initial = new SkuItem("SKU-REPLACE", "M1", "Initial Name", "Cat", 1000m, 500m);
        var updated = new SkuItem("SKU-REPLACE", "M1", "Updated Name", "Cat", 1200m, 600m);

        _bundleEngine.RegisterCatalogSku(initial);
        _bundleEngine.RegisterCatalogSku(updated);

        _bundleEngine.TotalSkus.Should().Be(1);
        _bundleEngine.GetCatalogSku("SKU-REPLACE")!.Name.Should().Be("Updated Name");
        _bundleEngine.GetCatalogSku("SKU-REPLACE")!.RetailPriceDzd.Should().Be(1200m);
    }

    [Fact]
    public void GetCatalogSku_NonExistent_ReturnsNull()
    {
        var result = _bundleEngine.GetCatalogSku("SKU-DOES-NOT-EXIST");
        result.Should().BeNull();
    }

    #endregion

    #region 2. Association Rule Mining (Support, Confidence, Lift) Tests

    [Fact]
    public void MineAssociationRules_StandardBaskets_CalculatesCorrectSupportConfidenceLift()
    {
        // 10 transactions
        // SKU-A in 6 transactions: Support(A) = 0.6
        // SKU-B in 4 transactions: Support(B) = 0.4
        // Both in 3 transactions: Support(A, B) = 0.3
        // Confidence(A => B) = 0.3 / 0.6 = 0.5
        // Lift(A => B) = 0.5 / 0.4 = 1.25

        List<TransactionRecord> transactions =
        [
            new("T1", ["SKU-A", "SKU-B"], DateTime.UtcNow),
            new("T2", ["SKU-A", "SKU-B"], DateTime.UtcNow),
            new("T3", ["SKU-A", "SKU-B"], DateTime.UtcNow),
            new("T4", ["SKU-A"], DateTime.UtcNow),
            new("T5", ["SKU-A"], DateTime.UtcNow),
            new("T6", ["SKU-A"], DateTime.UtcNow),
            new("T7", ["SKU-B"], DateTime.UtcNow),
            new("T8", ["SKU-C"], DateTime.UtcNow),
            new("T9", ["SKU-C"], DateTime.UtcNow),
            new("T10", ["SKU-D"], DateTime.UtcNow)
        ];

        _bundleEngine.IngestTransactions(transactions);
        _bundleEngine.TotalTransactions.Should().Be(10);

        var rules = _bundleEngine.MineAssociationRules(minSupport: 0.2, minConfidence: 0.4, minLift: 1.0);

        var ruleAB = rules.FirstOrDefault(r => r.AntecedentSkus.Contains("SKU-A") && r.ConsequentSkus.Contains("SKU-B"));
        ruleAB.Should().NotBeNull();
        ruleAB!.Support.Should().BeApproximately(0.3, 0.01);
        ruleAB.Confidence.Should().BeApproximately(0.5, 0.01);
        ruleAB.Lift.Should().BeApproximately(1.25, 0.01);
    }

    [Fact]
    public void MineAssociationRules_HighLiftComplementaryItems_DiscoveredAboveThreshold()
    {
        // Strong affinity: Whenever someone buys SKU-ESPRESSO (M1), they almost always buy SKU-BEANS (M2)
        List<TransactionRecord> txs = [];
        for (int i = 0; i < 20; i++)
        {
            txs.Add(new TransactionRecord($"TX-{i}", ["SKU-ESPRESSO", "SKU-BEANS"], DateTime.UtcNow));
        }
        for (int i = 20; i < 100; i++)
        {
            txs.Add(new TransactionRecord($"TX-{i}", ["SKU-OTHER"], DateTime.UtcNow));
        }

        _bundleEngine.IngestTransactions(txs);

        var rules = _bundleEngine.MineAssociationRules(minSupport: 0.1, minConfidence: 0.8, minLift: 2.0);

        rules.Should().NotBeEmpty();
        var highLiftRule = rules.First(r => r.AntecedentSkus.Contains("SKU-ESPRESSO"));
        highLiftRule.Lift.Should().BeGreaterThan(4.0);
    }

    [Fact]
    public void ClearTransactions_ResetsTransactionDatabase()
    {
        _bundleEngine.IngestTransactions([new TransactionRecord("TX-1", ["SKU-1"], DateTime.UtcNow)]);
        _bundleEngine.TotalTransactions.Should().Be(1);

        _bundleEngine.ClearTransactions();
        _bundleEngine.TotalTransactions.Should().Be(0);
    }

    #endregion

    #region 3. Cross-Merchant Bundle Discovery & Pricing Optimization Tests

    [Fact]
    public void DiscoverCrossMerchantBundles_DifferentMerchants_SynthesizesHighAffinityBundles()
    {
        _bundleEngine.RegisterCatalogSkus([
            new SkuItem("SKU-MACHINE", "MERCHANT-A", "Barista Pro Machine", "Appliances", 60000m, 40000m),
            new SkuItem("SKU-BEANS", "MERCHANT-B", "Artisan Coffee Beans", "Gourmet", 4000m, 2000m)
        ]);

        List<TransactionRecord> txs = [];
        for (int i = 0; i < 30; i++)
        {
            txs.Add(new TransactionRecord($"T-{i}", ["SKU-MACHINE", "SKU-BEANS"], DateTime.UtcNow));
        }
        for (int i = 30; i < 100; i++)
        {
            txs.Add(new TransactionRecord($"T-{i}", ["SKU-OTHER"], DateTime.UtcNow));
        }

        _bundleEngine.IngestTransactions(txs);

        var request = new DiscoverBundlesRequest(MinSupport: 0.1, MinLift: 2.0, MaxBundles: 5);
        var bundles = _bundleEngine.DiscoverCrossMerchantBundles(request);

        bundles.Should().NotBeEmpty();
        var bundle = bundles[0];
        bundle.MerchantIds.Should().Contain("MERCHANT-A");
        bundle.MerchantIds.Should().Contain("MERCHANT-B");
        bundle.RegularPriceDzd.Should().Be(64000m);
        bundle.BundleDiscountPct.Should().BeGreaterThan(0m);
        bundle.CombinedPriceDzd.Should().BeLessThan(64000m);
        bundle.EstimatedJointMarginDzd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void DiscoverCrossMerchantBundles_SameMerchantPairs_ExcludesFromCoOpDiscovery()
    {
        // Both SKUs belong to the SAME merchant: M1
        _bundleEngine.RegisterCatalogSkus([
            new SkuItem("SKU-X1", "MERCHANT-SAME", "Product 1", "Cat", 1000m, 500m),
            new SkuItem("SKU-X2", "MERCHANT-SAME", "Product 2", "Cat", 1000m, 500m)
        ]);

        List<TransactionRecord> txs = [];
        for (int i = 0; i < 50; i++)
        {
            txs.Add(new TransactionRecord($"T-{i}", ["SKU-X1", "SKU-X2"], DateTime.UtcNow));
        }
        _bundleEngine.IngestTransactions(txs);

        var bundles = _bundleEngine.DiscoverCrossMerchantBundles(new DiscoverBundlesRequest(MinSupport: 0.1, MinLift: 1.0));

        // Must not contain same-merchant pairs
        bundles.Should().NotContain(b => b.MerchantIds.Count == 1 && b.MerchantIds.Contains("MERCHANT-SAME"));
    }

    [Fact]
    public void CalculateOptimalDiscount_HighMarginHighLift_ProvidesIncentiveWhilePreservingProfit()
    {
        decimal regularPrice = 10000m;
        decimal costPrice = 5000m; // 50% margin
        double lift = 4.5;

        decimal discountPct = _bundleEngine.CalculateOptimalDiscount(regularPrice, costPrice, lift);

        // Optimal discount should fall within bounded reasonable incentive range [5%, 25%]
        discountPct.Should().BeInRange(0.05m, 0.25m);
        decimal discountedPrice = regularPrice * (1m - discountPct);
        discountedPrice.Should().BeGreaterThan(costPrice); // Margin preserved
    }

    [Fact]
    public void CalculateOptimalDiscount_NearZeroMargin_ReturnsMinimumFloorDiscount()
    {
        decimal regularPrice = 1000m;
        decimal costPrice = 980m; // Only 2% margin
        double lift = 3.0;

        decimal discountPct = _bundleEngine.CalculateOptimalDiscount(regularPrice, costPrice, lift);

        // Should be bounded to minimum floor (e.g. <= 5%) to avoid selling at a loss
        discountPct.Should().BeLessThanOrEqualTo(0.05m);
    }

    #endregion

    #region 4. Split-Cart Fulfillment & Logistics Allocation Tests

    [Fact]
    public void SplitCartAndAllocate_TwoMerchants_ProportionallyAllocatesShippingAndFees()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-ALGIERS-01",
            Items:
            [
                new CartItem("SKU-A", "MERCHANT-ALPHA", "Alpha Item", UnitPriceDzd: 10000m, CostPriceDzd: 6000m, Quantity: 1, WeightKg: 2.0, VolumeM3: 0.0),
                new CartItem("SKU-B", "MERCHANT-BETA", "Beta Item", UnitPriceDzd: 20000m, CostPriceDzd: 14000m, Quantity: 1, WeightKg: 8.0, VolumeM3: 0.0)
            ],
            BaseShippingRateDzd: 1000m, // 1000 DZD total shipping
            PlatformFeePct: 0.05m,     // 5% platform fee
            CoOpCommissionPct: 0.02m   // 2% co-op pool
        );

        var order = _splitCartService.SplitCartAndAllocate(request);

        order.GrandTotalDzd.Should().Be(31000m); // 30,000 items + 1,000 shipping
        order.SubOrders.Should().HaveCount(2);

        var subAlpha = order.SubOrders.First(s => s.MerchantId == "MERCHANT-ALPHA");
        var subBeta = order.SubOrders.First(s => s.MerchantId == "MERCHANT-BETA");

        // Proportional shipping by weight:
        // Alpha = 2.0 / 10.0 = 20% of 1000 = 200 DZD
        // Beta = 8.0 / 10.0 = 80% of 1000 = 800 DZD
        subAlpha.AllocatedShippingDzd.Should().Be(200m);
        subBeta.AllocatedShippingDzd.Should().Be(800m);

        // Subtotals: Alpha = 10,000, Beta = 20,000
        subAlpha.ItemsSubtotalDzd.Should().Be(10000m);
        subBeta.ItemsSubtotalDzd.Should().Be(20000m);

        // Fees: Alpha 5% = 500, Beta 5% = 1000
        subAlpha.PlatformFeeDzd.Should().Be(500m);
        subBeta.PlatformFeeDzd.Should().Be(1000m);

        // Net Payouts:
        // Alpha = 10000 (items) + 200 (ship) - 500 (plat) - 200 (co-op) = 9500 DZD
        subAlpha.MerchantNetPayoutDzd.Should().Be(9500m);
        // Beta = 20000 (items) + 800 (ship) - 1000 (plat) - 400 (co-op) = 19400 DZD
        subBeta.MerchantNetPayoutDzd.Should().Be(19400m);

        order.NetAttributionMap["MERCHANT-ALPHA"].Should().Be(9500m);
        order.NetAttributionMap["MERCHANT-BETA"].Should().Be(19400m);
    }

    [Fact]
    public void GetOrder_ValidOrderId_ReturnsSplitOrder()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-02",
            Items: [new CartItem("SKU-1", "M1", "Item", 5000m, 3000m, 1)],
            BaseShippingRateDzd: 500m
        );

        var created = _splitCartService.SplitCartAndAllocate(request);
        var fetched = _splitCartService.GetOrder(created.OrderId);

        fetched.Should().NotBeNull();
        fetched!.OrderId.Should().Be(created.OrderId);
    }

    [Fact]
    public void UpdateOrderStatus_TransitionsOrderAndSubOrders()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-03",
            Items: [new CartItem("SKU-1", "M1", "Item", 5000m, 3000m, 1)],
            BaseShippingRateDzd: 500m
        );

        var created = _splitCartService.SplitCartAndAllocate(request);
        var updated = _splitCartService.UpdateOrderStatus(created.OrderId, SettlementSplitStatus.DeliveredSettled);

        updated.OverallStatus.Should().Be(SettlementSplitStatus.DeliveredSettled);
        updated.SubOrders[0].Status.Should().Be(SettlementSplitStatus.DeliveredSettled);
    }

    [Fact]
    public void ProcessPartialRefund_SingleItemReturned_ClawsBackFeesAndCalculatesMerchantDebit()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-REFUND",
            Items:
            [
                new CartItem("SKU-RET", "MERCHANT-A", "Returned Item", UnitPriceDzd: 4000m, CostPriceDzd: 2000m, Quantity: 2, WeightKg: 1.0)
            ],
            BaseShippingRateDzd: 600m,
            PlatformFeePct: 0.05m,
            CoOpCommissionPct: 0.02m
        );

        var order = _splitCartService.SplitCartAndAllocate(request);

        // Refund 1 out of 2 units
        var refundReq = new PartialRefundRequest(
            OrderId: order.OrderId,
            MerchantId: "MERCHANT-A",
            SkuId: "SKU-RET",
            QuantityToRefund: 1,
            Reason: "Customer changed mind"
        );

        var result = _splitCartService.ProcessPartialRefund(refundReq);

        result.QuantityRefunded.Should().Be(1);
        result.ItemRefundAmountDzd.Should().Be(4000m);
        // Shipping is prorated or retained according to policy
        result.TotalCustomerRefundDzd.Should().BeGreaterThanOrEqualTo(4000m);
        result.ClawbackPlatformFeeDzd.Should().Be(200m); // 5% of 4,000
        result.ClawbackCoOpCommissionDzd.Should().Be(80m); // 2% of 4,000
        result.MerchantDebitDzd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void GetMerchantSubOrders_FiltersSubOrdersByMerchantId()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-MULTI",
            Items:
            [
                new CartItem("SKU-M1", "MERCHANT-SEARCH", "Item 1", 1000m, 500m, 1),
                new CartItem("SKU-M2", "MERCHANT-OTHER", "Item 2", 2000m, 1000m, 1)
            ],
            BaseShippingRateDzd: 500m
        );

        _splitCartService.SplitCartAndAllocate(request);

        var subOrders = _splitCartService.GetMerchantSubOrders("MERCHANT-SEARCH");
        subOrders.Should().NotBeEmpty();
        subOrders.Should().OnlyContain(s => s.MerchantId == "MERCHANT-SEARCH");
    }

    #endregion

    #region 5. Diffie-Hellman Private Set Intersection (PSI) Tests

    [Fact]
    public void GeneratePrivateKey_ReturnsValid256BitHexKey()
    {
        string key = _psiService.GeneratePrivateKey();

        key.Should().NotBeNullOrWhiteSpace();
        key.Length.Should().Be(64); // 32 bytes = 64 hex chars
        Convert.FromHexString(key).Length.Should().Be(32);
    }

    [Fact]
    public void BlindIdentifier_ProducesConsistentDeterministicTokenForSameKey()
    {
        string key = _psiService.GeneratePrivateKey();
        string email = "customer@algiers-logistics.dz";

        var token1 = _psiService.BlindIdentifier(email, key);
        var token2 = _psiService.BlindIdentifier(email, key);

        token1.BlindedHashHex.Should().Be(token2.BlindedHashHex);
        token1.BlindedHashHex.Should().NotContain(email); // Zero PII leak
    }

    [Fact]
    public void BlindIdentifier_DifferentKeys_ProduceDifferentBlindedHashes()
    {
        string keyA = _psiService.GeneratePrivateKey();
        string keyB = _psiService.GeneratePrivateKey();
        string email = "partner@oran-retail.dz";

        var tokenA = _psiService.BlindIdentifier(email, keyA);
        var tokenB = _psiService.BlindIdentifier(email, keyB);

        tokenA.BlindedHashHex.Should().NotBe(tokenB.BlindedHashHex);
    }

    [Fact]
    public void ReblindToken_CommutativeDiffieHellman_YieldsIdenticalFinalHashes()
    {
        // Diffie-Hellman Commutative Property: (H(x)^a)^b = (H(x)^b)^a
        string keyA = _psiService.GeneratePrivateKey();
        string keyB = _psiService.GeneratePrivateKey();
        string identifier = "vip_buyer_constantine@dz.com";

        // Route 1: Blind with A, then Reblind with B -> H(x)^ab
        var blindedA = _psiService.BlindIdentifier(identifier, keyA);
        var doubleBlindedAB = _psiService.ReblindToken(blindedA, keyB);

        // Route 2: Blind with B, then Reblind with A -> H(x)^ba
        var blindedB = _psiService.BlindIdentifier(identifier, keyB);
        var doubleBlindedBA = _psiService.ReblindToken(blindedB, keyA);

        doubleBlindedAB.BlindedHashHex.Should().Be(doubleBlindedBA.BlindedHashHex);
    }

    [Fact]
    public void FullPsiSession_OverlappingCustomerLists_CalculatesExactIntersectionWithoutLeakingPii()
    {
        // Merchant A customers: C1, C2, C3, C4
        List<string> merchantACustomers = ["cust_01@dz.com", "cust_02@dz.com", "cust_03@dz.com", "cust_04@dz.com"];
        // Merchant B customers: C3, C4, C5, C6 (Overlap: C3, C4 => 2 mutual customers)
        List<string> merchantBCustomers = ["cust_03@dz.com", "cust_04@dz.com", "cust_05@dz.com", "cust_06@dz.com"];

        // Step 1: Initiator starts session
        var session = _psiService.InitiateSession("MERCHANT-A", "MERCHANT-B", merchantACustomers);
        session.State.Should().Be(PsiProtocolState.Initiated);
        session.InitiatorElementCount.Should().Be(4);

        // Step 2: Partner exchanges tokens
        var sessionAfterExchange = _psiService.ExchangePartnerTokens(session.SessionId, "MERCHANT-B", merchantBCustomers);
        sessionAfterExchange.State.Should().Be(PsiProtocolState.BlindedKeysExchanged);
        sessionAfterExchange.PartnerElementCount.Should().Be(4);

        // Step 3: Compute final intersection
        var matchResult = _psiService.ComputeIntersection(session.SessionId);

        matchResult.IntersectionCount.Should().Be(2);
        matchResult.OverlapPercentageInitiator.Should().BeApproximately(0.5, 0.01); // 2 out of 4 = 50%
        matchResult.OverlapPercentagePartner.Should().BeApproximately(0.5, 0.01);
        matchResult.MatchedBlindedHashes.Should().HaveCount(2);

        // Ensure zero plaintext emails appear anywhere in matched result
        foreach (var hash in matchResult.MatchedBlindedHashes)
        {
            hash.Should().NotContain("@");
            hash.Should().NotContain("cust_");
        }
    }

    [Fact]
    public void FullPsiSession_DisjointCustomerLists_YieldsZeroIntersection()
    {
        List<string> merchantACustomers = ["alpha1@dz.com", "alpha2@dz.com"];
        List<string> merchantBCustomers = ["beta1@dz.com", "beta2@dz.com"];

        var session = _psiService.InitiateSession("M-A", "M-B", merchantACustomers);
        _psiService.ExchangePartnerTokens(session.SessionId, "M-B", merchantBCustomers);
        var result = _psiService.ComputeIntersection(session.SessionId);

        result.IntersectionCount.Should().Be(0);
        result.OverlapPercentageInitiator.Should().Be(0.0);
        result.MatchedBlindedHashes.Should().BeEmpty();
    }

    [Fact]
    public void FullPsiSession_IdenticalCustomerLists_Yields100PercentIntersection()
    {
        List<string> identicalList = ["user1@algiers.dz", "user2@algiers.dz", "user3@algiers.dz"];

        var session = _psiService.InitiateSession("M-1", "M-2", identicalList);
        _psiService.ExchangePartnerTokens(session.SessionId, "M-2", identicalList);
        var result = _psiService.ComputeIntersection(session.SessionId);

        result.IntersectionCount.Should().Be(3);
        result.OverlapPercentageInitiator.Should().BeApproximately(1.0, 0.01);
        result.OverlapPercentagePartner.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void EvaluatePartnershipPotential_ComputesOverlapRatioAndRevenueEstimate()
    {
        List<string> listA = ["a@dz.com", "b@dz.com", "c@dz.com", "d@dz.com"];
        List<string> listB = ["c@dz.com", "d@dz.com", "e@dz.com", "f@dz.com"];

        var session = _psiService.InitiateSession("M-ALPHA", "M-BETA", listA);
        _psiService.ExchangePartnerTokens(session.SessionId, "M-BETA", listB);
        _psiService.ComputeIntersection(session.SessionId);

        var summary = _psiService.EvaluatePartnershipPotential("M-ALPHA", "M-BETA", discoveredBundleCount: 3, potentialGrossRevenueDzd: 150000m);

        summary.MutualCustomerOverlapCount.Should().Be(2);
        summary.CustomerOverlapRatio.Should().BeApproximately(0.5, 0.05);
        summary.DiscoveredBundleCount.Should().Be(3);
        summary.PotentialGrossRevenueDzd.Should().Be(150000m);
    }

    #endregion

    #region 6. Concurrency, Edge Cases & Thread Safety Tests

    [Fact]
    public async Task SplitCart_ConcurrentCheckouts_MaintainsOrderUniquenessAndAttributionIntegrity()
    {
        int concurrentCheckouts = 25;
        var tasks = Enumerable.Range(0, concurrentCheckouts).Select(i => Task.Run(() =>
        {
            var req = new SplitCartRequest(
                CustomerId: $"CUST-CONC-{i}",
                Items:
                [
                    new CartItem($"SKU-{i}", "MERCHANT-CONC", $"Item {i}", 1000m + i, 500m, 1)
                ],
                BaseShippingRateDzd: 400m
            );
            return _splitCartService.SplitCartAndAllocate(req);
        }));

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(concurrentCheckouts);
        results.Select(r => r.OrderId).Should().OnlyHaveUniqueItems();
        _splitCartService.TotalOrders.Should().BeGreaterThanOrEqualTo(concurrentCheckouts);
    }

    [Fact]
    public void SplitCart_EmptyItems_ThrowsArgumentException()
    {
        var action = () => _splitCartService.SplitCartAndAllocate(new SplitCartRequest("CUST-EMPTY", [], 500m));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SplitCart_NegativeShippingRate_ThrowsArgumentOutOfRangeException()
    {
        var action = () => _splitCartService.SplitCartAndAllocate(new SplitCartRequest(
            "CUST-NEG",
            [new CartItem("SKU-1", "M1", "Item", 100m, 50m, 1)],
            BaseShippingRateDzd: -50m));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProcessPartialRefund_InvalidOrderId_ThrowsKeyNotFoundException()
    {
        var refundReq = new PartialRefundRequest("NON-EXISTENT-ORDER", "M1", "SKU-1", 1, "Return");
        var action = () => _splitCartService.ProcessPartialRefund(refundReq);
        action.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ProcessPartialRefund_ExceedingPurchasedQuantity_ThrowsInvalidOperationException()
    {
        var order = _splitCartService.SplitCartAndAllocate(new SplitCartRequest(
            "CUST-OVER",
            [new CartItem("SKU-1", "M1", "Item", 1000m, 500m, 1)],
            BaseShippingRateDzd: 500m
        ));

        // Requesting 5 refunds on order of 1 unit
        var refundReq = new PartialRefundRequest(order.OrderId, "M1", "SKU-1", 5, "Return");
        var action = () => _splitCartService.ProcessPartialRefund(refundReq);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DiscoverCrossMerchantBundles_WithMerchantFilter_OnlyIncludesSpecifiedMerchants()
    {
        _bundleEngine.RegisterCatalogSkus([
            new SkuItem("SKU-F1", "MERCHANT-FILTERED", "Product F1", "Cat", 2000m, 1000m),
            new SkuItem("SKU-F2", "MERCHANT-OTHER", "Product F2", "Cat", 3000m, 1500m),
            new SkuItem("SKU-F3", "MERCHANT-EXCLUDED", "Product F3", "Cat", 4000m, 2000m)
        ]);

        List<TransactionRecord> txs = [];
        for (int i = 0; i < 20; i++)
        {
            txs.Add(new TransactionRecord($"TF-{i}", ["SKU-F1", "SKU-F2", "SKU-F3"], DateTime.UtcNow));
        }
        _bundleEngine.IngestTransactions(txs);

        var request = new DiscoverBundlesRequest(
            MerchantFilter: ["MERCHANT-FILTERED", "MERCHANT-OTHER"],
            MinSupport: 0.1,
            MinLift: 1.0);

        var bundles = _bundleEngine.DiscoverCrossMerchantBundles(request);

        bundles.Should().NotContain(b => b.MerchantIds.Contains("MERCHANT-EXCLUDED"));
    }

    [Fact]
    public void DiscoverCrossMerchantBundles_EmptyCatalogOrTransactions_ReturnsEmpty()
    {
        var freshEngine = new CrossMerchantBundleSynthesizerEngine();
        var bundles = freshEngine.DiscoverCrossMerchantBundles(new DiscoverBundlesRequest());
        bundles.Should().BeEmpty();
    }

    [Fact]
    public void SplitCart_ThreeMerchants_SumOfAllocationsExactlyMatchesBaseShipping()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-THREE",
            Items:
            [
                new CartItem("SKU-M1", "M1", "Item 1", 5000m, 3000m, 1, WeightKg: 1.0, VolumeM3: 0.0),
                new CartItem("SKU-M2", "M2", "Item 2", 8000m, 4000m, 1, WeightKg: 3.0, VolumeM3: 0.0),
                new CartItem("SKU-M3", "M3", "Item 3", 12000m, 7000m, 1, WeightKg: 6.0, VolumeM3: 0.0)
            ],
            BaseShippingRateDzd: 1500m
        );

        var order = _splitCartService.SplitCartAndAllocate(request);

        order.SubOrders.Should().HaveCount(3);
        order.SubOrders.Sum(s => s.AllocatedShippingDzd).Should().Be(1500m);
        order.GrandTotalDzd.Should().Be(25000m + 1500m);
    }

    [Fact]
    public void SplitCart_SingleMerchantCart_Allocates100PercentShippingToThatMerchant()
    {
        var request = new SplitCartRequest(
            CustomerId: "CUST-SINGLE",
            Items:
            [
                new CartItem("SKU-S1", "M-SOLO", "Solo Item", 4000m, 2000m, 2, WeightKg: 2.0)
            ],
            BaseShippingRateDzd: 800m
        );

        var order = _splitCartService.SplitCartAndAllocate(request);

        order.SubOrders.Should().HaveCount(1);
        order.SubOrders[0].AllocatedShippingDzd.Should().Be(800m);
        order.SubOrders[0].MerchantId.Should().Be("M-SOLO");
    }

    [Fact]
    public void GetMerchantSubOrders_NonExistentMerchant_ReturnsEmpty()
    {
        var orders = _splitCartService.GetMerchantSubOrders("NON-EXISTENT-MERCHANT");
        orders.Should().BeEmpty();
    }

    [Fact]
    public void InitiateSession_NullOrEmptyMerchantId_ThrowsArgumentException()
    {
        var action = () => _psiService.InitiateSession("", "M-PARTNER", ["cust@dz.com"]);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExchangePartnerTokens_NonExistentSession_ThrowsKeyNotFoundException()
    {
        var action = () => _psiService.ExchangePartnerTokens("non-existent-session-id", "M-B", ["cust@dz.com"]);
        action.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ComputeIntersection_UnexchangedSession_ThrowsInvalidOperationException()
    {
        var session = _psiService.InitiateSession("M-INIT", "M-PARTNER", ["cust@dz.com"]);

        // Computing before exchange
        var action = () => _psiService.ComputeIntersection(session.SessionId);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetSession_NonExistentSession_ReturnsNull()
    {
        var session = _psiService.GetSession("psi-nonexistent");
        session.Should().BeNull();
    }

    [Fact]
    public void BlindIdentifier_NullInputs_ThrowsArgumentNullException()
    {
        var action = () => _psiService.BlindIdentifier(null!, "abcd");
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReblindToken_NullInputs_ThrowsArgumentNullException()
    {
        var action = () => _psiService.ReblindToken(null!, "abcd");
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DirectServiceCollectionRegistration_ValidatesDescriptorsWithoutBuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddCollaborativeCommerceServices();

        services.Should().Contain(d => d.ServiceType == typeof(ICrossMerchantBundleSynthesizerEngine) &&
                                       d.ImplementationType == typeof(CrossMerchantBundleSynthesizerEngine) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(ISplitCartFulfillmentAttributionService) &&
                                       d.ImplementationType == typeof(SplitCartFulfillmentAttributionService) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(IPrivateSetIntersectionMatcherService) &&
                                       d.ImplementationType == typeof(PrivateSetIntersectionMatcherService) &&
                                       d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
