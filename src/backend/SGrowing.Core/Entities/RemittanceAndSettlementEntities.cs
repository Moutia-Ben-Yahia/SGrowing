namespace SGrowing.Core.Entities;

public enum RemittanceStatus
{
    Draft = 1,
    Reconciled = 2,
    DiscrepancyFlagged = 3
}

public enum RemittanceDiscrepancyType
{
    ExactMatch = 1,
    ShortPayment = 2,
    Overpayment = 3,
    UnaccountedParcel = 4,
    FeeDispute = 5,
    PhantomReturn = 6
}

public class CourierRemittanceStatement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string StatementReference { get; set; } = string.Empty; // e.g., BVR-ARX-2026-10-W4
    public DateTime StatementDateUtc { get; set; } = DateTime.UtcNow;
    public int TotalParcelsReported { get; set; }
    public decimal TotalCashCollected { get; set; }
    public decimal TotalCourierFeesDeducted { get; set; }
    public decimal NetRemittedAmount { get; set; } // Cash Collected - Fees
    public RemittanceStatus Status { get; set; } = RemittanceStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReconciledAtUtc { get; set; }
    public string? ReconciledBy { get; set; }
}

public class RemittanceItemMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid StatementId { get; set; }
    public Guid? OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string ReportedStatus { get; set; } = "Delivered"; // Delivered, Returned
    public decimal ReportedCashCollected { get; set; }
    public decimal SystemExpectedAmount { get; set; }
    public decimal DifferenceAmount { get; set; } // Reported - Expected
    public decimal CourierFeeDeducted { get; set; }
    public RemittanceDiscrepancyType DiscrepancyType { get; set; } = RemittanceDiscrepancyType.ExactMatch;
    public string? DisputeTicketNumber { get; set; }
    public decimal HoldbackAmount { get; set; }
    public string? ResolutionNotes { get; set; }
    public bool IsResolved { get; set; } = false;
}

public class MerchantNetProfitabilitySummary
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public decimal GrossRevenueCollected { get; set; }
    public decimal TotalCogs { get; set; }
    public decimal TotalAdSpend { get; set; }
    public decimal TotalCourierFees { get; set; }
    public decimal NetProfit => GrossRevenueCollected - TotalCogs - TotalAdSpend - TotalCourierFees;
    public double NetProfitMarginPercent => GrossRevenueCollected > 0 ? (double)Math.Round((NetProfit / GrossRevenueCollected) * 100, 2) : 0;
}
