using System.Xml.Linq;
using SGrowing.Api.Modules.Commerce.Affiliates;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class AffiliateCommissionServiceTests
{
    private readonly AffiliateCommissionService _service = new();

    public AffiliateCommissionServiceTests()
    {
        // Ensure fresh preseeded state before each test class run
        _service.ResetToPreseededDataAsync().GetAwaiter().GetResult();
    }

    #region 1. Partner Registration & Lookup Tests

    [Fact]
    public async Task RegisterPartner_ValidDirectPartner_GeneratesUniqueReferralCodeAndSlug()
    {
        var request = new RegisterAffiliateRequest(
            Name: "Nordic Tech Reviewer",
            Email: "contact@nordictechreviewer.com",
            TierLevel: AffiliateTierLevel.Level1Direct);

        var partner = await _service.RegisterPartnerAsync(request);

        Assert.NotNull(partner);
        Assert.Equal("Nordic Tech Reviewer", partner.Name);
        Assert.Equal("contact@nordictechreviewer.com", partner.Email);
        Assert.NotEmpty(partner.ReferralCodes);
        Assert.StartsWith("NORDIC", partner.ReferralCodes[0]);
        Assert.Equal("nordic-tech-reviewer", partner.TrackingSlug);
        Assert.Equal(AffiliateStatus.Active, partner.Status);
    }

    [Fact]
    public async Task RegisterPartner_MissingName_ThrowsArgumentException()
    {
        var request = new RegisterAffiliateRequest(
            Name: "   ",
            Email: "valid@email.com",
            TierLevel: AffiliateTierLevel.Level1Direct);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterPartnerAsync(request));
    }

    [Fact]
    public async Task RegisterPartner_MissingEmail_ThrowsArgumentException()
    {
        var request = new RegisterAffiliateRequest(
            Name: "Valid Name",
            Email: "",
            TierLevel: AffiliateTierLevel.Level1Direct);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterPartnerAsync(request));
    }

    [Fact]
    public async Task RegisterPartner_DuplicateReferralCode_AppendsUniqueCounter()
    {
        var request1 = new RegisterAffiliateRequest(
            Name: "Alpha Partner",
            Email: "alpha1@example.com",
            TierLevel: AffiliateTierLevel.Level1Direct,
            CustomReferralCode: "ALPHACODE");

        var p1 = await _service.RegisterPartnerAsync(request1);

        var request2 = new RegisterAffiliateRequest(
            Name: "Alpha Duplicate",
            Email: "alpha2@example.com",
            TierLevel: AffiliateTierLevel.Level1Direct,
            CustomReferralCode: "ALPHACODE");

        var p2 = await _service.RegisterPartnerAsync(request2);

        Assert.Equal("ALPHACODE", p1.ReferralCodes[0]);
        Assert.Equal("ALPHACODE1", p2.ReferralCodes[0]);
    }

    [Fact]
    public async Task ResolvePartner_ByReferralCode_ReturnsCorrectPartner()
    {
        var resolved = await _service.ResolvePartnerByCodeOrSlugAsync("APEX15");

        Assert.NotNull(resolved);
        Assert.Equal("Apex SaaS Reviews", resolved.Name);
    }

    [Fact]
    public async Task ResolvePartner_ByTrackingSlug_ReturnsCorrectPartner()
    {
        var resolved = await _service.ResolvePartnerByCodeOrSlugAsync("venture-tech");

        Assert.NotNull(resolved);
        Assert.Equal("Venture Tech Media", resolved.Name);
    }

    [Fact]
    public async Task ResolvePartner_ByCustomLandingDomain_ReturnsCorrectPartner()
    {
        var resolved = await _service.ResolvePartnerByCodeOrSlugAsync("partners.globalscalehub.eu");

        Assert.NotNull(resolved);
        Assert.Equal("Global Scale Distribution Hub", resolved.Name);
    }

    #endregion

    #region 2. Attribution & Cookie Window Tests

    [Fact]
    public async Task TrackClick_GeneratesAttributionTokenWithValidTTL()
    {
        var cookie = await _service.TrackClickAsync("APEX15", customerId: "CUST-991");

        Assert.NotNull(cookie);
        Assert.StartsWith("aff_", cookie.Token);
        Assert.Equal("APEX15", cookie.ReferralCode);
        Assert.NotNull(cookie.ExpiresAtUtc);
        Assert.True(cookie.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task TrackClick_LifetimeAttribution_SetsNullExpiration()
    {
        // Cloud Stack Creator has AttributionWindowDays = 0 (Lifetime)
        var cookie = await _service.TrackClickAsync("CLOUDSTACK");

        Assert.NotNull(cookie);
        Assert.Null(cookie.ExpiresAtUtc);
    }

    [Fact]
    public async Task TrackConversion_WithValidCookieToken_AttributesSuccessfully()
    {
        var cookie = await _service.TrackClickAsync("APEX15", customerId: "CUST-100");

        var conversionReq = new TrackConversionRequest(
            OrderId: "ORD-TEST-1001",
            OrderGmv: 500m,
            AttributionToken: cookie.Token,
            CustomerId: "CUST-100");

        var result = await _service.TrackConversionAsync(conversionReq);

        Assert.True(result.IsAttributed);
        Assert.NotEmpty(result.CreatedCommissions);
        Assert.Equal(cookie.AffiliateId, result.DirectAffiliateId);
    }

    [Fact]
    public async Task TrackConversion_ExpiredCookieToken_RejectsAttribution()
    {
        var cookie = await _service.TrackClickAsync("APEX15");

        // Simulate order 45 days after click when window is 30 days
        var conversionReq = new TrackConversionRequest(
            OrderId: "ORD-EXPIRED-1",
            OrderGmv: 400m,
            AttributionToken: cookie.Token,
            OrderTimestampUtc: DateTime.UtcNow.AddDays(40));

        var result = await _service.TrackConversionAsync(conversionReq);

        Assert.False(result.IsAttributed);
        Assert.Contains("expired", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.CreatedCommissions);
    }

    [Fact]
    public async Task TrackConversion_OrderTimeBeyondAttributionWindow_RejectsAttribution()
    {
        var clickTime = DateTime.UtcNow.AddDays(-35); // 35 days ago (APEX15 window is 30 days)

        var conversionReq = new TrackConversionRequest(
            OrderId: "ORD-EXPIRED-2",
            OrderGmv: 300m,
            ReferralCode: "APEX15",
            ClickTimestampUtc: clickTime,
            OrderTimestampUtc: DateTime.UtcNow);

        var result = await _service.TrackConversionAsync(conversionReq);

        Assert.False(result.IsAttributed);
        Assert.Contains("outside attribution window", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 3. Commission Calculation Models Tests

    [Fact]
    public async Task CalculateCommission_PercentageOfGmv_ComputesCorrectly()
    {
        // Apex is 15% of GMV
        var req = new TrackConversionRequest(
            OrderId: "ORD-GMV-1",
            OrderGmv: 200m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        Assert.True(result.IsAttributed);
        var l1 = result.CreatedCommissions.First(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        Assert.Equal(30.0m, l1.CommissionAmount); // 15% of 200
        Assert.Equal(15.0m, l1.CommissionRate);
    }

    [Fact]
    public async Task CalculateCommission_PercentageOfMargin_ComputesMarginBasedReward()
    {
        // Venture Tech is 20% of Margin (GMV - COGS)
        var req = new TrackConversionRequest(
            OrderId: "ORD-MARGIN-1",
            OrderGmv: 1000m,
            CostOfGoodsSold: 600m, // Margin = 400
            ReferralCode: "VENTURE20");

        var result = await _service.TrackConversionAsync(req);

        Assert.True(result.IsAttributed);
        var l1 = result.CreatedCommissions.First(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        Assert.Equal(80.0m, l1.CommissionAmount); // 20% of 400
        Assert.Equal(400.0m, l1.OrderMargin);
    }

    [Fact]
    public async Task CalculateCommission_FixedBountyPerOrder_AwardsFlatAmount()
    {
        // Cloud Stack is FixedBounty $100 per order
        var req = new TrackConversionRequest(
            OrderId: "ORD-BOUNTY-1",
            OrderGmv: 250m,
            ReferralCode: "CLOUDSTACK");

        var result = await _service.TrackConversionAsync(req);

        Assert.True(result.IsAttributed);
        var l1 = result.CreatedCommissions.First(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        Assert.Equal(100.0m, l1.CommissionAmount);
    }

    [Fact]
    public async Task CalculateCommission_RecurringSubscriptionSplit_ComputesSplit()
    {
        var partner = await _service.RegisterPartnerAsync(new RegisterAffiliateRequest(
            Name: "SaaS Recurring Partner",
            Email: "recurring@saas.io",
            TierLevel: AffiliateTierLevel.Level1Direct,
            CommissionRule: new CommissionRule
            {
                RuleType = CommissionRuleType.RecurringSubscriptionSplit,
                RatePercentage = 25.0m
            }));

        var req = new TrackConversionRequest(
            OrderId: "SUB-REC-001",
            OrderGmv: 120m,
            ReferralCode: partner.ReferralCodes[0],
            IsRecurringSubscription: true);

        var result = await _service.TrackConversionAsync(req);

        Assert.True(result.IsAttributed);
        var l1 = result.CreatedCommissions.First(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        Assert.Equal(30.0m, l1.CommissionAmount); // 25% of 120
    }

    [Fact]
    public async Task CalculateCommission_MilestoneBonus_AwardsBonusOnMilestoneHit()
    {
        var partner = await _service.RegisterPartnerAsync(new RegisterAffiliateRequest(
            Name: "Milestone Hunter",
            Email: "hunter@milestones.com",
            TierLevel: AffiliateTierLevel.Level1Direct,
            CommissionRule: new CommissionRule
            {
                RuleType = CommissionRuleType.MilestoneBonus,
                RatePercentage = 10.0m,
                MilestoneTargetSales = 2,
                MilestoneBonusAmount = 50.0m
            }));

        var code = partner.ReferralCodes[0];

        // 1st conversion: regular 10%
        var res1 = await _service.TrackConversionAsync(new TrackConversionRequest("ORD-M1", 100m, ReferralCode: code));
        var l1Res1 = res1.CreatedCommissions.First(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        Assert.Equal(10.0m, l1Res1.CommissionAmount);

        // 2nd conversion: 10% + $50 bonus = $60
        var res2 = await _service.TrackConversionAsync(new TrackConversionRequest("ORD-M2", 100m, ReferralCode: code));
        var l1Res2 = res2.CreatedCommissions.First(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        Assert.Equal(60.0m, l1Res2.CommissionAmount);
    }

    [Fact]
    public async Task CalculateCommission_BelowMinimumOrderThreshold_RejectsCommission()
    {
        // Apex has MinimumOrderThreshold = $25
        var req = new TrackConversionRequest(
            OrderId: "ORD-TINY-1",
            OrderGmv: 15.0m, // Below $25
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        Assert.False(result.IsAttributed);
        Assert.Contains("below minimum threshold", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.CreatedCommissions);
    }

    [Fact]
    public async Task CalculateCommission_ExcludedProductCategory_RejectsCommission()
    {
        // Apex has ExcludedCategories = ["GiftCards", "HardwareClearance"]
        var req = new TrackConversionRequest(
            OrderId: "ORD-EXCL-1",
            OrderGmv: 200m,
            ProductCategory: "GiftCards",
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        Assert.False(result.IsAttributed);
        Assert.Contains("excluded", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.CreatedCommissions);
    }

    #endregion

    #region 4. Multi-Tier Hierarchy & Cascade Overrides Tests

    [Fact]
    public async Task TrackConversion_TwoTierHierarchy_CalculatesL1AndL2Commissions()
    {
        // Apex partner has parent Nordic Growth Media (L2), which has parent Global Hub (L3)
        var req = new TrackConversionRequest(
            OrderId: "ORD-CASCADE-1",
            OrderGmv: 1000m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        Assert.True(result.IsAttributed);
        Assert.Equal(3, result.CreatedCommissions.Count); // L1, L2, L3

        var l1 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        var l2 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level2Parent);

        Assert.Equal(150.0m, l1.CommissionAmount); // 15%
        Assert.Equal(50.0m, l2.CommissionAmount);  // 5%
    }

    [Fact]
    public async Task TrackConversion_ThreeTierHierarchy_CalculatesL1L2L3Commissions()
    {
        var req = new TrackConversionRequest(
            OrderId: "ORD-CASCADE-3T",
            OrderGmv: 2000m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        var l1 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level1Direct);
        var l2 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level2Parent);
        var l3 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level3Master);

        Assert.Equal(300.0m, l1.CommissionAmount); // 15% of 2000
        Assert.Equal(100.0m, l2.CommissionAmount); // 5% of 2000
        Assert.Equal(40.0m, l3.CommissionAmount);  // 2% of 2000
        Assert.Equal(440.0m, result.TotalCommissionsAwarded);
    }

    [Fact]
    public async Task TrackConversion_CustomTier2Override_UsesOverriddenRate()
    {
        var partner = await _service.RegisterPartnerAsync(new RegisterAffiliateRequest(
            Name: "Custom Override Partner",
            Email: "override@partner.com",
            TierLevel: AffiliateTierLevel.Level1Direct,
            ParentPartnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CommissionRule: new CommissionRule
            {
                RuleType = CommissionRuleType.PercentageOfGmv,
                RatePercentage = 12.0m,
                Tier2RateOverride = 8.0m // Overridden to 8% instead of default 5%
            }));

        var req = new TrackConversionRequest(
            OrderId: "ORD-OVR-1",
            OrderGmv: 1000m,
            ReferralCode: partner.ReferralCodes[0]);

        var result = await _service.TrackConversionAsync(req);

        var l2 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level2Parent);
        Assert.Equal(80.0m, l2.CommissionAmount); // 8% of 1000
        Assert.Equal(8.0m, l2.CommissionRate);
    }

    [Fact]
    public async Task TrackConversion_CustomTier3Override_UsesOverriddenRate()
    {
        var partner = await _service.RegisterPartnerAsync(new RegisterAffiliateRequest(
            Name: "Custom L3 Override Partner",
            Email: "l3override@partner.com",
            TierLevel: AffiliateTierLevel.Level1Direct,
            ParentPartnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CommissionRule: new CommissionRule
            {
                RatePercentage = 10.0m,
                Tier3RateOverride = 4.0m // Overridden to 4% instead of default 2%
            }));

        var req = new TrackConversionRequest(
            OrderId: "ORD-OVR-L3",
            OrderGmv: 1000m,
            ReferralCode: partner.ReferralCodes[0]);

        var result = await _service.TrackConversionAsync(req);

        var l3 = result.CreatedCommissions.Single(c => c.TierLevel == AffiliateTierLevel.Level3Master);
        Assert.Equal(40.0m, l3.CommissionAmount); // 4% of 1000
    }

    [Fact]
    public async Task TrackConversion_InactiveParentPartner_SkipsInactiveTier()
    {
        var l2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await _service.UpdatePartnerAsync(l2Id, new UpdateAffiliateRequest(Status: AffiliateStatus.Suspended));

        var req = new TrackConversionRequest(
            OrderId: "ORD-INACTIVE-P",
            OrderGmv: 500m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        // L2 is suspended, so only L1 should be created
        Assert.Single(result.CreatedCommissions);
        Assert.Equal(AffiliateTierLevel.Level1Direct, result.CreatedCommissions[0].TierLevel);

        // Restore L2 status
        await _service.UpdatePartnerAsync(l2Id, new UpdateAffiliateRequest(Status: AffiliateStatus.Active));
    }

    [Fact]
    public async Task TrackConversion_IndependentAffiliate_NoParentCascadesOnlyL1()
    {
        // Cloud Stack Creator has ParentPartnerId = null
        var req = new TrackConversionRequest(
            OrderId: "ORD-INDEP-1",
            OrderGmv: 500m,
            ReferralCode: "CLOUDSTACK");

        var result = await _service.TrackConversionAsync(req);

        Assert.Single(result.CreatedCommissions);
        Assert.Equal(AffiliateTierLevel.Level1Direct, result.CreatedCommissions[0].TierLevel);
        Assert.Equal(100.0m, result.CreatedCommissions[0].CommissionAmount);
    }

    #endregion

    #region 5. Anti-Fraud Escrow & Maturity Lifecycle Tests

    [Fact]
    public async Task TrackConversion_InitialStateIsPendingEscrowWithMaturesAtCalculated()
    {
        var req = new TrackConversionRequest(
            OrderId: "ORD-ESCROW-1",
            OrderGmv: 400m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);

        foreach (var comm in result.CreatedCommissions)
        {
            Assert.Equal(CommissionState.PendingEscrow, comm.State);
            Assert.Equal(14, comm.EscrowHoldbackDays);
            Assert.Equal(comm.CreatedAtUtc.AddDays(14), comm.MaturesAtUtc);
            Assert.Null(comm.ApprovedAtUtc);
        }
    }

    [Fact]
    public async Task MaturitySweep_BeforeHoldbackWindow_LeavesPendingEscrow()
    {
        var req = new TrackConversionRequest(
            OrderId: "ORD-HOLD-1",
            OrderGmv: 200m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);
        var txId = result.CreatedCommissions[0].Id;

        // Run sweep as of today (only 0 days old, requires 14)
        await _service.ProcessMaturitySweepAsync(DateTime.UtcNow);

        var tx = await _service.GetCommissionByIdAsync(txId);
        Assert.NotNull(tx);
        Assert.Equal(CommissionState.PendingEscrow, tx.State);
    }

    [Fact]
    public async Task MaturitySweep_AfterHoldbackWindow_TransitionsPendingToApproved()
    {
        var req = new TrackConversionRequest(
            OrderId: "ORD-HOLD-2",
            OrderGmv: 300m,
            ReferralCode: "APEX15");

        var result = await _service.TrackConversionAsync(req);
        var txId = result.CreatedCommissions[0].Id;

        // Run sweep 15 days in future
        int transitioned = await _service.ProcessMaturitySweepAsync(DateTime.UtcNow.AddDays(15));

        Assert.True(transitioned >= 1);
        var tx = await _service.GetCommissionByIdAsync(txId);
        Assert.NotNull(tx);
        Assert.Equal(CommissionState.Approved, tx.State);
        Assert.NotNull(tx.ApprovedAtUtc);
    }

    [Fact]
    public async Task MaturitySweep_UpdatesPartnerMetricsApprovedAmount()
    {
        var apexId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var partnerBefore = await _service.GetPartnerByIdAsync(apexId);
        decimal initialApproved = partnerBefore!.Metrics.ApprovedAvailableAmount;

        var req = new TrackConversionRequest("ORD-MAT-METRICS", 1000m, ReferralCode: "APEX15");
        await _service.TrackConversionAsync(req);

        await _service.ProcessMaturitySweepAsync(DateTime.UtcNow.AddDays(15));

        var partnerAfter = await _service.GetPartnerByIdAsync(apexId);
        Assert.True(partnerAfter!.Metrics.ApprovedAvailableAmount > initialApproved);
    }

    [Fact]
    public async Task ReverseCommission_ByOrderId_ClawsBackAllAssociatedTiers()
    {
        var req = new TrackConversionRequest(
            OrderId: "ORD-REFUND-ME",
            OrderGmv: 1000m,
            ReferralCode: "APEX15");

        var conv = await _service.TrackConversionAsync(req);
        Assert.Equal(3, conv.CreatedCommissions.Count);

        var reverseReq = new ReverseCommissionRequest(
            OrderId: "ORD-REFUND-ME",
            Reason: "Customer exercised 14-day statutory return guarantee");

        var reversed = await _service.ReverseCommissionAsync(reverseReq);

        Assert.Equal(3, reversed.Count);
        foreach (var rev in reversed)
        {
            Assert.Equal(CommissionState.RefundReversed, rev.State);
            Assert.Equal("Customer exercised 14-day statutory return guarantee", rev.ReversalReason);
            Assert.NotNull(rev.ReversedAtUtc);
        }
    }

    [Fact]
    public async Task ReverseCommission_ByCommissionId_ClawsBackTargetCommissionAndUpdatesMetrics()
    {
        var req = new TrackConversionRequest(
            OrderId: "ORD-SINGLE-CLAW",
            OrderGmv: 500m,
            ReferralCode: "APEX15");

        var conv = await _service.TrackConversionAsync(req);
        var targetTx = conv.CreatedCommissions[0];

        var reversed = await _service.ReverseCommissionAsync(new ReverseCommissionRequest(
            CommissionId: targetTx.Id,
            Reason: "Fraudulent chargeback flagged by Stripe"));

        Assert.Single(reversed);
        Assert.Equal(targetTx.Id, reversed[0].Id);
        Assert.Equal(CommissionState.RefundReversed, reversed[0].State);

        var partner = await _service.GetPartnerByIdAsync(targetTx.AffiliateId);
        Assert.True(partner!.Metrics.RefundReversalCount > 0);
    }

    #endregion

    #region 6. Refund Clawback & Edge Cases Tests

    [Fact]
    public async Task ReverseCommission_NonExistentOrder_ReturnsEmptyList()
    {
        var reversed = await _service.ReverseCommissionAsync(new ReverseCommissionRequest(
            OrderId: "NON-EXISTENT-ORDER-12345",
            Reason: "Testing invalid order"));

        Assert.Empty(reversed);
    }

    [Fact]
    public async Task ReverseCommission_MissingBothOrderIdAndCommissionId_ThrowsArgumentException()
    {
        var req = new ReverseCommissionRequest(OrderId: null, CommissionId: null);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ReverseCommissionAsync(req));
    }

    [Fact]
    public async Task ReverseCommission_AlreadyReversedCommission_IsNotDoubleReversed()
    {
        var req = new TrackConversionRequest("ORD-DOUBLE-REV", 400m, ReferralCode: "APEX15");
        await _service.TrackConversionAsync(req);

        var rev1 = await _service.ReverseCommissionAsync(new ReverseCommissionRequest(OrderId: "ORD-DOUBLE-REV"));
        Assert.NotEmpty(rev1);

        var rev2 = await _service.ReverseCommissionAsync(new ReverseCommissionRequest(OrderId: "ORD-DOUBLE-REV"));
        Assert.Empty(rev2); // Already reversed, so no active commissions clawed back
    }

    [Fact]
    public async Task ReverseCommission_TracksReversalReasonAndTimestamp()
    {
        var req = new TrackConversionRequest("ORD-REASON-CHECK", 200m, ReferralCode: "APEX15");
        var conv = await _service.TrackConversionAsync(req);

        await _service.ReverseCommissionAsync(new ReverseCommissionRequest(
            OrderId: "ORD-REASON-CHECK",
            Reason: "Friendly fraud dispute"));

        var comm = await _service.GetCommissionByIdAsync(conv.CreatedCommissions[0].Id);
        Assert.Equal("Friendly fraud dispute", comm!.ReversalReason);
        Assert.NotNull(comm.ReversedAtUtc);
    }

    [Fact]
    public async Task TrackConversion_InactiveAffiliate_RejectsAttribution()
    {
        var partner = await _service.RegisterPartnerAsync(new RegisterAffiliateRequest(
            Name: "Inactive Tester",
            Email: "inactive@test.com",
            TierLevel: AffiliateTierLevel.Level1Direct));

        await _service.UpdatePartnerAsync(partner.Id, new UpdateAffiliateRequest(Status: AffiliateStatus.Inactive));

        var req = new TrackConversionRequest("ORD-INACT-ATTRIB", 200m, ReferralCode: partner.ReferralCodes[0]);
        var result = await _service.TrackConversionAsync(req);

        Assert.False(result.IsAttributed);
        Assert.Contains("Inactive", result.Reason);
    }

    #endregion

    #region 7. Settlement Batch Generation & Thresholds Tests

    [Fact]
    public async Task GeneratePayoutBatch_BelowThreshold_SkipsAffiliate()
    {
        // Set high threshold of $10,000 so no current approved partner qualifies
        var batch = await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
            MinPayoutThreshold: 10000.0m));

        Assert.Equal(0, batch.AffiliateCount);
        Assert.Equal(0m, batch.GrossTotal);
        Assert.Empty(batch.LineItems);
    }

    [Fact]
    public async Task GeneratePayoutBatch_MeetsThreshold_AggregatesAndMarksCommissionsPaidOut()
    {
        // Apex has a preseeded approved transaction of $120.00
        var batch = await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
            MinPayoutThreshold: 50.0m,
            SpecificAffiliateIds: new List<Guid> { Guid.Parse("33333333-3333-3333-3333-333333333333") }));

        Assert.True(batch.AffiliateCount >= 1);
        Assert.True(batch.GrossTotal >= 120.0m);
        Assert.NotEmpty(batch.BatchNumber);

        // Check that commission is now PaidOut
        var comms = await _service.GetCommissionsAsync(
            affiliateId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            status: CommissionState.PaidOut);

        Assert.Contains(comms, c => c.SettlementBatchId == batch.Id);
    }

    [Fact]
    public async Task GeneratePayoutBatch_W8BenForeignAffiliate_Withholds10PercentTax()
    {
        // Nordic Growth is W-8BEN (10% withholding) with $40 preseeded approved balance
        // Let's add an order and mature it so it crosses $50 threshold
        var res = await _service.TrackConversionAsync(new TrackConversionRequest(
            OrderId: "ORD-W8-TAX",
            OrderGmv: 1000m,
            ReferralCode: "NORDIC5"));

        await _service.ProcessMaturitySweepAsync(DateTime.UtcNow.AddDays(15));

        var batch = await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
            MinPayoutThreshold: 40.0m,
            SpecificAffiliateIds: new List<Guid> { Guid.Parse("22222222-2222-2222-2222-222222222222") }));

        var item = batch.LineItems.Single(l => l.AffiliateId == Guid.Parse("22222222-2222-2222-2222-222222222222"));
        Assert.True(item.GrossAmount > 0);
        Assert.Equal(Math.Round(item.GrossAmount * 0.10m, 2), item.TaxWithheld);
        Assert.Equal(item.GrossAmount - item.TaxWithheld, item.NetAmount);
    }

    [Fact]
    public async Task GeneratePayoutBatch_W9VerifiedAffiliate_ZeroTaxWithholding()
    {
        // Apex is W-9 verified (0% backup withholding)
        var batch = await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
            MinPayoutThreshold: 50.0m,
            SpecificAffiliateIds: new List<Guid> { Guid.Parse("33333333-3333-3333-3333-333333333333") }));

        var item = batch.LineItems.Single(l => l.AffiliateId == Guid.Parse("33333333-3333-3333-3333-333333333333"));
        Assert.Equal(0.0m, item.TaxWithheld);
        Assert.Equal(item.GrossAmount, item.NetAmount);
    }

    [Fact]
    public async Task GeneratePayoutBatch_SpecificAffiliateIdsFilter_ProcessesOnlySelectedPartners()
    {
        var targetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var batch = await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
            MinPayoutThreshold: 20.0m,
            SpecificAffiliateIds: new List<Guid> { targetId }));

        Assert.All(batch.LineItems, item => Assert.Equal(targetId, item.AffiliateId));
    }

    [Fact]
    public async Task GeneratePayoutBatch_PayoutMethodFilter_ProcessesOnlySelectedMethod()
    {
        var batch = await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
            MinPayoutThreshold: 10.0m,
            PayoutMethodFilter: PayoutMethod.PayPal));

        Assert.All(batch.LineItems, item => Assert.Equal(PayoutMethod.PayPal, item.Method));
    }

    #endregion

    #region 8. Manifest Formatting & Validation Tests

    [Fact]
    public void GenerateSepaXml_ProducesValidPain00100103XmlWithRequiredNodes()
    {
        var batch = new SettlementBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = "SETTLE-TEST-SEPA",
            CreatedAtUtc = DateTime.UtcNow,
            Currency = "EUR"
        };

        var items = new List<SettlementLineItem>
        {
            new()
            {
                LineItemId = Guid.NewGuid(),
                AffiliateId = Guid.NewGuid(),
                AffiliateName = "Euro Distributor BV",
                RecipientEmail = "finance@eurodist.nl",
                Method = PayoutMethod.Sepa,
                GrossAmount = 200m,
                TaxWithheld = 20m,
                NetAmount = 180m,
                DestinationReference = "NL91ABNA0417164300",
                SwiftBic = "ABNANL2A"
            }
        };

        var xmlString = AffiliateCommissionService.GenerateSepaXmlManifest(batch, items);

        Assert.NotNull(xmlString);
        Assert.Contains("pain.001.001.03", xmlString);
        Assert.Contains("NL91ABNA0417164300", xmlString);
        Assert.Contains("180.00", xmlString);

        // Parse with XDocument to guarantee well-formed XML syntax
        var doc = XDocument.Parse(xmlString);
        Assert.NotNull(doc.Root);
        Assert.Equal("Document", doc.Root.Name.LocalName);
    }

    [Fact]
    public void GenerateSepaXml_IncludesCorrectControlSumAndTransactionCount()
    {
        var batch = new SettlementBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = "SETTLE-TEST-SUM",
            CreatedAtUtc = DateTime.UtcNow,
            Currency = "EUR"
        };

        var items = new List<SettlementLineItem>
        {
            new() { LineItemId = Guid.NewGuid(), AffiliateId = Guid.NewGuid(), AffiliateName = "Partner 1", NetAmount = 150.25m, Method = PayoutMethod.Sepa, DestinationReference = "DE89370400440532013000" },
            new() { LineItemId = Guid.NewGuid(), AffiliateId = Guid.NewGuid(), AffiliateName = "Partner 2", NetAmount = 249.75m, Method = PayoutMethod.Sepa, DestinationReference = "FR1420041010050500013M02606" }
        };

        var xmlString = AffiliateCommissionService.GenerateSepaXmlManifest(batch, items);
        var doc = XDocument.Parse(xmlString);
        XNamespace ns = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.03";

        var nbOfTxs = doc.Descendants(ns + "NbOfTxs").First().Value;
        var ctrlSum = doc.Descendants(ns + "CtrlSum").First().Value;

        Assert.Equal("2", nbOfTxs);
        Assert.Equal("400.00", ctrlSum);
    }

    [Fact]
    public void GeneratePayPalCsv_ProducesStandardHeaderAndEscapedRows()
    {
        var batch = new SettlementBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = "SETTLE-PP-001",
            Currency = "USD"
        };

        var items = new List<SettlementLineItem>
        {
            new()
            {
                AffiliateId = Guid.NewGuid(),
                AffiliateName = "US Tech Blog",
                RecipientEmail = "paypal@ustechblog.com",
                Method = PayoutMethod.PayPal,
                NetAmount = 125.50m,
                DestinationReference = "paypal@ustechblog.com"
            }
        };

        var csv = AffiliateCommissionService.GeneratePayPalCsvManifest(batch, items);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("RecipientEmail,Amount,Currency,Note,CustomId", lines[0]);
        Assert.Contains("\"paypal@ustechblog.com\",125.50,USD,", lines[1]);
    }

    [Fact]
    public void GenerateWireCryptoLedger_ProducesCompleteCsvLedger()
    {
        var batch = new SettlementBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = "SETTLE-WIRE-01",
            Currency = "USD"
        };

        var items = new List<SettlementLineItem>
        {
            new()
            {
                LineItemId = Guid.NewGuid(),
                AffiliateId = Guid.NewGuid(),
                AffiliateName = "Crypto Agency",
                Method = PayoutMethod.Crypto,
                GrossAmount = 500m,
                TaxWithheld = 50m,
                NetAmount = 450m,
                DestinationReference = "Ethereum:0xAbCd...1234",
                SwiftBic = null
            }
        };

        var ledger = AffiliateCommissionService.GenerateWireCryptoLedgerManifest(batch, items);

        Assert.Contains("LineItemId,AffiliateId,AffiliateName", ledger);
        Assert.Contains("450.00", ledger);
        Assert.Contains("Ethereum:0xAbCd...1234", ledger);
    }

    [Fact]
    public async Task DownloadManifest_SepaAndPayPal_ReturnsExpectedFormat()
    {
        var batches = await _service.GetSettlementBatchesAsync();
        var histBatch = batches.First();

        var sepaManifest = await _service.GetBatchManifestAsync(histBatch.Id, "sepa");
        var ppManifest = await _service.GetBatchManifestAsync(histBatch.Id, "paypal");

        Assert.NotNull(sepaManifest);
        Assert.Contains("pain.001.001.03", sepaManifest);

        Assert.NotNull(ppManifest);
        Assert.Contains("RecipientEmail,Amount,Currency", ppManifest);
    }

    #endregion

    #region 9. Performance Metrics & Top Affiliates Tests

    [Fact]
    public async Task GetMetrics_AggregatesReferredGmvAndAccruedCommissionsCorrectly()
    {
        var metrics = await _service.GetMetricsAsync();

        Assert.NotNull(metrics);
        Assert.True(metrics.TotalReferredGmv > 0);
        Assert.True(metrics.TotalCommissionsAccrued > 0);
        Assert.True(metrics.ActiveAffiliatesCount >= 5);
        Assert.True(metrics.TotalConversionsCount > 0);
    }

    [Fact]
    public async Task GetMetrics_IdentifiesTopAffiliatesRankedByGmv()
    {
        var metrics = await _service.GetMetricsAsync();

        Assert.NotEmpty(metrics.TopAffiliates);
        // Verified ordered descending by referred GMV
        for (int i = 0; i < metrics.TopAffiliates.Count - 1; i++)
        {
            Assert.True(metrics.TopAffiliates[i].TotalGmv >= metrics.TopAffiliates[i + 1].TotalGmv);
        }
    }

    [Fact]
    public async Task GetMetrics_CalculatesAverageOrderValueAndStatusBreakdown()
    {
        var metrics = await _service.GetMetricsAsync();

        Assert.True(metrics.AverageOrderValue > 0);
        Assert.True(metrics.CommissionsByStatus.ContainsKey("PaidOut"));
        Assert.True(metrics.CommissionsByStatus.ContainsKey("Approved"));
        Assert.True(metrics.CommissionsByTier.ContainsKey(1));
    }

    #endregion

    #region 10. High Concurrency & Thread-Safety Tests

    [Fact]
    public async Task ConcurrentConversions_50SimultaneousThreads_AttributesAllCommissionsAccurately()
    {
        const int concurrentRequests = 50;
        var tasks = new Task<ConversionResult>[concurrentRequests];

        for (int i = 0; i < concurrentRequests; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                var req = new TrackConversionRequest(
                    OrderId: $"ORD-CONCURRENT-{index}",
                    OrderGmv: 100.0m + index,
                    ReferralCode: "APEX15");

                return await _service.TrackConversionAsync(req);
            });
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrentRequests, results.Length);
        Assert.All(results, r => Assert.True(r.IsAttributed));

        // Each conversion should have created 3 tier records (L1, L2, L3)
        var totalCreated = results.Sum(r => r.CreatedCommissions.Count);
        Assert.Equal(concurrentRequests * 3, totalCreated);
    }

    [Fact]
    public async Task ConcurrentSettlementAndConversions_NoDeadlocksOrRaceConditions()
    {
        const int iterations = 30;
        var tasks = new List<Task>();

        for (int i = 0; i < iterations; i++)
        {
            int index = i;
            // Half conversions
            tasks.Add(Task.Run(async () =>
            {
                await _service.TrackConversionAsync(new TrackConversionRequest(
                    OrderId: $"ORD-STRESS-MIX-{index}",
                    OrderGmv: 250m,
                    ReferralCode: "CLOUDSTACK"));
            }));

            // Half sweeps and settlements
            tasks.Add(Task.Run(async () =>
            {
                await _service.ProcessMaturitySweepAsync(DateTime.UtcNow.AddDays(20));
                await _service.GeneratePayoutBatchAsync(new GenerateSettlementBatchRequest(
                    MinPayoutThreshold: 50.0m));
            }));
        }

        // Must complete without unhandled exception or deadlock within 10 seconds
        await Task.WhenAll(tasks);

        var metrics = await _service.GetMetricsAsync();
        Assert.True(metrics.TotalCommissionsAccrued > 0);
    }

    [Fact]
    public async Task ConcurrentClicks_50SimultaneousThreads_IncrementsClickCountSafely()
    {
        var partner = await _service.GetPartnerByIdAsync(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        int initialClicks = partner!.Metrics.TotalClicks;

        const int concurrentClicks = 50;
        var tasks = new Task<AttributionCookieRecord>[concurrentClicks];

        for (int i = 0; i < concurrentClicks; i++)
        {
            tasks[i] = Task.Run(async () => await _service.TrackClickAsync("APEX15"));
        }

        var cookies = await Task.WhenAll(tasks);

        Assert.Equal(concurrentClicks, cookies.Length);
        Assert.Equal(initialClicks + concurrentClicks, partner.Metrics.TotalClicks);
    }

    #endregion
}
