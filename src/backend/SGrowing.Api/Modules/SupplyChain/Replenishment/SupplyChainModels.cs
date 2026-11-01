using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.SupplyChain.Replenishment;

#region Enums

/// <summary>
/// Operational priority for purchase order replenishment proposals.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PoPriority
{
    Normal = 0,
    Urgent = 1,
    Speculative = 2
}

/// <summary>
/// Lifecycle status for purchase order proposals.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PoStatus
{
    Proposed = 0,
    Dispatched = 1,
    Acknowledged = 2,
    Fulfilled = 3
}

/// <summary>
/// Operational staging status for JIT cross-docked inventory allocations.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrossDockStatus
{
    PendingTransfer = 0,
    Staged = 1,
    Loaded = 2
}

/// <summary>
/// Service Level Agreement confidence factor (Z-score) for safety stock calculation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ServiceLevelConfidence
{
    /// <summary>
    /// 95% SLA (Z = 1.644853).
    /// </summary>
    Sla95 = 95,

    /// <summary>
    /// 99% SLA (Z = 2.326348).
    /// </summary>
    Sla99 = 99
}

#endregion

#region Domain Records

/// <summary>
/// Inventory velocity, demand characteristics, lead time, and safety parameters for a SKU in a warehouse.
/// </summary>
public record SkuVelocityRecord(
    string Sku,
    Guid WarehouseId,
    decimal AverageDailyDemand,
    decimal DemandStdDev,
    decimal LeadTimeDays,
    decimal LeadTimeStdDev,
    decimal CurrentStock,
    decimal OnOrderStock,
    decimal SafetyStock,
    decimal ReorderPoint,
    decimal UnitHoldingCost,
    decimal OrderSetupCost,
    decimal EconomicOrderQuantity,
    decimal UnitCost = 0m,
    Guid? SupplierId = null,
    string? Description = null,
    DateTime? LastEvaluatedAtUtc = null);

/// <summary>
/// Output of the Holt-Winters triple exponential smoothing demand forecasting engine.
/// </summary>
public record ForecastResult(
    string Sku,
    int ForecastHorizonDays,
    IReadOnlyList<decimal> ProjectedDemandSeries,
    decimal Trend,
    IReadOnlyList<decimal> SeasonalityFactors,
    DateTime ComputedAtUtc,
    decimal CurrentLevel = 0m,
    decimal Alpha = 0.2m,
    decimal Beta = 0.1m,
    decimal Gamma = 0.3m,
    int Period = 7,
    decimal MeanSquaredError = 0m);

/// <summary>
/// Purchase order replenishment proposal triggered when inventory position drops to or below the ROP.
/// </summary>
public record PurchaseOrderProposal(
    Guid ProposalId,
    Guid SupplierId,
    string Sku,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalEstimatedCost,
    PoPriority Priority,
    string Reason,
    PoStatus Status,
    DateTime CreatedAtUtc,
    Guid WarehouseId = default,
    DateTime? DispatchedAtUtc = null,
    DateTime? AcknowledgedAtUtc = null,
    DateTime? EstimatedDeliveryDate = null,
    string? TransmissionPayload = null);

/// <summary>
/// JIT cross-docking allocation linking an incoming shipment directly to a waiting backorder.
/// </summary>
public record CrossDockAllocationRecord(
    Guid AllocationId,
    Guid InboundPoId,
    Guid OutboundOrderId,
    string Sku,
    decimal AllocatedQuantity,
    string DockBay,
    string TargetCarrier,
    DateTime AllocatedAtUtc,
    CrossDockStatus Status,
    string? WaybillNumber = null,
    string? CustomerName = null);

/// <summary>
/// Incoming shipment receipt at warehouse dock doors.
/// </summary>
public record InboundReceipt(
    Guid InboundPoId,
    string Sku,
    decimal ReceivedQuantity,
    string InboundDockBay,
    DateTime ReceivedAtUtc,
    string? SupplierName = null,
    Guid? SupplierId = null);

/// <summary>
/// Pending or backordered customer order awaiting inventory fulfillment.
/// </summary>
public record CustomerBackorderOrder(
    Guid OrderId,
    string CustomerName,
    string Sku,
    decimal BackorderedQuantity,
    string TargetCarrier,
    int PriorityLevel = 1,
    DateTime OrderDateUtc = default);

/// <summary>
/// Consolidated dispatch manifest for cross-docked goods at a specific outbound dock bay.
/// </summary>
public record CrossDockStagingManifest(
    Guid ManifestId,
    string DockBay,
    string TargetCarrier,
    IReadOnlyList<CrossDockAllocationRecord> Allocations,
    decimal TotalAllocatedUnits,
    string CourierWaybillReference,
    DateTime GeneratedAtUtc);

#endregion

#region API Contracts

/// <summary>
/// Request to generate a Holt-Winters forecast and dynamic safety stock / ROP parameters.
/// </summary>
public record CalculateForecastRequest(
    string Sku,
    IReadOnlyList<decimal> HistoricalDemand,
    int ForecastHorizonDays = 7,
    int Period = 7,
    decimal? Alpha = 0.2m,
    decimal? Beta = 0.1m,
    decimal? Gamma = 0.3m,
    decimal? LeadTimeDays = null,
    decimal? LeadTimeStdDev = null,
    decimal? UnitHoldingCost = null,
    decimal? OrderSetupCost = null,
    ServiceLevelConfidence Confidence = ServiceLevelConfidence.Sla99);

/// <summary>
/// Response with Holt-Winters forecast and computed supply chain replenishment parameters.
/// </summary>
public record CalculateForecastResponse(
    ForecastResult Forecast,
    decimal DynamicSafetyStock,
    decimal DynamicReorderPoint,
    decimal EconomicOrderQuantity,
    decimal AverageDailyDemand,
    decimal DemandStdDev);

/// <summary>
/// Request to evaluate warehouse stock and trigger auto-replenishment PO proposals.
/// </summary>
public record EvaluateStockRequest(
    Guid WarehouseId,
    IReadOnlyList<SkuVelocityRecord>? SkuRecords = null);

/// <summary>
/// Response summarizing the evaluated warehouse stock and generated PO proposals.
/// </summary>
public record EvaluateStockResponse(
    Guid WarehouseId,
    int TotalSkusEvaluated,
    int ReplenishmentTriggeredCount,
    IReadOnlyList<PurchaseOrderProposal> GeneratedProposals,
    DateTime EvaluatedAtUtc);

/// <summary>
/// Request to dispatch a purchase order proposal to the supplier.
/// </summary>
public record DispatchPoRequest(
    Guid ProposalId);

/// <summary>
/// Response for dispatched purchase order including the generated EDI/JSON order payload.
/// </summary>
public record DispatchPoResponse(
    PurchaseOrderProposal Proposal,
    string TransmissionPayload,
    bool Success,
    string Message);

/// <summary>
/// Request to acknowledge a purchase order with an estimated delivery date.
/// </summary>
public record AcknowledgePoRequest(
    Guid ProposalId,
    DateTime EstimatedDeliveryDate,
    string? SupplierNote = null);

/// <summary>
/// Response for an acknowledged purchase order.
/// </summary>
public record AcknowledgePoResponse(
    PurchaseOrderProposal Proposal,
    bool Success,
    string Message);

/// <summary>
/// Request to allocate an inbound receipt directly to outbound backordered customer orders.
/// </summary>
public record CrossDockAllocateRequest(
    InboundReceipt Receipt,
    IReadOnlyList<CustomerBackorderOrder>? PendingOrders = null);

/// <summary>
/// Response detailing cross-dock allocations, staged manifests, and remaining quantity for putaway.
/// </summary>
public record CrossDockAllocateResponse(
    InboundReceipt Receipt,
    decimal TotalReceived,
    decimal TotalAllocated,
    decimal RemainingToPutaway,
    IReadOnlyList<CrossDockAllocationRecord> Allocations,
    IReadOnlyList<CrossDockStagingManifest> StagingManifests);

/// <summary>
/// Supply chain replenishment engine health and metadata.
/// </summary>
public record SupplyChainEngineHealth(
    string Status,
    string Version,
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string> Capabilities);

#endregion
