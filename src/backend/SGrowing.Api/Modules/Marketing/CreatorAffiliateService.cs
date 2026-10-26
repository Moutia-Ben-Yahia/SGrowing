using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public interface ICreatorAffiliateService
{
    Task<CreatorProfile> RegisterCreatorAsync(Guid tenantId, CreatorProfile creator);
    Task<AffiliateReferralCode> CreateReferralCodeAsync(Guid tenantId, Guid creatorId, string code, string discountType = "Percentage", decimal discountValue = 10.0m);
    Task<CommissionEscrowLedger?> AttributeOrderCommissionAsync(Guid tenantId, string referralCode, Guid orderId, string trackingNumber, decimal orderAmount);
    Task<CommissionEscrowLedger?> ClearEscrowOnDeliveryAsync(Guid tenantId, Guid orderId);
    Task<CommissionEscrowLedger?> VoidEscrowOnReturnOrCancelAsync(Guid tenantId, Guid orderId, string reason);
    Task<List<CreatorPayoutRecord>> GeneratePayoutBatchAsync(Guid tenantId);
    Task<CreatorPayoutRecord> ExecutePayoutAsync(Guid tenantId, Guid payoutRecordId, string transactionReference);
    Task<CreatorPerformanceSummary> GetCreatorSummaryAsync(Guid tenantId, Guid creatorId);
    Task<List<CreatorProfile>> ListCreatorsAsync(Guid tenantId);
    Task<List<CommissionEscrowLedger>> ListEscrowLedgerAsync(Guid tenantId, Guid? creatorId = null);
}

public class CreatorAffiliateService : ICreatorAffiliateService
{
    private readonly SGrowingDbContext _db;

    public CreatorAffiliateService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CreatorProfile> RegisterCreatorAsync(Guid tenantId, CreatorProfile creator)
    {
        creator.TenantId = tenantId;
        _db.CreatorProfiles.Add(creator);
        await _db.SaveChangesAsync();
        return creator;
    }

    public async Task<AffiliateReferralCode> CreateReferralCodeAsync(
        Guid tenantId,
        Guid creatorId,
        string code,
        string discountType = "Percentage",
        decimal discountValue = 10.0m)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = await _db.AffiliateReferralCodes
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == normalizedCode);

        if (existing != null)
            throw new InvalidOperationException($"Referral code '{normalizedCode}' already exists for this tenant.");

        var refCode = AffiliateReferralCode.Create(tenantId, creatorId, normalizedCode, discountType, discountValue);
        _db.AffiliateReferralCodes.Add(refCode);
        await _db.SaveChangesAsync();
        return refCode;
    }

    public async Task<CommissionEscrowLedger?> AttributeOrderCommissionAsync(
        Guid tenantId,
        string referralCode,
        Guid orderId,
        string trackingNumber,
        decimal orderAmount)
    {
        var normalizedCode = referralCode.Trim().ToUpperInvariant();
        var codeEntity = await _db.AffiliateReferralCodes
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == normalizedCode && r.IsActive);

        if (codeEntity == null) return null;

        var creator = await _db.CreatorProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == codeEntity.CreatorId && c.IsActive);

        if (creator == null) return null;

        // Calculate commission
        decimal commission = creator.CommissionType == CommissionType.Percentage
            ? Math.Round(orderAmount * (creator.CommissionRate / 100m), 2)
            : creator.CommissionRate;

        // Create Escrow Ledger entry in EscrowHold
        var ledger = CommissionEscrowLedger.Create(
            tenantId,
            creator.Id,
            orderId,
            normalizedCode,
            trackingNumber,
            orderAmount,
            commission);

        _db.CommissionEscrowLedgers.Add(ledger);

        // Update tracking on code & creator escrow balance
        codeEntity.UsageCount++;
        codeEntity.TotalReferredGmv += orderAmount;
        creator.PendingEscrowBalance += commission;

        await _db.SaveChangesAsync();
        return ledger;
    }

    public async Task<CommissionEscrowLedger?> ClearEscrowOnDeliveryAsync(Guid tenantId, Guid orderId)
    {
        var ledger = await _db.CommissionEscrowLedgers
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.OrderId == orderId && l.Status == EscrowStatus.EscrowHold);

        if (ledger == null) return null;

        var creator = await _db.CreatorProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == ledger.CreatorId);

        ledger.Status = EscrowStatus.ClearedForPayout;
        ledger.ClearedAtUtc = DateTime.UtcNow;

        if (creator != null)
        {
            creator.PendingEscrowBalance = Math.Max(0m, creator.PendingEscrowBalance - ledger.CommissionAmount);
            creator.TotalEarned += ledger.CommissionAmount;
        }

        await _db.SaveChangesAsync();
        return ledger;
    }

    public async Task<CommissionEscrowLedger?> VoidEscrowOnReturnOrCancelAsync(Guid tenantId, Guid orderId, string reason)
    {
        var ledger = await _db.CommissionEscrowLedgers
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.OrderId == orderId &&
                (l.Status == EscrowStatus.EscrowHold || l.Status == EscrowStatus.ClearedForPayout));

        if (ledger == null) return null;

        var creator = await _db.CreatorProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == ledger.CreatorId);

        var previousStatus = ledger.Status;
        ledger.Status = EscrowStatus.VoidedReturn;
        ledger.VoidReason = reason;
        ledger.VoidedAtUtc = DateTime.UtcNow;

        if (creator != null)
        {
            if (previousStatus == EscrowStatus.EscrowHold)
            {
                creator.PendingEscrowBalance = Math.Max(0m, creator.PendingEscrowBalance - ledger.CommissionAmount);
            }
            else if (previousStatus == EscrowStatus.ClearedForPayout)
            {
                creator.TotalEarned = Math.Max(0m, creator.TotalEarned - ledger.CommissionAmount);
            }
        }

        await _db.SaveChangesAsync();
        return ledger;
    }

    public async Task<List<CreatorPayoutRecord>> GeneratePayoutBatchAsync(Guid tenantId)
    {
        var clearedLedgers = await _db.CommissionEscrowLedgers
            .Where(l => l.TenantId == tenantId && l.Status == EscrowStatus.ClearedForPayout)
            .ToListAsync();

        var batchId = Guid.NewGuid();
        var payoutRecords = new List<CreatorPayoutRecord>();

        var groupedByCreator = clearedLedgers.GroupBy(l => l.CreatorId);

        foreach (var group in groupedByCreator)
        {
            var creator = await _db.CreatorProfiles
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == group.Key);

            if (creator == null) continue;

            decimal totalPayable = group.Sum(l => l.CommissionAmount);

            // Check if minimum payout threshold is met
            if (totalPayable >= creator.MinimumPayoutThreshold && totalPayable > 0m)
            {
                var payout = new CreatorPayoutRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CreatorId = creator.Id,
                    PayoutBatchId = batchId,
                    Amount = totalPayable,
                    Currency = "TND",
                    PaymentMethod = creator.PaymentMethod,
                    DestinationAccount = creator.PayoutDetails,
                    Status = CreatorPayoutStatus.Approved,
                    IncludedOrdersCount = group.Count(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.CreatorPayoutRecords.Add(payout);
                payoutRecords.Add(payout);
            }
        }

        if (payoutRecords.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return payoutRecords;
    }

    public async Task<CreatorPayoutRecord> ExecutePayoutAsync(Guid tenantId, Guid payoutRecordId, string transactionReference)
    {
        var payout = await _db.CreatorPayoutRecords
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == payoutRecordId);

        if (payout == null)
            throw new InvalidOperationException($"Payout record {payoutRecordId} not found.");

        if (payout.Status == CreatorPayoutStatus.Transferred)
            return payout;

        var creator = await _db.CreatorProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == payout.CreatorId);

        payout.Status = CreatorPayoutStatus.Transferred;
        payout.TransactionReference = transactionReference.Trim();
        payout.ExecutedAtUtc = DateTime.UtcNow;

        if (creator != null)
        {
            creator.TotalPaidOut += payout.Amount;
        }

        // Mark associated cleared escrow items as PaidOut
        var ledgers = await _db.CommissionEscrowLedgers
            .Where(l => l.TenantId == tenantId && l.CreatorId == payout.CreatorId && l.Status == EscrowStatus.ClearedForPayout)
            .ToListAsync();

        foreach (var ledger in ledgers)
        {
            ledger.Status = EscrowStatus.PaidOut;
            ledger.PaidOutAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return payout;
    }

    public async Task<CreatorPerformanceSummary> GetCreatorSummaryAsync(Guid tenantId, Guid creatorId)
    {
        var creator = await _db.CreatorProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == creatorId);

        if (creator == null)
            throw new InvalidOperationException($"Creator {creatorId} not found.");

        var ledgers = await _db.CommissionEscrowLedgers
            .Where(l => l.TenantId == tenantId && l.CreatorId == creatorId)
            .ToListAsync();

        int totalOrders = ledgers.Count;
        int deliveredOrders = ledgers.Count(l => l.Status == EscrowStatus.ClearedForPayout || l.Status == EscrowStatus.PaidOut);
        int returnedOrders = ledgers.Count(l => l.Status == EscrowStatus.VoidedReturn);

        decimal grossGmv = ledgers.Sum(l => l.OrderAmount);
        decimal netGmv = ledgers.Where(l => l.Status == EscrowStatus.ClearedForPayout || l.Status == EscrowStatus.PaidOut).Sum(l => l.OrderAmount);

        decimal deliveryRate = totalOrders > 0
            ? Math.Round((decimal)deliveredOrders / totalOrders * 100m, 1)
            : 0m;

        decimal totalEarned = ledgers.Where(l => l.Status == EscrowStatus.ClearedForPayout || l.Status == EscrowStatus.PaidOut).Sum(l => l.CommissionAmount);
        decimal pendingEscrow = ledgers.Where(l => l.Status == EscrowStatus.EscrowHold).Sum(l => l.CommissionAmount);
        decimal voided = ledgers.Where(l => l.Status == EscrowStatus.VoidedReturn).Sum(l => l.CommissionAmount);
        decimal paidOut = ledgers.Where(l => l.Status == EscrowStatus.PaidOut).Sum(l => l.CommissionAmount);
        decimal availableForPayout = totalEarned - paidOut;

        return new CreatorPerformanceSummary(
            creator.Id,
            creator.FullName,
            creator.Handle,
            grossGmv,
            netGmv,
            totalOrders,
            deliveredOrders,
            returnedOrders,
            deliveryRate,
            totalEarned,
            pendingEscrow,
            voided,
            paidOut,
            availableForPayout
        );
    }

    public async Task<List<CreatorProfile>> ListCreatorsAsync(Guid tenantId)
    {
        return await _db.CreatorProfiles
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.TotalEarned)
            .ToListAsync();
    }

    public async Task<List<CommissionEscrowLedger>> ListEscrowLedgerAsync(Guid tenantId, Guid? creatorId = null)
    {
        var query = _db.CommissionEscrowLedgers.Where(l => l.TenantId == tenantId);
        if (creatorId.HasValue)
        {
            query = query.Where(l => l.CreatorId == creatorId.Value);
        }
        return await query.OrderByDescending(l => l.CreatedAtUtc).ToListAsync();
    }
}
