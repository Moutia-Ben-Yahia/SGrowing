using System;
using System.Linq;
using System.Threading.Tasks;
using SGrowing.Api.Modules.Marketing.Loyalty;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CustomerLoyaltyRewardsTests
{
    [Fact]
    public async Task GetAccountSummary_SeededSultanAccount_ReturnsVipDiamondWithExpectedBalance()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();

        // Act
        var summary = await service.GetAccountSummary(CustomerLoyaltyRewardsService.FallbackTenantId, CustomerLoyaltyRewardsService.SultanContactId);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal("Sultan Al-Dosari", summary.ContactName);
        Assert.Equal("sultan.dosari@growthksa.com", summary.ContactEmail);
        Assert.Equal(4850, summary.CurrentPointsBalance);
        Assert.Equal(5350, summary.LifetimePointsEarned);
        Assert.Equal(LoyaltyMembershipTier.VipDiamond, summary.CurrentTier);
        Assert.Equal(0, summary.NextTierPointsRemaining);
        Assert.Equal("SULTAN-VIP", summary.ReferralCode);
        Assert.NotEmpty(summary.ActiveTierPerks);
        Assert.Equal(4, summary.RecentTransactions.Count);

        // Verify Sultan's latest transaction is the redemption
        var latestTx = summary.RecentTransactions[0];
        Assert.Equal(LoyaltyTransactionType.BurnRedemption, latestTx.Type);
        Assert.Equal(-500, latestTx.Points);
        Assert.Equal(4850, latestTx.BalanceAfter);
    }

    [Fact]
    public async Task EarnPoints_NewAccount_EnforcesDoubleEntryInvariantAndTracksBalance()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Act: Earn first batch
        var earn1 = new EarnPointsRequest(
            TenantId: tenantId,
            ContactId: contactId,
            Type: LoyaltyTransactionType.EarnPurchase,
            Points: 300,
            ReferenceId: "ORD-001",
            Description: "First online checkout"
        );
        var summary1 = await service.EarnPoints(earn1);

        // Assert batch 1
        Assert.Equal(300, summary1.CurrentPointsBalance);
        Assert.Equal(300, summary1.LifetimePointsEarned);
        Assert.Equal(LoyaltyMembershipTier.Bronze, summary1.CurrentTier);
        Assert.Equal(200, summary1.NextTierPointsRemaining); // 500 - 300
        Assert.Single(summary1.RecentTransactions);

        var tx1 = summary1.RecentTransactions[0];
        Assert.Equal(300, tx1.Points);
        Assert.Equal(300, tx1.BalanceAfter);
        Assert.Equal(0 + tx1.Points, tx1.BalanceAfter); // Invariant: previous (0) + Points (300) = 300

        // Act: Earn second batch
        var earn2 = new EarnPointsRequest(
            TenantId: tenantId,
            ContactId: contactId,
            Type: LoyaltyTransactionType.EarnReview,
            Points: 150,
            ReferenceId: "REV-505",
            Description: "Verified product review"
        );
        var summary2 = await service.EarnPoints(earn2);

        // Assert batch 2
        Assert.Equal(450, summary2.CurrentPointsBalance);
        Assert.Equal(450, summary2.LifetimePointsEarned);
        Assert.Equal(LoyaltyMembershipTier.Bronze, summary2.CurrentTier);
        Assert.Equal(50, summary2.NextTierPointsRemaining);
        Assert.Equal(2, summary2.RecentTransactions.Count);

        var tx2 = summary2.RecentTransactions[0];
        Assert.Equal(150, tx2.Points);
        Assert.Equal(450, tx2.BalanceAfter);
        Assert.Equal(summary1.CurrentPointsBalance + tx2.Points, tx2.BalanceAfter); // Invariant
    }

    [Fact]
    public async Task RedeemPoints_WithSufficientBalance_EnforcesDoubleEntryInvariant()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Earn 1000 points first
        await service.EarnPoints(new EarnPointsRequest(tenantId, contactId, LoyaltyTransactionType.EarnPurchase, 1000, "ORD-INIT", "Order initial"));

        // Act: Redeem 400 points
        var redeemReq = new RedeemPointsRequest(tenantId, contactId, 400, "ORD-RED-99");
        var summary = await service.RedeemPoints(redeemReq);

        // Assert
        Assert.Equal(600, summary.CurrentPointsBalance);
        Assert.Equal(1000, summary.LifetimePointsEarned); // Lifetime earn not reduced!
        Assert.Equal(2, summary.RecentTransactions.Count);

        var burnTx = summary.RecentTransactions[0];
        Assert.Equal(LoyaltyTransactionType.BurnRedemption, burnTx.Type);
        Assert.Equal(-400, burnTx.Points);
        Assert.Equal(600, burnTx.BalanceAfter);
        Assert.Equal(1000 + burnTx.Points, burnTx.BalanceAfter); // Invariant: 1000 + (-400) = 600
    }

    [Fact]
    public async Task RedeemPoints_InsufficientBalance_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        await service.EarnPoints(new EarnPointsRequest(tenantId, contactId, LoyaltyTransactionType.EarnPurchase, 250, "ORD-SMALL", "Small order"));

        // Act & Assert: Attempting to burn 500 points with only 250 available
        var redeemReq = new RedeemPointsRequest(tenantId, contactId, 500, "ORD-OVER");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RedeemPoints(redeemReq));

        Assert.Contains("Insufficient points balance", ex.Message);

        // Verify balance was not modified
        var summary = await service.GetAccountSummary(tenantId, contactId);
        Assert.Equal(250, summary.CurrentPointsBalance);
    }

    [Fact]
    public async Task AutomaticTierUpgrade_ProgressesThroughBronzeSilverGoldVipDiamond()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Step 1: 400 points -> Bronze
        var s1 = await service.EarnPoints(new EarnPointsRequest(tenantId, contactId, LoyaltyTransactionType.EarnPurchase, 400, "ORD-1", "Step 1"));
        Assert.Equal(LoyaltyMembershipTier.Bronze, s1.CurrentTier);
        Assert.Equal(100, s1.NextTierPointsRemaining);

        // Step 2: +200 points (Total 600) -> Silver (500 - 1999)
        var s2 = await service.EarnPoints(new EarnPointsRequest(tenantId, contactId, LoyaltyTransactionType.EarnPurchase, 200, "ORD-2", "Step 2"));
        Assert.Equal(LoyaltyMembershipTier.Silver, s2.CurrentTier);
        Assert.Equal(1400, s2.NextTierPointsRemaining); // 2000 - 600

        // Step 3: +1600 points (Total 2200) -> Gold (2000 - 4999)
        var s3 = await service.EarnPoints(new EarnPointsRequest(tenantId, contactId, LoyaltyTransactionType.EarnPurchase, 1600, "ORD-3", "Step 3"));
        Assert.Equal(LoyaltyMembershipTier.Gold, s3.CurrentTier);
        Assert.Equal(2800, s3.NextTierPointsRemaining); // 5000 - 2200

        // Step 4: +3000 points (Total 5200) -> VipDiamond (5000+)
        var s4 = await service.EarnPoints(new EarnPointsRequest(tenantId, contactId, LoyaltyTransactionType.EarnPurchase, 3000, "ORD-4", "Step 4"));
        Assert.Equal(LoyaltyMembershipTier.VipDiamond, s4.CurrentTier);
        Assert.Equal(0, s4.NextTierPointsRemaining); // Top tier reached

        // Step 5: Redeem 3000 points (Balance goes to 2200, Lifetime remains 5200) -> Stays VipDiamond!
        var s5 = await service.RedeemPoints(new RedeemPointsRequest(tenantId, contactId, 3000, "VOUCHER-BIG"));
        Assert.Equal(2200, s5.CurrentPointsBalance);
        Assert.Equal(5200, s5.LifetimePointsEarned);
        Assert.Equal(LoyaltyMembershipTier.VipDiamond, s5.CurrentTier);
        Assert.Equal(0, s5.NextTierPointsRemaining);
    }

    [Fact]
    public async Task AttributeReferral_AwardsPointsToReferrerAndGrantsDiscountToReferee()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = CustomerLoyaltyRewardsService.FallbackTenantId;
        var referrerId = CustomerLoyaltyRewardsService.SultanContactId;
        var initialSultan = await service.GetAccountSummary(tenantId, referrerId);
        var initialBalance = initialSultan.CurrentPointsBalance;

        var refereeId = Guid.NewGuid();
        var orderTotalSar = 500.00m;

        // Act: Attribute referral for Sultan's code "SULTAN-VIP"
        var req = new AttributeReferralRequest(
            TenantId: tenantId,
            ReferralCode: "SULTAN-VIP",
            RefereeContactId: refereeId,
            QualifyingOrderTotalSar: orderTotalSar
        );

        var attribution = await service.AttributeReferral(req);

        // Assert attribution record
        Assert.NotNull(attribution);
        Assert.Equal(tenantId, attribution.TenantId);
        Assert.Equal(referrerId, attribution.ReferrerContactId);
        Assert.Equal(refereeId, attribution.RefereeContactId);
        Assert.Equal("SULTAN-VIP", attribution.ReferralCode);
        Assert.Equal(15.0m, attribution.RefereeDiscountPercent);

        // Expected points: 250 base + 10% of 500 = 300 pts
        Assert.Equal(300, attribution.ReferrerPointsAwarded);

        // Assert Sultan's account received the points
        var updatedSultan = await service.GetAccountSummary(tenantId, referrerId);
        Assert.Equal(initialBalance + 300, updatedSultan.CurrentPointsBalance);
        Assert.Equal(initialSultan.LifetimePointsEarned + 300, updatedSultan.LifetimePointsEarned);

        var referralTx = updatedSultan.RecentTransactions[0];
        Assert.Equal(LoyaltyTransactionType.EarnReferral, referralTx.Type);
        Assert.Equal(300, referralTx.Points);
        Assert.Equal(initialBalance + 300, referralTx.BalanceAfter);
    }

    [Fact]
    public async Task AttributeReferral_SelfReferral_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = CustomerLoyaltyRewardsService.FallbackTenantId;
        var sultanId = CustomerLoyaltyRewardsService.SultanContactId;

        // Act & Assert: Sultan tries to use his own referral code
        var req = new AttributeReferralRequest(
            TenantId: tenantId,
            ReferralCode: "SULTAN-VIP",
            RefereeContactId: sultanId,
            QualifyingOrderTotalSar: 350.00m
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AttributeReferral(req));
        Assert.Contains("Self-referral is not permitted", ex.Message);
    }

    [Fact]
    public async Task AttributeReferral_DuplicateReferee_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantId = CustomerLoyaltyRewardsService.FallbackTenantId;
        var refereeId = Guid.NewGuid();

        var req1 = new AttributeReferralRequest(tenantId, "SULTAN-VIP", refereeId, 200m);
        await service.AttributeReferral(req1);

        // Act & Assert: Same referee trying to attribute again
        var req2 = new AttributeReferralRequest(tenantId, "SULTAN-VIP", refereeId, 300m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AttributeReferral(req2));
        Assert.Contains("already been attributed", ex.Message);
    }

    [Fact]
    public async Task TenantIsolation_AccountsAndCodesRemainStrictlyIsolated()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var contactA = Guid.NewGuid();

        // Create account in Tenant A
        var earnA = new EarnPointsRequest(tenantA, contactA, LoyaltyTransactionType.EarnPurchase, 800, "ORD-A1", "Tenant A order");
        var summaryA = await service.EarnPoints(earnA);

        Assert.Equal(tenantA, summaryA.TenantId);
        Assert.Equal(800, summaryA.CurrentPointsBalance);

        // Act & Assert: Tenant B querying contactA should throw KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAccountSummary(tenantB, contactA));

        // Act & Assert: Tenant B attempting to attribute Tenant A's referral code should throw KeyNotFoundException
        var refereeB = Guid.NewGuid();
        var attrReq = new AttributeReferralRequest(tenantB, summaryA.ReferralCode, refereeB, 250m);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AttributeReferral(attrReq));
    }

    [Fact]
    public void GetTierCatalog_ReturnsFourTiersWithExpectedMultipliers()
    {
        // Arrange
        var service = new CustomerLoyaltyRewardsService();

        // Act
        var catalog = service.GetTierCatalog();

        // Assert
        Assert.Equal(4, catalog.Count);

        var bronze = catalog.Single(t => t.Tier == LoyaltyMembershipTier.Bronze);
        Assert.Equal(0, bronze.MinPoints);
        Assert.Equal(499, bronze.MaxPoints);
        Assert.Equal(1.0m, bronze.PointsMultiplier);

        var silver = catalog.Single(t => t.Tier == LoyaltyMembershipTier.Silver);
        Assert.Equal(500, silver.MinPoints);
        Assert.Equal(1999, silver.MaxPoints);
        Assert.Equal(1.25m, silver.PointsMultiplier);

        var gold = catalog.Single(t => t.Tier == LoyaltyMembershipTier.Gold);
        Assert.Equal(2000, gold.MinPoints);
        Assert.Equal(4999, gold.MaxPoints);
        Assert.Equal(1.5m, gold.PointsMultiplier);

        var diamond = catalog.Single(t => t.Tier == LoyaltyMembershipTier.VipDiamond);
        Assert.Equal(5000, diamond.MinPoints);
        Assert.Null(diamond.MaxPoints);
        Assert.Equal(2.0m, diamond.PointsMultiplier);
    }
}
