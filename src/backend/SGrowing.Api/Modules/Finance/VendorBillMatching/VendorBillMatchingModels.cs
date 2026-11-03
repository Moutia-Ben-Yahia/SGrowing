using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.VendorBillMatching;

public enum MatchStatus
{
    PendingOcr = 0,
    Matched3Way = 1,
    PriceDiscrepancy = 2,
    QuantityDiscrepancy = 3,
    ApprovedEarlyDiscount = 4
}

public record VendorBillRecord(
    string BillId,
    string VendorName,
    string PoNumber,
    string GrnNumber,
    decimal InvoiceAmount,
    decimal PoAmount,
    decimal EarlyDiscountAmount,
    double DiscountRatePct,       // e.g. 2.0% for 2/10 Net 30
    int DiscountWindowDays,       // e.g. 10 days
    MatchStatus Status,
    DateTime ReceivedAt,
    DateTime DiscountExpiryDate,
    string? MatchAuditReceipt
);

public record VendorBillOverview(
    int PendingBillsCount,
    decimal TotalBillVolume,
    decimal TotalEarlyDiscountsCaptured,
    int PerfectMatchesCount,
    List<VendorBillRecord> Bills
);

public record ApproveEarlyDiscountCommand(
    string BillId,
    string ApproverId
);

public record BillApprovalResult(
    string BillId,
    decimal NetPaymentAmount,
    decimal SavedDiscountAmount,
    string SettlementReceipt
);
