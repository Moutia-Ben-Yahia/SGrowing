using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.Logistics.Reconciliation;

/// <summary>
/// Regional couriers operating across Saudi Arabia and the MENA region.
/// </summary>
public enum CourierCode
{
    SMSA,
    Aramex,
    SPL,
    JTExpress,
    Other
}

/// <summary>
/// Status of Cash-on-Delivery parcel remittance matching.
/// </summary>
public enum RemittanceMatchStatus
{
    Matched,
    Underpaid,
    Overpaid,
    LostInTransit,
    RtoFeeDisputed,
    UnmatchedWaybill
}

/// <summary>
/// An individual item in a courier remittance batch compared against expected order COD amounts.
/// </summary>
public record CourierRemittanceItem(
    string TrackingNumber,
    string OrderNumber,
    decimal ExpectedCodAmountSar,
    decimal RemittedAmountSar,
    decimal CourierFeeSar,
    decimal DiscrepancyAmountSar,
    RemittanceMatchStatus Status,
    string? DiscrepancyReason
);

/// <summary>
/// An ingested courier remittance settlement batch.
/// </summary>
public record CourierRemittanceBatch(
    Guid BatchId,
    Guid TenantId,
    string BatchReference,
    CourierCode Courier,
    DateTime SettlementDateUtc,
    int TotalParcels,
    decimal TotalExpectedCodSar,
    decimal TotalRemittedCodSar,
    decimal TotalCourierFeesSar,
    decimal NetDiscrepancySar,
    List<CourierRemittanceItem> Items,
    DateTime IngestedAtUtc
);

/// <summary>
/// Request to ingest and reconcile a courier remittance batch bordereau.
/// </summary>
public record IngestCourierRemittanceRequest(
    Guid TenantId,
    string BatchReference,
    CourierCode Courier,
    List<RemittanceItemInput> RemittanceItems
);

/// <summary>
/// Input line item from a courier remittance file.
/// </summary>
public record RemittanceItemInput(
    string TrackingNumber,
    string OrderNumber,
    decimal RemittedAmountSar,
    decimal CourierFeeSar,
    bool IsDelivered,
    bool IsReturned = false
);

/// <summary>
/// Official dispute filed with a regional courier for underpayment, transit loss, or unjustified RTO fees.
/// </summary>
public record CourierDisputeRecord(
    Guid DisputeId,
    Guid TenantId,
    Guid BatchId,
    string TrackingNumber,
    CourierCode Courier,
    decimal DisputedAmountSar,
    string Reason,
    string Status,
    DateTime FiledAtUtc
);

/// <summary>
/// Executive reconciliation summary report across all courier remittance batches.
/// </summary>
public record ReconciliationSummaryReport(
    int TotalBatchesProcessed,
    int TotalParcelsReconciled,
    decimal ReconciledRatePercent,
    decimal TotalNetDiscrepancySar,
    int OpenDisputesCount,
    Dictionary<string, int> DiscrepancyStatusBreakdown,
    List<CourierPerformanceKpi> CourierPerformance
);

/// <summary>
/// Courier partner performance KPI metrics.
/// </summary>
public record CourierPerformanceKpi(
    string CourierName,
    int DeliveredParcels,
    decimal TotalCashRemittedSar,
    decimal DiscrepancyRatePercent,
    double AvgSettlementDays
);

/// <summary>
/// Request payload to file a formal courier discrepancy dispute.
/// </summary>
public record FileDisputeRequest(
    Guid TenantId,
    Guid BatchId,
    string TrackingNumber,
    string Reason
);
