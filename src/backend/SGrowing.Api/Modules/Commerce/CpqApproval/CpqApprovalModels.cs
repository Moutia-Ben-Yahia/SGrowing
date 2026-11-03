using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.CpqApproval;

public enum ApprovalTier
{
    RepAutonomous = 0,    // <= 10.0% discount
    SalesManager = 1,     // 10.1% - 20.0%
    SalesDirector = 2,    // 20.1% - 35.0%
    CroBoardGate = 3      // > 35.0% or custom payment terms
}

public enum QuoteApprovalStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Escalated = 4
}

public record QuoteItem(
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public record QuoteApprovalRequest(
    string QuoteId,
    string AccountName,
    decimal TotalListPrice,
    decimal DiscountPercentage,
    string PaymentTerms, // e.g. "Net 30", "Net 60", "Net 90"
    string RequesterId,
    string RequesterName,
    List<QuoteItem> Items,
    string Justification
);

public record QuoteApprovalRecord(
    string QuoteId,
    string AccountName,
    decimal TotalListPrice,
    decimal DiscountPercentage,
    decimal NetPrice,
    string PaymentTerms,
    ApprovalTier RequiredTier,
    QuoteApprovalStatus Status,
    string RequesterName,
    DateTime RequestedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? AuditToken,
    int SlaMinutesRemaining,
    bool IsSlaBreached,
    string? Justification
);

public record ApproveQuoteCommand(
    string QuoteId,
    string ApproverId,
    string ApproverName,
    string? Comments
);

public record RejectQuoteCommand(
    string QuoteId,
    string ApproverId,
    string ApproverName,
    string Reason
);

public record CpqApprovalOverview(
    int PendingApprovalsCount,
    decimal TotalPendingValue,
    int SlaBreachesCount,
    decimal AverageApprovedDiscount,
    List<QuoteApprovalRecord> Quotes
);
