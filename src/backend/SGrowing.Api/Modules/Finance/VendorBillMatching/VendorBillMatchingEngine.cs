using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.VendorBillMatching;

public interface IVendorBillMatchingEngine
{
    VendorBillOverview GetOverview();
    BillApprovalResult ApproveEarlyDiscount(ApproveEarlyDiscountCommand command);
}

public class VendorBillMatchingEngine : IVendorBillMatchingEngine
{
    private readonly ConcurrentDictionary<string, VendorBillRecord> _bills = new();

    public VendorBillMatchingEngine()
    {
        SeedBills();
    }

    private void SeedBills()
    {
        var now = DateTime.UtcNow;

        var b1 = new VendorBillRecord(
            BillId: "BILL-2026-4401",
            VendorName: "Amazon Web Services Inc",
            PoNumber: "PO-INFRA-8801",
            GrnNumber: "GRN-CLOUD-9912",
            InvoiceAmount: 145000m,
            PoAmount: 145000m,
            EarlyDiscountAmount: 2900m, // 2% 2/10 Net 30
            DiscountRatePct: 2.0,
            DiscountWindowDays: 10,
            Status: MatchStatus.Matched3Way,
            ReceivedAt: now.AddDays(-2),
            DiscountExpiryDate: now.AddDays(8),
            MatchAuditReceipt: "3WAY-VERIFIED-PO8801-GRN9912"
        );

        var b2 = new VendorBillRecord(
            BillId: "BILL-2026-4402",
            VendorName: "Snowflake Computing Inc",
            PoNumber: "PO-DATA-8802",
            GrnNumber: "GRN-DATA-9915",
            InvoiceAmount: 82000m,
            PoAmount: 82000m,
            EarlyDiscountAmount: 1640m,
            DiscountRatePct: 2.0,
            DiscountWindowDays: 10,
            Status: MatchStatus.Matched3Way,
            ReceivedAt: now.AddDays(-4),
            DiscountExpiryDate: now.AddDays(6),
            MatchAuditReceipt: "3WAY-VERIFIED-PO8802-GRN9915"
        );

        var b3 = new VendorBillRecord(
            BillId: "BILL-2026-4403",
            VendorName: "Twilio Telecommunications",
            PoNumber: "PO-VOX-8803",
            GrnNumber: "GRN-VOX-9918",
            InvoiceAmount: 64500m,
            PoAmount: 61000m, // Discrepancy!
            EarlyDiscountAmount: 1220m,
            DiscountRatePct: 2.0,
            DiscountWindowDays: 10,
            Status: MatchStatus.PriceDiscrepancy,
            ReceivedAt: now.AddDays(-6),
            DiscountExpiryDate: now.AddDays(4),
            MatchAuditReceipt: null
        );

        var b4 = new VendorBillRecord(
            BillId: "BILL-2026-4400",
            VendorName: "Stripe Payments Platform",
            PoNumber: "PO-FIN-8800",
            GrnNumber: "GRN-FIN-9910",
            InvoiceAmount: 210000m,
            PoAmount: 210000m,
            EarlyDiscountAmount: 4200m,
            DiscountRatePct: 2.0,
            DiscountWindowDays: 10,
            Status: MatchStatus.ApprovedEarlyDiscount,
            ReceivedAt: now.AddDays(-12),
            DiscountExpiryDate: now.AddDays(-2),
            MatchAuditReceipt: "3WAY-VERIFIED-PO8800-SETTLED"
        );

        _bills[b1.BillId] = b1;
        _bills[b2.BillId] = b2;
        _bills[b3.BillId] = b3;
        _bills[b4.BillId] = b4;
    }

    public VendorBillOverview GetOverview()
    {
        var list = _bills.Values.OrderByDescending(b => b.ReceivedAt).ToList();
        var pending = list.Where(b => b.Status == MatchStatus.Matched3Way || b.Status == MatchStatus.PriceDiscrepancy).ToList();
        var captured = list.Where(b => b.Status == MatchStatus.ApprovedEarlyDiscount).Sum(b => b.EarlyDiscountAmount);
        var totalVolume = list.Sum(b => b.InvoiceAmount);
        var perfect = list.Count(b => b.Status == MatchStatus.Matched3Way || b.Status == MatchStatus.ApprovedEarlyDiscount);

        return new VendorBillOverview(
            PendingBillsCount: pending.Count,
            TotalBillVolume: totalVolume,
            TotalEarlyDiscountsCaptured: captured,
            PerfectMatchesCount: perfect,
            Bills: list
        );
    }

    public BillApprovalResult ApproveEarlyDiscount(ApproveEarlyDiscountCommand command)
    {
        if (!_bills.TryGetValue(command.BillId, out var existing))
        {
            throw new KeyNotFoundException($"Vendor bill {command.BillId} not found.");
        }

        if (existing.Status == MatchStatus.PriceDiscrepancy)
        {
            throw new InvalidOperationException($"Cannot approve bill {command.BillId} with unresolved price discrepancy against PO.");
        }

        var net = existing.InvoiceAmount - existing.EarlyDiscountAmount;
        var updated = existing with
        {
            Status = MatchStatus.ApprovedEarlyDiscount,
            MatchAuditReceipt = $"DISCOUNT-CAPTURED-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
        };

        _bills[command.BillId] = updated;

        var receipt = $"DISCOUNT-ESCROW-PAID: Settled net {net:C0} to {existing.VendorName} capturing {existing.EarlyDiscountAmount:C0} early discount approved by {command.ApproverId}.";

        return new BillApprovalResult(
            BillId: command.BillId,
            NetPaymentAmount: net,
            SavedDiscountAmount: existing.EarlyDiscountAmount,
            SettlementReceipt: receipt
        );
    }
}
