using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class CreatorAffiliateTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CreatorAffiliateDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CreatorAffiliate_Escrow_Lifecycle_And_Payout_Batch_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();

        // 1. Register Influencer / Creator (Amira Beauty, 10% commission, 50 TND payout threshold)
        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Amira Ben Salem",
            handle: "@amirabeauty",
            email: "amira@creator.tn",
            phone: "+21698112233",
            platform: CreatorPlatform.Instagram,
            commissionType: CommissionType.Percentage,
            commissionRate: 10.0m,
            minPayoutThreshold: 50.0m,
            paymentMethod: "BankTransfer",
            payoutDetails: "TN5910006035183492817342");

        await service.RegisterCreatorAsync(tenantId, creator);

        // 2. Create Referral Discount Code
        var code = await service.CreateReferralCodeAsync(tenantId, creator.Id, "AMIRA10", "Percentage", 10.0m);
        Assert.Equal("AMIRA10", code.Code);

        // 3. Customer places Order 1 (180 TND COD) with code AMIRA10
        var order1Id = Guid.NewGuid();
        var ledger1 = await service.AttributeOrderCommissionAsync(tenantId, "AMIRA10", order1Id, "TRK-COD-01", 180.0m);

        Assert.NotNull(ledger1);
        Assert.Equal(EscrowStatus.EscrowHold, ledger1.Status);
        Assert.Equal(18.0m, ledger1.CommissionAmount); // 10% of 180

        var summaryAfterOrder1 = await service.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(18.0m, summaryAfterOrder1.PendingEscrowCommission);
        Assert.Equal(0m, summaryAfterOrder1.TotalEarnedCommission); // Not cleared yet because in COD hold!

        // 4. Order 1 Delivered by Courier -> Escrow Clears
        var cleared = await service.ClearEscrowOnDeliveryAsync(tenantId, order1Id);
        Assert.NotNull(cleared);
        Assert.Equal(EscrowStatus.ClearedForPayout, cleared.Status);

        var summaryAfterDelivery = await service.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(0m, summaryAfterDelivery.PendingEscrowCommission);
        Assert.Equal(18.0m, summaryAfterDelivery.TotalEarnedCommission);
        Assert.Equal(18.0m, summaryAfterDelivery.AvailableForPayoutBalance);

        // 5. Customer places Order 2 (120 TND COD) with code AMIRA10 -> Package is Refused & Returned
        var order2Id = Guid.NewGuid();
        var ledger2 = await service.AttributeOrderCommissionAsync(tenantId, "AMIRA10", order2Id, "TRK-COD-02", 120.0m);
        Assert.NotNull(ledger2);
        Assert.Equal(12.0m, ledger2.CommissionAmount);

        // Courier reports parcel refused at doorstep
        var voided = await service.VoidEscrowOnReturnOrCancelAsync(tenantId, order2Id, "Customer Refused at Doorstep");
        Assert.NotNull(voided);
        Assert.Equal(EscrowStatus.VoidedReturn, voided.Status);

        var summaryAfterReturn = await service.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(18.0m, summaryAfterReturn.TotalEarnedCommission);
        Assert.Equal(12.0m, summaryAfterReturn.VoidedCommission); // Voided, merchant saved 12 TND from unearned commission!
        Assert.Equal(0m, summaryAfterReturn.PendingEscrowCommission);

        // 6. Customer places Order 3 (400 TND COD) with code AMIRA10 -> Delivered
        var order3Id = Guid.NewGuid();
        await service.AttributeOrderCommissionAsync(tenantId, "AMIRA10", order3Id, "TRK-COD-03", 400.0m);
        await service.ClearEscrowOnDeliveryAsync(tenantId, order3Id);

        // Now Available balance = 18 + 40 = 58 TND (> 50 TND threshold)
        var payouts = await service.GeneratePayoutBatchAsync(tenantId);
        Assert.Single(payouts);
        var payout = payouts[0];
        Assert.Equal(58.0m, payout.Amount);
        Assert.Equal(2, payout.IncludedOrdersCount);

        // 7. Execute Wire Transfer Payout
        var executed = await service.ExecutePayoutAsync(tenantId, payout.Id, "WIRE-REF-992182");
        Assert.Equal(CreatorPayoutStatus.Transferred, executed.Status);

        var finalSummary = await service.GetCreatorSummaryAsync(tenantId, creator.Id);
        Assert.Equal(58.0m, finalSummary.PaidOutCommission);
        Assert.Equal(0m, finalSummary.AvailableForPayoutBalance);
    }

    [Fact]
    public async Task CreateReferralCode_ThrowsInvalidOperationException_WhenCodeAlreadyExistsForTenant()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();

        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Yassine Dev",
            handle: "@yassinedev",
            email: "yassine@dev.tn",
            phone: "+21620112233",
            platform: CreatorPlatform.YouTube,
            commissionType: CommissionType.Percentage,
            commissionRate: 15.0m,
            minPayoutThreshold: 100.0m,
            paymentMethod: "BankTransfer",
            payoutDetails: "TN5910006035183492817342");

        await service.RegisterCreatorAsync(tenantId, creator);
        await service.CreateReferralCodeAsync(tenantId, creator.Id, "YASSINE15");

        // Attempt duplicate code registration
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateReferralCodeAsync(tenantId, creator.Id, "yassine15"));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task AttributeOrderCommission_SupportsFixedCommissionPerOrder()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();

        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Sami Tech",
            handle: "@samitech",
            email: "sami@tech.tn",
            phone: "+21650112233",
            platform: CreatorPlatform.TikTok,
            commissionType: CommissionType.FixedPerDeliveredOrder,
            commissionRate: 25.0m, // 25 TND fixed per order
            minPayoutThreshold: 50.0m,
            paymentMethod: "Flouci",
            payoutDetails: "FLOUCI_9921");

        await service.RegisterCreatorAsync(tenantId, creator);
        await service.CreateReferralCodeAsync(tenantId, creator.Id, "SAMI25");

        var orderId = Guid.NewGuid();
        var ledger = await service.AttributeOrderCommissionAsync(tenantId, "SAMI25", orderId, "TRK-COD-FIXED", 500.0m);

        Assert.NotNull(ledger);
        Assert.Equal(25.0m, ledger.CommissionAmount); // Fixed 25 TND regardless of order amount
    }

    [Fact]
    public async Task AttributeOrderCommission_ReturnsNull_WhenCodeNotFoundOrInactive()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();

        var ledger = await service.AttributeOrderCommissionAsync(
            tenantId,
            "INVALID_NONEXISTENT_CODE",
            Guid.NewGuid(),
            "TRK-NONE",
            100.0m);

        Assert.Null(ledger);
    }

    [Fact]
    public async Task GeneratePayoutBatch_SkipsCreators_BelowMinimumPayoutThreshold()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();

        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Nadia Lifestyle",
            handle: "@nadialife",
            email: "nadia@life.tn",
            phone: "+21699112233",
            platform: CreatorPlatform.Instagram,
            commissionType: CommissionType.Percentage,
            commissionRate: 10.0m,
            minPayoutThreshold: 100.0m, // Requires 100 TND minimum
            paymentMethod: "BankTransfer",
            payoutDetails: "TN5910006035183492817342");

        await service.RegisterCreatorAsync(tenantId, creator);
        await service.CreateReferralCodeAsync(tenantId, creator.Id, "NADIA10");

        // Order with 30 TND commission (300 TND order * 10%)
        var orderId = Guid.NewGuid();
        await service.AttributeOrderCommissionAsync(tenantId, "NADIA10", orderId, "TRK-01", 300.0m);
        await service.ClearEscrowOnDeliveryAsync(tenantId, orderId);

        // Available balance is 30 TND, which is < 100 TND minimum
        var payouts = await service.GeneratePayoutBatchAsync(tenantId);
        Assert.Empty(payouts);
    }

    [Fact]
    public async Task ExecutePayout_IsIdempotent_WhenAlreadyTransferred()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();

        var creator = CreatorProfile.Create(
            tenantId,
            fullName: "Karim Deals",
            handle: "@karimdeals",
            email: "karim@deals.tn",
            phone: "+21622112233",
            platform: CreatorPlatform.Facebook,
            commissionType: CommissionType.Percentage,
            commissionRate: 10.0m,
            minPayoutThreshold: 20.0m,
            paymentMethod: "BankTransfer",
            payoutDetails: "TN5910006035183492817342");

        await service.RegisterCreatorAsync(tenantId, creator);
        await service.CreateReferralCodeAsync(tenantId, creator.Id, "KARIM10");

        var orderId = Guid.NewGuid();
        await service.AttributeOrderCommissionAsync(tenantId, "KARIM10", orderId, "TRK-01", 500.0m);
        await service.ClearEscrowOnDeliveryAsync(tenantId, orderId);

        var payouts = await service.GeneratePayoutBatchAsync(tenantId);
        Assert.Single(payouts);

        var payoutId = payouts[0].Id;
        var firstExec = await service.ExecutePayoutAsync(tenantId, payoutId, "REF-TX-001");
        Assert.Equal(CreatorPayoutStatus.Transferred, firstExec.Status);

        // Second execution should return existing payout without throwing
        var secondExec = await service.ExecutePayoutAsync(tenantId, payoutId, "REF-TX-001");
        Assert.Equal(CreatorPayoutStatus.Transferred, secondExec.Status);
    }

    [Fact]
    public async Task GetCreatorSummary_ThrowsInvalidOperationException_WhenCreatorDoesNotExist()
    {
        var db = CreateDbContext();
        var service = new CreatorAffiliateService(db);
        var tenantId = Guid.NewGuid();
        var unknownCreatorId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetCreatorSummaryAsync(tenantId, unknownCreatorId));
    }
}
