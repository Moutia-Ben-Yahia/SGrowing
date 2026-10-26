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
        Assert.Equal(66.7m, finalSummary.DeliverySuccessRate); // 2 delivered out of 3 total = 66.7%
    }
}
