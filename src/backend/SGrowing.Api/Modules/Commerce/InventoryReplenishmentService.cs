using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Commerce;

#region Enums & Constants

public static class StockHealthTier
{
    public const string CriticalOutOfStock = "CriticalOutOfStock";
    public const string LowStockBreach = "LowStockBreach";
    public const string AtRisk = "AtRisk";
    public const string Optimal = "Optimal";
    public const string Overstocked = "Overstocked";

    public static readonly string[] All =
    {
        CriticalOutOfStock,
        LowStockBreach,
        AtRisk,
        Optimal,
        Overstocked
    };
}

public static class PurchaseOrderStatus
{
    public const string Draft = "Draft";
    public const string SubmittedToVendor = "SubmittedToVendor";
    public const string Acknowledged = "Acknowledged";
    public const string InTransit = "InTransit";
    public const string Received = "Received";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft,
        SubmittedToVendor,
        Acknowledged,
        InTransit,
        Received,
        Cancelled
    };

    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            (Draft, SubmittedToVendor) => true,
            (Draft, Cancelled) => true,
            (SubmittedToVendor, Acknowledged) => true,
            (SubmittedToVendor, Cancelled) => true,
            (Acknowledged, InTransit) => true,
            (Acknowledged, Cancelled) => true,
            (InTransit, Received) => true,
            _ => false
        };
}

public static class TransferProposalStatus
{
    public const string Proposed = "Proposed";
    public const string Approved = "Approved";
    public const string InTransit = "InTransit";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
}

public static class TransferUrgency
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

#endregion

#region DTOs & Contracts

public record VendorDto(
    string Id,
    string Name,
    string ContactPerson,
    string ContactEmail,
    string ContactPhone,
    string Country,
    string Currency,
    int StandardLeadTimeDays,
    double LeadTimeVarianceFactor,
    int Moq,
    int BatchMultiplier,
    decimal StandardOrderCostUsd,
    decimal DefaultTaxRatePercent);

public record WarehouseStockDto(
    string WarehouseId,
    string WarehouseName,
    string LocationCode,
    string City,
    int CurrentStock,
    int AllocatedStock,
    int AvailableStock,
    double DailyVelocity,
    double DoirDays,
    int SafetyStockUnits,
    int ReorderPointUnits,
    int EoqUnits,
    string HealthTier,
    DateTime LastAuditedAtUtc);

public record InventoryItemDto(
    string Sku,
    string ProductName,
    string Category,
    string Barcode,
    decimal UnitCostUsd,
    decimal RetailPriceUsd,
    string PrimaryVendorId,
    string PrimaryVendorName,
    int TotalCurrentStock,
    int TotalAllocatedStock,
    int TotalAvailableStock,
    double CombinedDailyVelocity,
    double CombinedDoirDays,
    int CombinedSafetyStockUnits,
    int CombinedReorderPointUnits,
    int CombinedEoqUnits,
    string AggregatedHealthTier,
    decimal TotalAssetValueUsd,
    List<int> Past30DaysDemandHistory,
    List<WarehouseStockDto> WarehouseBreakdown,
    DateTime LastReplenishedAtUtc);

public record StockHealthSummaryDto(
    int TotalSkus,
    int CriticalOutOfStockCount,
    int LowStockBreachCount,
    int AtRiskCount,
    int OptimalCount,
    int OverstockedCount,
    int TotalUnitsOnHand,
    decimal TotalAssetValueUsd,
    int ActivePurchaseOrdersCount,
    decimal ActivePoCommittedValueUsd,
    int PendingRebalanceTransfersCount,
    DateTime EvaluatedAtUtc);

public record PurchaseOrderLineItemDto(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitCostUsd,
    decimal LineTotalUsd,
    int CalculatedDeficit,
    int AppliedMoq,
    int AppliedBatchMultiplier);

public record PurchaseOrderDto(
    Guid Id,
    Guid TenantId,
    string PoNumber,
    string VendorId,
    string VendorName,
    string VendorContactEmail,
    string DestinationWarehouseId,
    string DestinationWarehouseName,
    string Status,
    List<PurchaseOrderLineItemDto> Items,
    int TotalUnits,
    decimal SubtotalUsd,
    decimal TaxAmountUsd,
    decimal ShippingFeeUsd,
    decimal TotalAmountUsd,
    string Currency,
    int EstimatedLeadTimeDays,
    DateTime? ExpectedDeliveryDateUtc,
    string? TrackingNumber,
    string? Notes,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ReceivedAtUtc);

public record WarehouseTransferProposalDto(
    Guid ProposalId,
    Guid TenantId,
    string Sku,
    string ProductName,
    string SourceWarehouseId,
    string SourceWarehouseName,
    int SourceStockBefore,
    int SourceRop,
    double SourceDoirBefore,
    string DestinationWarehouseId,
    string DestinationWarehouseName,
    int DestinationStockBefore,
    int DestinationRop,
    double DestinationDoirBefore,
    int SuggestedQuantity,
    int EstimatedTransitDays,
    decimal EstimatedFreightCostUsd,
    string Urgency,
    string Reason,
    string Status,
    DateTime CreatedAtUtc);

public record ExecuteTransferResultDto(
    Guid ProposalId,
    string Sku,
    string SourceWarehouseId,
    int SourceStockAfter,
    string DestinationWarehouseId,
    int DestinationStockAfter,
    int TransferredQuantity,
    string Status,
    DateTime ExecutedAtUtc);

public record InventoryRecalculationResultDto(
    int TotalSkusProcessed,
    int BreachedRopCount,
    int CriticalOutOfStockCount,
    double ServiceLevelPercent,
    double ZFactorUsed,
    decimal DefaultOrderCostUsd,
    decimal AnnualHoldingRatePercent,
    DateTime RecalculatedAtUtc);

public record RecalculateInventoryRequest(
    double? ServiceLevelPercent = 95.0,
    decimal? OrderCostOverride = null,
    decimal? AnnualHoldingRateOverride = null);

public record GenerateAutonomousPoRequest(
    string? TargetWarehouseId = null,
    bool IncludeAtRiskItems = false,
    string? InitiatedBy = "Autonomous Inventory Engine");

public record CreateManualPoRequest(
    string VendorId,
    string DestinationWarehouseId,
    string Sku,
    int Quantity,
    decimal? UnitCostOverride = null,
    string? Notes = null,
    string CreatedBy = "Inventory Manager");

public record UpdatePoStatusRequest(
    string NextStatus,
    string? TrackingNumber = null,
    string? Notes = null,
    string UpdatedBy = "System");

public record ExecuteTransferRequest(
    Guid ProposalId,
    int? OverrideQuantity = null,
    string ApprovedBy = "Warehouse Logistics Lead");

#endregion

#region Service Interface

public interface IInventoryReplenishmentService
{
    // Inventory Items & Search
    Task<List<InventoryItemDto>> GetInventoryItemsAsync(
        Guid tenantId,
        string? searchQuery = null,
        string? category = null,
        string? healthTier = null,
        string? warehouseId = null);

    Task<InventoryItemDto?> GetItemBySkuAsync(Guid tenantId, string sku);

    // Stock Health Summary KPIs
    Task<StockHealthSummaryDto> GetHealthSummaryAsync(Guid tenantId);

    // Inventory Recalculation
    Task<InventoryRecalculationResultDto> RecalculateMetricsAsync(
        Guid tenantId,
        RecalculateInventoryRequest? request = null);

    // Autonomous & Manual Purchase Orders
    Task<List<PurchaseOrderDto>> GenerateAutonomousPurchaseOrdersAsync(
        Guid tenantId,
        GenerateAutonomousPoRequest? request = null);

    Task<PurchaseOrderDto> CreateManualPurchaseOrderAsync(
        Guid tenantId,
        CreateManualPoRequest request);

    Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(
        Guid tenantId,
        string? status = null,
        string? vendorId = null,
        string? warehouseId = null);

    Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(Guid tenantId, Guid poId);

    Task<PurchaseOrderDto> UpdatePurchaseOrderStatusAsync(
        Guid tenantId,
        Guid poId,
        UpdatePoStatusRequest request);

    // Multi-Warehouse Rebalancing Transfers
    Task<List<WarehouseTransferProposalDto>> GetTransferRecommendationsAsync(Guid tenantId);

    Task<ExecuteTransferResultDto> ExecuteTransferAsync(
        Guid tenantId,
        ExecuteTransferRequest request);
}

#endregion

#region Implementation

public class InventoryReplenishmentService : IInventoryReplenishmentService
{
    // Singleton fallback instance for clean zero-config endpoint execution
    private static readonly Lazy<InventoryReplenishmentService> s_instance =
        new(() => new InventoryReplenishmentService());

    public static InventoryReplenishmentService Instance => s_instance.Value;

    // In-memory tenant partitioned storage
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, InventoryItemRecord>> s_tenantItems = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, VendorRecord>> s_tenantVendors = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, WarehouseRecord>> s_tenantWarehouses = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, PurchaseOrderRecord>> s_tenantPurchaseOrders = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, WarehouseTransferProposalRecord>> s_tenantTransfers = new();

    private static readonly object s_initLock = new();
    private static bool s_seeded = false;

    public InventoryReplenishmentService()
    {
        EnsureSeeded();
    }

    #region Exact Inventory Math Formulas (Pure & Tested)

    /// <summary>
    /// Daily Velocity = Sum(30-day demand) / 30.0
    /// </summary>
    public static double CalculateDailyVelocity(IEnumerable<int>? dailyDemands)
    {
        if (dailyDemands is null) return 0.0;
        var list = dailyDemands.ToList();
        if (list.Count == 0) return 0.0;
        return (double)list.Sum() / 30.0;
    }

    /// <summary>
    /// Days of Inventory Remaining (DOIR) = CurrentStock / DailyVelocity
    /// Capped to 999.0 if velocity is near zero or zero to prevent division by zero/overflow.
    /// Returns 0.0 if stock is 0.
    /// </summary>
    public static double CalculateDoir(int currentStock, double dailyVelocity)
    {
        if (currentStock <= 0) return 0.0;
        if (dailyVelocity <= 0.0001) return 999.0;
        var doir = (double)currentStock / dailyVelocity;
        return double.IsInfinity(doir) || double.IsNaN(doir) ? 999.0 : Math.Round(Math.Min(doir, 999.0), 2);
    }

    /// <summary>
    /// Dynamic Safety Stock = Z * LeadTimeVarianceFactor * sqrt(DailyDemandMean)
    /// where Z = 1.65 (95% service level) or 2.33 (99% service level).
    /// </summary>
    public static double CalculateDynamicSafetyStock(double zFactor, double leadTimeVarianceFactor, double dailyDemandMean)
    {
        if (dailyDemandMean <= 0.0 || zFactor <= 0.0 || leadTimeVarianceFactor <= 0.0)
            return 0.0;

        return zFactor * leadTimeVarianceFactor * Math.Sqrt(dailyDemandMean);
    }

    public static int CalculateDynamicSafetyStockUnits(double zFactor, double leadTimeVarianceFactor, double dailyDemandMean)
    {
        var raw = CalculateDynamicSafetyStock(zFactor, leadTimeVarianceFactor, dailyDemandMean);
        return (int)Math.Ceiling(raw);
    }

    /// <summary>
    /// Resolves Z-factor from service level percentage (e.g. 95% -> 1.65, 99% -> 2.33).
    /// </summary>
    public static double ResolveZFactor(double serviceLevelPercent)
    {
        if (serviceLevelPercent >= 99.9) return 3.09;
        if (serviceLevelPercent >= 99.0) return 2.33;
        if (serviceLevelPercent >= 97.5) return 1.96;
        if (serviceLevelPercent >= 95.0) return 1.65;
        if (serviceLevelPercent >= 90.0) return 1.28;
        if (serviceLevelPercent >= 85.0) return 1.04;
        return 1.65; // Default standard 95%
    }

    /// <summary>
    /// Reorder Point (ROP) = (LeadTimeDays * DailyVelocity) + SafetyStock
    /// </summary>
    public static double CalculateReorderPoint(int leadTimeDays, double dailyVelocity, double safetyStock)
    {
        if (leadTimeDays < 0 || dailyVelocity < 0.0) return 0.0;
        return (leadTimeDays * dailyVelocity) + safetyStock;
    }

    public static int CalculateReorderPointUnits(int leadTimeDays, double dailyVelocity, double safetyStock)
    {
        var raw = CalculateReorderPoint(leadTimeDays, dailyVelocity, safetyStock);
        return (int)Math.Ceiling(raw);
    }

    /// <summary>
    /// Economic Order Quantity (EOQ) = sqrt((2 * AnnualDemand * OrderCost) / HoldingCostPerUnitPerYear)
    /// </summary>
    public static double CalculateEoq(double annualDemand, decimal orderCost, decimal holdingCostPerUnitPerYear)
    {
        if (annualDemand <= 0.0 || orderCost <= 0m || holdingCostPerUnitPerYear <= 0.0001m)
            return 0.0;

        var numerator = 2.0 * annualDemand * (double)orderCost;
        var denominator = (double)holdingCostPerUnitPerYear;
        return Math.Sqrt(numerator / denominator);
    }

    public static int CalculateEoqUnits(double annualDemand, decimal orderCost, decimal holdingCostPerUnitPerYear)
    {
        var raw = CalculateEoq(annualDemand, orderCost, holdingCostPerUnitPerYear);
        return (int)Math.Ceiling(raw);
    }

    /// <summary>
    /// Stock Health Classification:
    /// - CriticalOutOfStock: Stock == 0
    /// - LowStockBreach: Stock <= ROP (and Stock > 0)
    /// - AtRisk: Stock <= ROP * 1.25 (and Stock > ROP)
    /// - Optimal: ROP * 1.25 < Stock <= ROP * 3.5 (and DOIR <= 90 days)
    /// - Overstocked: Stock > ROP * 3.5 (or DOIR > 90 days)
    /// </summary>
    public static string ClassifyStockHealth(int currentStock, double rop, double doir)
    {
        if (currentStock <= 0)
            return StockHealthTier.CriticalOutOfStock;

        if (currentStock <= rop)
            return StockHealthTier.LowStockBreach;

        if (currentStock <= rop * 1.25)
            return StockHealthTier.AtRisk;

        if (currentStock > rop * 3.5 || doir > 90.0)
            return StockHealthTier.Overstocked;

        return StockHealthTier.Optimal;
    }

    /// <summary>
    /// Snaps required replenishment quantity to Vendor MOQ and Batch Multiplier.
    /// Base deficit: Math.Max(EOQ, (ROP - CurrentStock) + SafetyStock).
    /// Snaps to: Max(def, MOQ) and rounds up to the nearest multiple of BatchMultiplier.
    /// </summary>
    public static int CalculateReplenishmentQuantity(
        int currentStock,
        double rop,
        double eoq,
        double safetyStock,
        int moq,
        int batchMultiplier)
    {
        var deficit = Math.Max(eoq, (rop - currentStock) + safetyStock);
        var rawQty = (int)Math.Ceiling(Math.Max(1.0, deficit));

        // Enforce Minimum Order Quantity
        var qtyWithMoq = Math.Max(rawQty, Math.Max(1, moq));

        // Enforce Batch Multiplier (pack/case increment)
        if (batchMultiplier > 1)
        {
            var batches = (int)Math.Ceiling((double)qtyWithMoq / batchMultiplier);
            return batches * batchMultiplier;
        }

        return qtyWithMoq;
    }

    #endregion

    #region Inventory Queries & Health Summary

    public Task<List<InventoryItemDto>> GetInventoryItemsAsync(
        Guid tenantId,
        string? searchQuery = null,
        string? category = null,
        string? healthTier = null,
        string? warehouseId = null)
    {
        var itemsDict = GetTenantItems(tenantId);
        var query = itemsDict.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.Sku.ToLowerInvariant().Contains(q) ||
                i.ProductName.ToLowerInvariant().Contains(q) ||
                i.Barcode.ToLowerInvariant().Contains(q) ||
                i.Category.ToLowerInvariant().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(healthTier))
        {
            query = query.Where(i => string.Equals(i.AggregatedHealthTier, healthTier, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            query = query.Where(i => i.WarehouseStocks.ContainsKey(warehouseId));
        }

        var results = query
            .OrderBy(i => HealthTierPriority(i.AggregatedHealthTier))
            .ThenBy(i => i.ProductName)
            .Select(i => MapToDto(tenantId, i))
            .ToList();

        return Task.FromResult(results);
    }

    public Task<InventoryItemDto?> GetItemBySkuAsync(Guid tenantId, string sku)
    {
        var itemsDict = GetTenantItems(tenantId);
        if (itemsDict.TryGetValue(sku, out var item))
        {
            return Task.FromResult<InventoryItemDto?>(MapToDto(tenantId, item));
        }
        return Task.FromResult<InventoryItemDto?>(null);
    }

    public Task<StockHealthSummaryDto> GetHealthSummaryAsync(Guid tenantId)
    {
        var itemsDict = GetTenantItems(tenantId);
        var posDict = GetTenantPurchaseOrders(tenantId);
        var transfersDict = GetTenantTransfers(tenantId);

        var allItems = itemsDict.Values.ToList();

        var criticalCount = allItems.Count(i => i.AggregatedHealthTier == StockHealthTier.CriticalOutOfStock);
        var lowBreachCount = allItems.Count(i => i.AggregatedHealthTier == StockHealthTier.LowStockBreach);
        var atRiskCount = allItems.Count(i => i.AggregatedHealthTier == StockHealthTier.AtRisk);
        var optimalCount = allItems.Count(i => i.AggregatedHealthTier == StockHealthTier.Optimal);
        var overstockedCount = allItems.Count(i => i.AggregatedHealthTier == StockHealthTier.Overstocked);

        var totalUnits = allItems.Sum(i => i.WarehouseStocks.Values.Sum(ws => ws.CurrentStock));
        var totalAssetValue = allItems.Sum(i =>
            i.WarehouseStocks.Values.Sum(ws => ws.CurrentStock) * i.UnitCostUsd);

        var activePos = posDict.Values.Where(p =>
            p.Status != PurchaseOrderStatus.Received && p.Status != PurchaseOrderStatus.Cancelled).ToList();

        var activePoCount = activePos.Count;
        var activePoCommittedValue = activePos.Sum(p => p.TotalAmountUsd);

        var pendingTransfers = transfersDict.Values.Count(t =>
            t.Status == TransferProposalStatus.Proposed || t.Status == TransferProposalStatus.Approved);

        var summary = new StockHealthSummaryDto(
            TotalSkus: allItems.Count,
            CriticalOutOfStockCount: criticalCount,
            LowStockBreachCount: lowBreachCount,
            AtRiskCount: atRiskCount,
            OptimalCount: optimalCount,
            OverstockedCount: overstockedCount,
            TotalUnitsOnHand: totalUnits,
            TotalAssetValueUsd: Math.Round(totalAssetValue, 2),
            ActivePurchaseOrdersCount: activePoCount,
            ActivePoCommittedValueUsd: Math.Round(activePoCommittedValue, 2),
            PendingRebalanceTransfersCount: pendingTransfers,
            EvaluatedAtUtc: DateTime.UtcNow);

        return Task.FromResult(summary);
    }

    #endregion

    #region Inventory Recalculation

    public Task<InventoryRecalculationResultDto> RecalculateMetricsAsync(
        Guid tenantId,
        RecalculateInventoryRequest? request = null)
    {
        var itemsDict = GetTenantItems(tenantId);
        var vendorsDict = GetTenantVendors(tenantId);

        var serviceLevel = request?.ServiceLevelPercent ?? 95.0;
        var zFactor = ResolveZFactor(serviceLevel);
        var defaultOrderCost = request?.OrderCostOverride ?? 85.0m;
        var annualHoldingRate = request?.AnnualHoldingRateOverride ?? 0.22m; // 22% annual holding cost

        var breachedCount = 0;
        var criticalCount = 0;

        foreach (var item in itemsDict.Values)
        {
            var vendor = vendorsDict.TryGetValue(item.PrimaryVendorId, out var v)
                ? v
                : new VendorRecord(item.PrimaryVendorId, item.PrimaryVendorName, "General Rep", "vendor@example.com", "+1-800-555", "USA", "USD", 14, 1.5, 50, 25, defaultOrderCost, 19.0m);

            var leadTime = vendor.StandardLeadTimeDays;
            var varianceFactor = vendor.LeadTimeVarianceFactor;
            var orderCost = vendor.StandardOrderCostUsd > 0 ? vendor.StandardOrderCostUsd : defaultOrderCost;
            var unitHoldingCost = item.UnitCostUsd * annualHoldingRate;
            if (unitHoldingCost <= 0.001m) unitHoldingCost = 1.0m;

            // Recalculate per warehouse
            foreach (var ws in item.WarehouseStocks.Values)
            {
                ws.DailyVelocity = CalculateDailyVelocity(ws.DemandHistory);
                ws.DoirDays = CalculateDoir(ws.CurrentStock, ws.DailyVelocity);
                ws.SafetyStockUnits = CalculateDynamicSafetyStockUnits(zFactor, varianceFactor, ws.DailyVelocity);
                ws.ReorderPointUnits = CalculateReorderPointUnits(leadTime, ws.DailyVelocity, ws.SafetyStockUnits);

                var annualDemand = ws.DailyVelocity * 365.0;
                ws.EoqUnits = CalculateEoqUnits(annualDemand, orderCost, unitHoldingCost);
                ws.HealthTier = ClassifyStockHealth(ws.CurrentStock, ws.ReorderPointUnits, ws.DoirDays);
                ws.LastAuditedAtUtc = DateTime.UtcNow;
            }

            // Aggregate overall item stats
            var combinedStock = item.WarehouseStocks.Values.Sum(ws => ws.CurrentStock);
            var combinedAlloc = item.WarehouseStocks.Values.Sum(ws => ws.AllocatedStock);
            var combinedVelocity = CalculateDailyVelocity(item.Past30DaysDemandHistory);
            var combinedDoir = CalculateDoir(combinedStock, combinedVelocity);
            var combinedSafety = CalculateDynamicSafetyStockUnits(zFactor, varianceFactor, combinedVelocity);
            var combinedRop = CalculateReorderPointUnits(leadTime, combinedVelocity, combinedSafety);
            var combinedAnnualDemand = combinedVelocity * 365.0;
            var combinedEoq = CalculateEoqUnits(combinedAnnualDemand, orderCost, unitHoldingCost);

            item.AggregatedHealthTier = ClassifyStockHealth(combinedStock, combinedRop, combinedDoir);

            if (item.AggregatedHealthTier == StockHealthTier.CriticalOutOfStock) criticalCount++;
            if (item.AggregatedHealthTier == StockHealthTier.LowStockBreach) breachedCount++;
        }

        var result = new InventoryRecalculationResultDto(
            TotalSkusProcessed: itemsDict.Count,
            BreachedRopCount: breachedCount,
            CriticalOutOfStockCount: criticalCount,
            ServiceLevelPercent: serviceLevel,
            ZFactorUsed: zFactor,
            DefaultOrderCostUsd: defaultOrderCost,
            AnnualHoldingRatePercent: annualHoldingRate * 100m,
            RecalculatedAtUtc: DateTime.UtcNow);

        return Task.FromResult(result);
    }

    #endregion

    #region Autonomous & Manual Purchase Orders

    public Task<List<PurchaseOrderDto>> GenerateAutonomousPurchaseOrdersAsync(
        Guid tenantId,
        GenerateAutonomousPoRequest? request = null)
    {
        var itemsDict = GetTenantItems(tenantId);
        var vendorsDict = GetTenantVendors(tenantId);
        var warehousesDict = GetTenantWarehouses(tenantId);
        var posDict = GetTenantPurchaseOrders(tenantId);

        var targetWarehouseId = request?.TargetWarehouseId;
        var includeAtRisk = request?.IncludeAtRiskItems ?? false;
        var initiator = request?.InitiatedBy ?? "Autonomous Inventory Engine";

        // Collect items requiring replenishment
        var replenishmentNeeded = new List<(InventoryItemRecord item, WarehouseStockRecord ws, VendorRecord vendor)>();

        foreach (var item in itemsDict.Values)
        {
            if (!vendorsDict.TryGetValue(item.PrimaryVendorId, out var vendor))
                continue;

            foreach (var ws in item.WarehouseStocks.Values)
            {
                if (!string.IsNullOrEmpty(targetWarehouseId) &&
                    !string.Equals(ws.WarehouseId, targetWarehouseId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var shouldRestock = ws.HealthTier == StockHealthTier.CriticalOutOfStock ||
                                    ws.HealthTier == StockHealthTier.LowStockBreach ||
                                    (includeAtRisk && ws.HealthTier == StockHealthTier.AtRisk);

                if (shouldRestock)
                {
                    // Check if there is already an active PO in flight for this SKU + Warehouse
                    var hasActivePo = posDict.Values.Any(p =>
                        p.DestinationWarehouseId == ws.WarehouseId &&
                        p.Status != PurchaseOrderStatus.Received &&
                        p.Status != PurchaseOrderStatus.Cancelled &&
                        p.Items.Any(line => line.Sku == item.Sku));

                    if (!hasActivePo)
                    {
                        replenishmentNeeded.Add((item, ws, vendor));
                    }
                }
            }
        }

        if (replenishmentNeeded.Count == 0)
        {
            return Task.FromResult(new List<PurchaseOrderDto>());
        }

        // Group by (VendorId, DestinationWarehouseId)
        var grouped = replenishmentNeeded
            .GroupBy(g => (g.vendor.Id, g.ws.WarehouseId));

        var generatedOrders = new List<PurchaseOrderDto>();

        foreach (var group in grouped)
        {
            var vendor = group.First().vendor;
            var destinationWhId = group.Key.WarehouseId;
            var destinationWhName = warehousesDict.TryGetValue(destinationWhId, out var wh) ? wh.Name : destinationWhId;

            var poNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            var lineItems = new List<PurchaseOrderLineItemRecord>();

            foreach (var (item, ws, v) in group)
            {
                var deficit = Math.Max(ws.EoqUnits, (ws.ReorderPointUnits - ws.CurrentStock) + ws.SafetyStockUnits);
                var qty = CalculateReplenishmentQuantity(
                    ws.CurrentStock,
                    ws.ReorderPointUnits,
                    ws.EoqUnits,
                    ws.SafetyStockUnits,
                    v.Moq,
                    v.BatchMultiplier);

                var lineTotal = qty * item.UnitCostUsd;
                lineItems.Add(new PurchaseOrderLineItemRecord(
                    item.Sku,
                    item.ProductName,
                    qty,
                    item.UnitCostUsd,
                    lineTotal,
                    deficit,
                    v.Moq,
                    v.BatchMultiplier));
            }

            var subtotal = lineItems.Sum(li => li.LineTotalUsd);
            var taxAmount = Math.Round(subtotal * (vendor.DefaultTaxRatePercent / 100m), 2);
            var shippingFee = subtotal > 5000m ? 0m : 150.0m;
            var totalAmount = subtotal + taxAmount + shippingFee;

            var expectedDelivery = DateTime.UtcNow.AddDays(vendor.StandardLeadTimeDays);

            var poRecord = new PurchaseOrderRecord(
                Guid.NewGuid(),
                tenantId,
                poNumber,
                vendor.Id,
                vendor.Name,
                vendor.ContactEmail,
                destinationWhId,
                destinationWhName,
                PurchaseOrderStatus.SubmittedToVendor, // Auto engine automatically transmits to vendor
                lineItems,
                lineItems.Sum(li => li.Quantity),
                subtotal,
                taxAmount,
                shippingFee,
                totalAmount,
                vendor.Currency,
                vendor.StandardLeadTimeDays,
                expectedDelivery,
                null,
                $"Auto-replenishment triggered by {initiator} on ROP breach.",
                initiator,
                DateTime.UtcNow,
                DateTime.UtcNow,
                null);

            posDict[poRecord.Id] = poRecord;
            generatedOrders.Add(MapToDto(poRecord));
        }

        return Task.FromResult(generatedOrders);
    }

    public Task<PurchaseOrderDto> CreateManualPurchaseOrderAsync(
        Guid tenantId,
        CreateManualPoRequest request)
    {
        var itemsDict = GetTenantItems(tenantId);
        var vendorsDict = GetTenantVendors(tenantId);
        var warehousesDict = GetTenantWarehouses(tenantId);
        var posDict = GetTenantPurchaseOrders(tenantId);

        if (!itemsDict.TryGetValue(request.Sku, out var item))
            throw new KeyNotFoundException($"Inventory SKU '{request.Sku}' not found.");

        if (!vendorsDict.TryGetValue(request.VendorId, out var vendor))
            throw new KeyNotFoundException($"Vendor '{request.VendorId}' not found.");

        var whName = warehousesDict.TryGetValue(request.DestinationWarehouseId, out var wh)
            ? wh.Name
            : request.DestinationWarehouseId;

        // Apply MOQ & batch multipliers
        var snappedQty = CalculateReplenishmentQuantity(
            0,
            0,
            request.Quantity,
            0,
            vendor.Moq,
            vendor.BatchMultiplier);

        var unitCost = request.UnitCostOverride ?? item.UnitCostUsd;
        var subtotal = snappedQty * unitCost;
        var taxAmount = Math.Round(subtotal * (vendor.DefaultTaxRatePercent / 100m), 2);
        var shippingFee = 120.0m;
        var totalAmount = subtotal + taxAmount + shippingFee;
        var poNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-M{Random.Shared.Next(100, 999)}";

        var lineItem = new PurchaseOrderLineItemRecord(
            item.Sku,
            item.ProductName,
            snappedQty,
            unitCost,
            subtotal,
            request.Quantity,
            vendor.Moq,
            vendor.BatchMultiplier);

        var poRecord = new PurchaseOrderRecord(
            Guid.NewGuid(),
            tenantId,
            poNumber,
            vendor.Id,
            vendor.Name,
            vendor.ContactEmail,
            request.DestinationWarehouseId,
            whName,
            PurchaseOrderStatus.Draft,
            new List<PurchaseOrderLineItemRecord> { lineItem },
            snappedQty,
            subtotal,
            taxAmount,
            shippingFee,
            totalAmount,
            vendor.Currency,
            vendor.StandardLeadTimeDays,
            DateTime.UtcNow.AddDays(vendor.StandardLeadTimeDays),
            null,
            request.Notes ?? "Manual PO created via Replenishment Cockpit",
            request.CreatedBy,
            DateTime.UtcNow,
            null,
            null);

        posDict[poRecord.Id] = poRecord;
        return Task.FromResult(MapToDto(poRecord));
    }

    public Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(
        Guid tenantId,
        string? status = null,
        string? vendorId = null,
        string? warehouseId = null)
    {
        var posDict = GetTenantPurchaseOrders(tenantId);
        var query = posDict.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Where(p => string.Equals(p.VendorId, vendorId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            query = query.Where(p => string.Equals(p.DestinationWarehouseId, warehouseId, StringComparison.OrdinalIgnoreCase));
        }

        var results = query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(MapToDto)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(Guid tenantId, Guid poId)
    {
        var posDict = GetTenantPurchaseOrders(tenantId);
        if (posDict.TryGetValue(poId, out var po))
        {
            return Task.FromResult<PurchaseOrderDto?>(MapToDto(po));
        }
        return Task.FromResult<PurchaseOrderDto?>(null);
    }

    public Task<PurchaseOrderDto> UpdatePurchaseOrderStatusAsync(
        Guid tenantId,
        Guid poId,
        UpdatePoStatusRequest request)
    {
        var posDict = GetTenantPurchaseOrders(tenantId);
        if (!posDict.TryGetValue(poId, out var po))
            throw new KeyNotFoundException($"Purchase Order '{poId}' was not found.");

        var nextStatus = request.NextStatus;

        if (!PurchaseOrderStatus.All.Contains(nextStatus))
            throw new ArgumentException($"Invalid status '{nextStatus}'.");

        if (!PurchaseOrderStatus.CanTransition(po.Status, nextStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition Purchase Order {po.PoNumber} from state '{po.Status}' to '{nextStatus}'.");
        }

        po.Status = nextStatus;
        if (!string.IsNullOrWhiteSpace(request.TrackingNumber))
        {
            po.TrackingNumber = request.TrackingNumber;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            po.Notes = string.IsNullOrWhiteSpace(po.Notes)
                ? request.Notes
                : $"{po.Notes} | {request.Notes}";
        }

        if (nextStatus == PurchaseOrderStatus.SubmittedToVendor && po.SubmittedAtUtc is null)
        {
            po.SubmittedAtUtc = DateTime.UtcNow;
        }

        // When goods are received at warehouse, increment warehouse stock!
        if (nextStatus == PurchaseOrderStatus.Received)
        {
            po.ReceivedAtUtc = DateTime.UtcNow;
            ReceiveStockIntoWarehouse(tenantId, po);
        }

        return Task.FromResult(MapToDto(po));
    }

    private void ReceiveStockIntoWarehouse(Guid tenantId, PurchaseOrderRecord po)
    {
        var itemsDict = GetTenantItems(tenantId);

        foreach (var line in po.Items)
        {
            if (itemsDict.TryGetValue(line.Sku, out var item))
            {
                if (item.WarehouseStocks.TryGetValue(po.DestinationWarehouseId, out var ws))
                {
                    ws.CurrentStock += line.Quantity;
                    ws.DoirDays = CalculateDoir(ws.CurrentStock, ws.DailyVelocity);
                    ws.HealthTier = ClassifyStockHealth(ws.CurrentStock, ws.ReorderPointUnits, ws.DoirDays);
                    ws.LastAuditedAtUtc = DateTime.UtcNow;
                }

                // Recalculate item overall
                var totalStock = item.WarehouseStocks.Values.Sum(s => s.CurrentStock);
                var totalVelocity = CalculateDailyVelocity(item.Past30DaysDemandHistory);
                var totalDoir = CalculateDoir(totalStock, totalVelocity);
                var totalSafety = item.WarehouseStocks.Values.Sum(s => s.SafetyStockUnits);
                var totalRop = item.WarehouseStocks.Values.Sum(s => s.ReorderPointUnits);

                item.AggregatedHealthTier = ClassifyStockHealth(totalStock, totalRop, totalDoir);
                item.LastReplenishedAtUtc = DateTime.UtcNow;
            }
        }
    }

    #endregion

    #region Multi-Warehouse Rebalancing Transfers

    public Task<List<WarehouseTransferProposalDto>> GetTransferRecommendationsAsync(Guid tenantId)
    {
        var itemsDict = GetTenantItems(tenantId);
        var warehousesDict = GetTenantWarehouses(tenantId);
        var transfersDict = GetTenantTransfers(tenantId);

        var proposals = new List<WarehouseTransferProposalDto>();

        foreach (var item in itemsDict.Values)
        {
            // Find deficit warehouses (Critical or LowStockBreach or DOIR < 7)
            var deficitWarehouses = item.WarehouseStocks.Values
                .Where(ws => ws.HealthTier == StockHealthTier.CriticalOutOfStock ||
                             ws.HealthTier == StockHealthTier.LowStockBreach ||
                             ws.DoirDays < 7.0)
                .ToList();

            if (deficitWarehouses.Count == 0) continue;

            // Find surplus warehouses (Overstocked or Optimal with DOIR > 30 and CurrentStock > ROP * 2.0)
            var surplusWarehouses = item.WarehouseStocks.Values
                .Where(ws => (ws.HealthTier == StockHealthTier.Overstocked || ws.HealthTier == StockHealthTier.Optimal) &&
                             ws.DoirDays > 30.0 &&
                             ws.CurrentStock > ws.ReorderPointUnits * 1.8)
                .OrderByDescending(ws => ws.CurrentStock - ws.ReorderPointUnits)
                .ToList();

            if (surplusWarehouses.Count == 0) continue;

            foreach (var deficit in deficitWarehouses)
            {
                foreach (var surplus in surplusWarehouses)
                {
                    if (surplus.WarehouseId == deficit.WarehouseId) continue;

                    // Surplus warehouse must keep at least 1.5 * ROP
                    var surplusSafeRetention = (int)Math.Ceiling(surplus.ReorderPointUnits * 1.5);
                    var maxTransferableFromSurplus = Math.Max(0, surplus.CurrentStock - surplusSafeRetention);

                    // Deficit needs up to 1.5 * ROP
                    var deficitTarget = (int)Math.Ceiling(deficit.ReorderPointUnits * 1.5);
                    var neededAtDeficit = Math.Max(0, deficitTarget - deficit.CurrentStock);

                    var proposedQty = Math.Min(neededAtDeficit, maxTransferableFromSurplus);

                    // Minimum viable inter-warehouse transfer quantity
                    if (proposedQty >= 10)
                    {
                        var urgency = deficit.CurrentStock == 0
                            ? TransferUrgency.Critical
                            : (deficit.DoirDays < 5.0 ? TransferUrgency.High : TransferUrgency.Medium);

                        var transitDays = 2; // Central European regional road transit
                        var estimatedFreight = Math.Round(25.0m + (proposedQty * 0.45m), 2);
                        var reason = $"Transfer {proposedQty} units from surplus hub '{surplus.WarehouseName}' (DOIR {surplus.DoirDays:F0}d) to relieve critical deficit at '{deficit.WarehouseName}' (DOIR {deficit.DoirDays:F0}d, ROP {deficit.ReorderPointUnits}).";

                        var proposal = new WarehouseTransferProposalRecord(
                            Guid.NewGuid(),
                            tenantId,
                            item.Sku,
                            item.ProductName,
                            surplus.WarehouseId,
                            surplus.WarehouseName,
                            surplus.CurrentStock,
                            surplus.ReorderPointUnits,
                            surplus.DoirDays,
                            deficit.WarehouseId,
                            deficit.WarehouseName,
                            deficit.CurrentStock,
                            deficit.ReorderPointUnits,
                            deficit.DoirDays,
                            proposedQty,
                            transitDays,
                            estimatedFreight,
                            urgency,
                            reason,
                            TransferProposalStatus.Proposed,
                            DateTime.UtcNow);

                        transfersDict[proposal.ProposalId] = proposal;
                        proposals.Add(MapToDto(proposal));
                        break; // Move to next deficit node
                    }
                }
            }
        }

        return Task.FromResult(proposals);
    }

    public Task<ExecuteTransferResultDto> ExecuteTransferAsync(
        Guid tenantId,
        ExecuteTransferRequest request)
    {
        var transfersDict = GetTenantTransfers(tenantId);
        if (!transfersDict.TryGetValue(request.ProposalId, out var proposal))
            throw new KeyNotFoundException($"Transfer Proposal '{request.ProposalId}' was not found.");

        if (proposal.Status == TransferProposalStatus.Completed)
            throw new InvalidOperationException($"Transfer Proposal '{request.ProposalId}' has already been executed.");

        var itemsDict = GetTenantItems(tenantId);
        if (!itemsDict.TryGetValue(proposal.Sku, out var item))
            throw new KeyNotFoundException($"SKU '{proposal.Sku}' was not found.");

        if (!item.WarehouseStocks.TryGetValue(proposal.SourceWarehouseId, out var sourceStock))
            throw new InvalidOperationException($"Source warehouse '{proposal.SourceWarehouseId}' has no record for SKU {proposal.Sku}.");

        if (!item.WarehouseStocks.TryGetValue(proposal.DestinationWarehouseId, out var destStock))
            throw new InvalidOperationException($"Destination warehouse '{proposal.DestinationWarehouseId}' has no record for SKU {proposal.Sku}.");

        var transferQty = request.OverrideQuantity ?? proposal.SuggestedQuantity;

        if (transferQty <= 0)
            throw new ArgumentException("Transfer quantity must be greater than zero.");

        if (sourceStock.CurrentStock < transferQty)
            throw new InvalidOperationException($"Source warehouse '{sourceStock.WarehouseName}' has insufficient stock ({sourceStock.CurrentStock}) to transfer {transferQty} units.");

        // Deduct from source and add to destination
        sourceStock.CurrentStock -= transferQty;
        sourceStock.DoirDays = CalculateDoir(sourceStock.CurrentStock, sourceStock.DailyVelocity);
        sourceStock.HealthTier = ClassifyStockHealth(sourceStock.CurrentStock, sourceStock.ReorderPointUnits, sourceStock.DoirDays);
        sourceStock.LastAuditedAtUtc = DateTime.UtcNow;

        destStock.CurrentStock += transferQty;
        destStock.DoirDays = CalculateDoir(destStock.CurrentStock, destStock.DailyVelocity);
        destStock.HealthTier = ClassifyStockHealth(destStock.CurrentStock, destStock.ReorderPointUnits, destStock.DoirDays);
        destStock.LastAuditedAtUtc = DateTime.UtcNow;

        // Recalculate combined health
        var combinedStock = item.WarehouseStocks.Values.Sum(ws => ws.CurrentStock);
        var combinedVelocity = CalculateDailyVelocity(item.Past30DaysDemandHistory);
        var combinedDoir = CalculateDoir(combinedStock, combinedVelocity);
        var combinedRop = item.WarehouseStocks.Values.Sum(ws => ws.ReorderPointUnits);
        item.AggregatedHealthTier = ClassifyStockHealth(combinedStock, combinedRop, combinedDoir);

        proposal.Status = TransferProposalStatus.Completed;

        var result = new ExecuteTransferResultDto(
            ProposalId: proposal.ProposalId,
            Sku: proposal.Sku,
            SourceWarehouseId: proposal.SourceWarehouseId,
            SourceStockAfter: sourceStock.CurrentStock,
            DestinationWarehouseId: proposal.DestinationWarehouseId,
            DestinationStockAfter: destStock.CurrentStock,
            TransferredQuantity: transferQty,
            Status: TransferProposalStatus.Completed,
            ExecutedAtUtc: DateTime.UtcNow);

        return Task.FromResult(result);
    }

    #endregion

    #region Helper & Seed Logic

    private static int HealthTierPriority(string tier) =>
        tier switch
        {
            StockHealthTier.CriticalOutOfStock => 1,
            StockHealthTier.LowStockBreach => 2,
            StockHealthTier.AtRisk => 3,
            StockHealthTier.Optimal => 4,
            StockHealthTier.Overstocked => 5,
            _ => 6
        };

    private static ConcurrentDictionary<string, InventoryItemRecord> GetTenantItems(Guid tenantId) =>
        s_tenantItems.GetOrAdd(tenantId, _ => SeedItems(tenantId));

    private static ConcurrentDictionary<string, VendorRecord> GetTenantVendors(Guid tenantId) =>
        s_tenantVendors.GetOrAdd(tenantId, _ => SeedVendors());

    private static ConcurrentDictionary<string, WarehouseRecord> GetTenantWarehouses(Guid tenantId) =>
        s_tenantWarehouses.GetOrAdd(tenantId, _ => SeedWarehouses());

    private static ConcurrentDictionary<Guid, PurchaseOrderRecord> GetTenantPurchaseOrders(Guid tenantId) =>
        s_tenantPurchaseOrders.GetOrAdd(tenantId, _ => SeedPurchaseOrders(tenantId));

    private static ConcurrentDictionary<Guid, WarehouseTransferProposalRecord> GetTenantTransfers(Guid tenantId) =>
        s_tenantTransfers.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, WarehouseTransferProposalRecord>());

    private void EnsureSeeded()
    {
        if (s_seeded) return;
        lock (s_initLock)
        {
            if (s_seeded) return;
            var defaultTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
            GetTenantVendors(defaultTenant);
            GetTenantWarehouses(defaultTenant);
            GetTenantItems(defaultTenant);
            GetTenantPurchaseOrders(defaultTenant);
            s_seeded = true;
        }
    }

    private static ConcurrentDictionary<string, VendorRecord> SeedVendors()
    {
        var dict = new ConcurrentDictionary<string, VendorRecord>();

        dict["VND-SHENZHEN-AUDIO"] = new VendorRecord(
            "VND-SHENZHEN-AUDIO",
            "Shenzhen AudioTech Ltd",
            "Chen Wei",
            "orders@shenzhen-audiotech.cn",
            "+86 755 8839 2100",
            "China",
            "USD",
            StandardLeadTimeDays: 14,
            LeadTimeVarianceFactor: 1.8,
            Moq: 50,
            BatchMultiplier: 25,
            StandardOrderCostUsd: 95.0m,
            DefaultTaxRatePercent: 19.0m);

        dict["VND-ADDIS-COFFEE"] = new VendorRecord(
            "VND-ADDIS-COFFEE",
            "Addis Bean Collective",
            "Abebe Tadesse",
            "export@addisbean.org",
            "+251 11 551 8920",
            "Ethiopia",
            "USD",
            StandardLeadTimeDays: 21,
            LeadTimeVarianceFactor: 2.1,
            Moq: 100,
            BatchMultiplier: 50,
            StandardOrderCostUsd: 110.0m,
            DefaultTaxRatePercent: 7.0m);

        dict["VND-NORDIC-ERGO"] = new VendorRecord(
            "VND-NORDIC-ERGO",
            "Nordic Ergonomics Corp",
            "Frederik Lind",
            "supply@nordic-ergo.se",
            "+46 8 440 9200",
            "Sweden",
            "USD",
            StandardLeadTimeDays: 18,
            LeadTimeVarianceFactor: 1.5,
            Moq: 20,
            BatchMultiplier: 10,
            StandardOrderCostUsd: 135.0m,
            DefaultTaxRatePercent: 19.0m);

        dict["VND-ANKERCORE"] = new VendorRecord(
            "VND-ANKERCORE",
            "AnkerCore Electronics",
            "Hiroshi Tanaka",
            "b2b@ankercore.jp",
            "+81 3 5520 8110",
            "Japan",
            "USD",
            StandardLeadTimeDays: 10,
            LeadTimeVarianceFactor: 1.2,
            Moq: 150,
            BatchMultiplier: 50,
            StandardOrderCostUsd: 75.0m,
            DefaultTaxRatePercent: 19.0m);

        dict["VND-APEX-DISPLAY"] = new VendorRecord(
            "VND-APEX-DISPLAY",
            "Apex Display Systems Inc",
            "Marcus Vance",
            "procurement@apexdisplays.com",
            "+1 408 920 4410",
            "United States",
            "USD",
            StandardLeadTimeDays: 16,
            LeadTimeVarianceFactor: 1.7,
            Moq: 30,
            BatchMultiplier: 10,
            StandardOrderCostUsd: 140.0m,
            DefaultTaxRatePercent: 19.0m);

        return dict;
    }

    private static ConcurrentDictionary<string, WarehouseRecord> SeedWarehouses()
    {
        var dict = new ConcurrentDictionary<string, WarehouseRecord>();

        dict["WH-CENTRAL"] = new WarehouseRecord(
            "WH-CENTRAL",
            "Central Fulfillment Hub",
            "HUB-FRA-01",
            "Frankfurt",
            "Germany",
            CapacityUnits: 60000);

        dict["WH-NORTH"] = new WarehouseRecord(
            "WH-NORTH",
            "North Regional Node",
            "NODE-STK-02",
            "Stockholm",
            "Sweden",
            CapacityUnits: 25000);

        dict["WH-SOUTH"] = new WarehouseRecord(
            "WH-SOUTH",
            "South Distribution Center",
            "NODE-MIL-03",
            "Milan",
            "Italy",
            CapacityUnits: 30000);

        dict["WH-WEST"] = new WarehouseRecord(
            "WH-WEST",
            "West Coast Gateway",
            "NODE-ROT-04",
            "Rotterdam",
            "Netherlands",
            CapacityUnits: 35000);

        return dict;
    }

    private static ConcurrentDictionary<string, InventoryItemRecord> SeedItems(Guid tenantId)
    {
        var dict = new ConcurrentDictionary<string, InventoryItemRecord>();
        var zDefault = 1.65; // 95% service level

        // 1. ANC-HEADPHONES-PRO: Wireless ANC Headphones
        var ancDailyDemandsCentral = GenerateDemands(25, 4); // ~25/day -> 750 / month
        var ancDailyDemandsNorth = GenerateDemands(3, 1);    // ~3/day -> 90 / month
        var ancItem = new InventoryItemRecord(
            "ANC-HEADPHONES-PRO",
            "Wireless ANC Headphones Pro",
            "Electronics",
            "8431029481023",
            UnitCostUsd: 85.00m,
            RetailPriceUsd: 199.00m,
            PrimaryVendorId: "VND-SHENZHEN-AUDIO",
            PrimaryVendorName: "Shenzhen AudioTech Ltd",
            Past30DaysDemandHistory: GenerateDemands(28, 5));

        // WH-CENTRAL: 420 on hand (Optimal, ~17 days)
        var centralAncStock = 420;
        var centralAncVel = CalculateDailyVelocity(ancDailyDemandsCentral);
        var centralAncSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.8, centralAncVel);
        var centralAncRop = CalculateReorderPointUnits(14, centralAncVel, centralAncSafety);
        var centralAncEoq = CalculateEoqUnits(centralAncVel * 365, 95m, 85m * 0.22m);
        var centralAncDoir = CalculateDoir(centralAncStock, centralAncVel);
        ancItem.WarehouseStocks["WH-CENTRAL"] = new WarehouseStockRecord(
            "WH-CENTRAL", "Central Fulfillment Hub", "HUB-FRA-01", "Frankfurt",
            centralAncStock, 20, centralAncStock - 20, centralAncVel, centralAncDoir,
            centralAncSafety, centralAncRop, centralAncEoq,
            ClassifyStockHealth(centralAncStock, centralAncRop, centralAncDoir),
            ancDailyDemandsCentral, DateTime.UtcNow);

        // WH-NORTH: 12 on hand (Critical/LowStock breach! ROP is ~45)
        var northAncStock = 12;
        var northAncVel = CalculateDailyVelocity(ancDailyDemandsNorth);
        var northAncSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.8, northAncVel);
        var northAncRop = CalculateReorderPointUnits(14, northAncVel, northAncSafety);
        var northAncEoq = CalculateEoqUnits(northAncVel * 365, 95m, 85m * 0.22m);
        var northAncDoir = CalculateDoir(northAncStock, northAncVel);
        ancItem.WarehouseStocks["WH-NORTH"] = new WarehouseStockRecord(
            "WH-NORTH", "North Regional Node", "NODE-STK-02", "Stockholm",
            northAncStock, 2, northAncStock - 2, northAncVel, northAncDoir,
            northAncSafety, northAncRop, northAncEoq,
            ClassifyStockHealth(northAncStock, northAncRop, northAncDoir),
            ancDailyDemandsNorth, DateTime.UtcNow);

        var totalAncStock = centralAncStock + northAncStock;
        var totalAncVel = CalculateDailyVelocity(ancItem.Past30DaysDemandHistory);
        var totalAncDoir = CalculateDoir(totalAncStock, totalAncVel);
        ancItem.AggregatedHealthTier = ClassifyStockHealth(totalAncStock, centralAncRop + northAncRop, totalAncDoir);
        dict[ancItem.Sku] = ancItem;

        // 2. ARABICA-COFFEE-ORG-1KG: Organic Arabica Coffee
        var coffeeDemandsCentral = GenerateDemands(18, 3);
        var coffeeDemandsSouth = GenerateDemands(8, 2);
        var coffeeItem = new InventoryItemRecord(
            "ARABICA-COFFEE-ORG-1KG",
            "Organic Arabica Coffee Beans (1kg Bag)",
            "Gourmet Food & Beverage",
            "8431029485511",
            UnitCostUsd: 14.50m,
            RetailPriceUsd: 32.00m,
            PrimaryVendorId: "VND-ADDIS-COFFEE",
            PrimaryVendorName: "Addis Bean Collective",
            Past30DaysDemandHistory: GenerateDemands(26, 4));

        // WH-CENTRAL: 85 on hand (Low Stock Breach, ROP ~ 150)
        var centralCoffeeStock = 85;
        var centralCoffeeVel = CalculateDailyVelocity(coffeeDemandsCentral);
        var centralCoffeeSafety = CalculateDynamicSafetyStockUnits(zDefault, 2.1, centralCoffeeVel);
        var centralCoffeeRop = CalculateReorderPointUnits(21, centralCoffeeVel, centralCoffeeSafety);
        var centralCoffeeEoq = CalculateEoqUnits(centralCoffeeVel * 365, 110m, 14.50m * 0.22m);
        var centralCoffeeDoir = CalculateDoir(centralCoffeeStock, centralCoffeeVel);
        coffeeItem.WarehouseStocks["WH-CENTRAL"] = new WarehouseStockRecord(
            "WH-CENTRAL", "Central Fulfillment Hub", "HUB-FRA-01", "Frankfurt",
            centralCoffeeStock, 15, centralCoffeeStock - 15, centralCoffeeVel, centralCoffeeDoir,
            centralCoffeeSafety, centralCoffeeRop, centralCoffeeEoq,
            ClassifyStockHealth(centralCoffeeStock, centralCoffeeRop, centralCoffeeDoir),
            coffeeDemandsCentral, DateTime.UtcNow);

        // WH-SOUTH: 420 on hand (Optimal)
        var southCoffeeStock = 420;
        var southCoffeeVel = CalculateDailyVelocity(coffeeDemandsSouth);
        var southCoffeeSafety = CalculateDynamicSafetyStockUnits(zDefault, 2.1, southCoffeeVel);
        var southCoffeeRop = CalculateReorderPointUnits(21, southCoffeeVel, southCoffeeSafety);
        var southCoffeeEoq = CalculateEoqUnits(southCoffeeVel * 365, 110m, 14.50m * 0.22m);
        var southCoffeeDoir = CalculateDoir(southCoffeeStock, southCoffeeVel);
        coffeeItem.WarehouseStocks["WH-SOUTH"] = new WarehouseStockRecord(
            "WH-SOUTH", "South Distribution Center", "NODE-MIL-03", "Milan",
            southCoffeeStock, 30, southCoffeeStock - 30, southCoffeeVel, southCoffeeDoir,
            southCoffeeSafety, southCoffeeRop, southCoffeeEoq,
            ClassifyStockHealth(southCoffeeStock, southCoffeeRop, southCoffeeDoir),
            coffeeDemandsSouth, DateTime.UtcNow);

        var totalCoffeeStock = centralCoffeeStock + southCoffeeStock;
        var totalCoffeeVel = CalculateDailyVelocity(coffeeItem.Past30DaysDemandHistory);
        var totalCoffeeDoir = CalculateDoir(totalCoffeeStock, totalCoffeeVel);
        coffeeItem.AggregatedHealthTier = ClassifyStockHealth(totalCoffeeStock, centralCoffeeRop + southCoffeeRop, totalCoffeeDoir);
        dict[coffeeItem.Sku] = coffeeItem;

        // 3. ERGO-DESK-MOT-140: Ergonomic Standing Desk
        var deskDemandsCentral = GenerateDemands(5, 1);
        var deskDemandsWest = GenerateDemands(2, 1);
        var deskItem = new InventoryItemRecord(
            "ERGO-DESK-MOT-140",
            "Ergonomic Motorized Standing Desk 140cm",
            "Office Furniture",
            "8431029492194",
            UnitCostUsd: 210.00m,
            RetailPriceUsd: 499.00m,
            PrimaryVendorId: "VND-NORDIC-ERGO",
            PrimaryVendorName: "Nordic Ergonomics Corp",
            Past30DaysDemandHistory: GenerateDemands(7, 2));

        // WH-CENTRAL: 0 on hand (CriticalOutOfStock)
        var centralDeskStock = 0;
        var centralDeskVel = CalculateDailyVelocity(deskDemandsCentral);
        var centralDeskSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.5, centralDeskVel);
        var centralDeskRop = CalculateReorderPointUnits(18, centralDeskVel, centralDeskSafety);
        var centralDeskEoq = CalculateEoqUnits(centralDeskVel * 365, 135m, 210m * 0.22m);
        var centralDeskDoir = CalculateDoir(centralDeskStock, centralDeskVel);
        deskItem.WarehouseStocks["WH-CENTRAL"] = new WarehouseStockRecord(
            "WH-CENTRAL", "Central Fulfillment Hub", "HUB-FRA-01", "Frankfurt",
            centralDeskStock, 0, 0, centralDeskVel, centralDeskDoir,
            centralDeskSafety, centralDeskRop, centralDeskEoq,
            ClassifyStockHealth(centralDeskStock, centralDeskRop, centralDeskDoir),
            deskDemandsCentral, DateTime.UtcNow);

        // WH-WEST: 38 on hand (Optimal)
        var westDeskStock = 38;
        var westDeskVel = CalculateDailyVelocity(deskDemandsWest);
        var westDeskSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.5, westDeskVel);
        var westDeskRop = CalculateReorderPointUnits(18, westDeskVel, westDeskSafety);
        var westDeskEoq = CalculateEoqUnits(westDeskVel * 365, 135m, 210m * 0.22m);
        var westDeskDoir = CalculateDoir(westDeskStock, westDeskVel);
        deskItem.WarehouseStocks["WH-WEST"] = new WarehouseStockRecord(
            "WH-WEST", "West Coast Gateway", "NODE-ROT-04", "Rotterdam",
            westDeskStock, 5, westDeskStock - 5, westDeskVel, westDeskDoir,
            westDeskSafety, westDeskRop, westDeskEoq,
            ClassifyStockHealth(westDeskStock, westDeskRop, westDeskDoir),
            deskDemandsWest, DateTime.UtcNow);

        deskItem.AggregatedHealthTier = StockHealthTier.CriticalOutOfStock;
        dict[deskItem.Sku] = deskItem;

        // 4. GAN-CHARGER-120W: Ultra-Fast GaN Charger 120W
        var ganDemandsCentral = GenerateDemands(15, 3);
        var ganDemandsNorth = GenerateDemands(4, 1);
        var ganItem = new InventoryItemRecord(
            "GAN-CHARGER-120W",
            "Ultra-Fast GaN Charger 120W Triple-Port",
            "Mobile Accessories",
            "8431029497722",
            UnitCostUsd: 18.00m,
            RetailPriceUsd: 49.00m,
            PrimaryVendorId: "VND-ANKERCORE",
            PrimaryVendorName: "AnkerCore Electronics",
            Past30DaysDemandHistory: GenerateDemands(19, 4));

        // WH-CENTRAL: 1650 on hand (Overstocked! DOIR > 100 days)
        var centralGanStock = 1650;
        var centralGanVel = CalculateDailyVelocity(ganDemandsCentral);
        var centralGanSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.2, centralGanVel);
        var centralGanRop = CalculateReorderPointUnits(10, centralGanVel, centralGanSafety);
        var centralGanEoq = CalculateEoqUnits(centralGanVel * 365, 75m, 18m * 0.22m);
        var centralGanDoir = CalculateDoir(centralGanStock, centralGanVel);
        ganItem.WarehouseStocks["WH-CENTRAL"] = new WarehouseStockRecord(
            "WH-CENTRAL", "Central Fulfillment Hub", "HUB-FRA-01", "Frankfurt",
            centralGanStock, 80, centralGanStock - 80, centralGanVel, centralGanDoir,
            centralGanSafety, centralGanRop, centralGanEoq,
            ClassifyStockHealth(centralGanStock, centralGanRop, centralGanDoir),
            ganDemandsCentral, DateTime.UtcNow);

        // WH-NORTH: 32 on hand (AtRisk / LowStock breach, ROP ~ 45) -> Rebalance from Central!
        var northGanStock = 32;
        var northGanVel = CalculateDailyVelocity(ganDemandsNorth);
        var northGanSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.2, northGanVel);
        var northGanRop = CalculateReorderPointUnits(10, northGanVel, northGanSafety);
        var northGanEoq = CalculateEoqUnits(northGanVel * 365, 75m, 18m * 0.22m);
        var northGanDoir = CalculateDoir(northGanStock, northGanVel);
        ganItem.WarehouseStocks["WH-NORTH"] = new WarehouseStockRecord(
            "WH-NORTH", "North Regional Node", "NODE-STK-02", "Stockholm",
            northGanStock, 5, northGanStock - 5, northGanVel, northGanDoir,
            northGanSafety, northGanRop, northGanEoq,
            ClassifyStockHealth(northGanStock, northGanRop, northGanDoir),
            ganDemandsNorth, DateTime.UtcNow);

        ganItem.AggregatedHealthTier = StockHealthTier.Overstocked;
        dict[ganItem.Sku] = ganItem;

        // 5. OLED-MONITOR-34C: 34" Curved UltraWide OLED Monitor
        var monitorDemandsCentral = GenerateDemands(6, 1);
        var monitorItem = new InventoryItemRecord(
            "OLED-MONITOR-34C",
            "34\" Curved UltraWide QD-OLED Monitor 175Hz",
            "Electronics",
            "8431029501831",
            UnitCostUsd: 520.00m,
            RetailPriceUsd: 980.00m,
            PrimaryVendorId: "VND-APEX-DISPLAY",
            PrimaryVendorName: "Apex Display Systems Inc",
            Past30DaysDemandHistory: GenerateDemands(6, 1));

        var centralMonStock = 110;
        var centralMonVel = CalculateDailyVelocity(monitorDemandsCentral);
        var centralMonSafety = CalculateDynamicSafetyStockUnits(zDefault, 1.7, centralMonVel);
        var centralMonRop = CalculateReorderPointUnits(16, centralMonVel, centralMonSafety);
        var centralMonEoq = CalculateEoqUnits(centralMonVel * 365, 140m, 520m * 0.22m);
        var centralMonDoir = CalculateDoir(centralMonStock, centralMonVel);
        monitorItem.WarehouseStocks["WH-CENTRAL"] = new WarehouseStockRecord(
            "WH-CENTRAL", "Central Fulfillment Hub", "HUB-FRA-01", "Frankfurt",
            centralMonStock, 12, centralMonStock - 12, centralMonVel, centralMonDoir,
            centralMonSafety, centralMonRop, centralMonEoq,
            ClassifyStockHealth(centralMonStock, centralMonRop, centralMonDoir),
            monitorDemandsCentral, DateTime.UtcNow);

        monitorItem.AggregatedHealthTier = ClassifyStockHealth(centralMonStock, centralMonRop, centralMonDoir);
        dict[monitorItem.Sku] = monitorItem;

        return dict;
    }

    private static ConcurrentDictionary<Guid, PurchaseOrderRecord> SeedPurchaseOrders(Guid tenantId)
    {
        var dict = new ConcurrentDictionary<Guid, PurchaseOrderRecord>();

        // PO 1: Recent in-transit order from Shenzhen AudioTech
        var po1Id = Guid.Parse("33333333-3333-3333-3333-333333333331");
        var po1Line = new PurchaseOrderLineItemRecord(
            "ANC-HEADPHONES-PRO",
            "Wireless ANC Headphones Pro",
            Quantity: 200,
            UnitCostUsd: 85.00m,
            LineTotalUsd: 17000.00m,
            CalculatedDeficit: 185,
            AppliedMoq: 50,
            AppliedBatchMultiplier: 25);

        dict[po1Id] = new PurchaseOrderRecord(
            po1Id,
            tenantId,
            "PO-20261025-8812",
            "VND-SHENZHEN-AUDIO",
            "Shenzhen AudioTech Ltd",
            "orders@shenzhen-audiotech.cn",
            "WH-CENTRAL",
            "Central Fulfillment Hub",
            PurchaseOrderStatus.InTransit,
            new List<PurchaseOrderLineItemRecord> { po1Line },
            TotalUnits: 200,
            SubtotalUsd: 17000.00m,
            TaxAmountUsd: 3230.00m,
            ShippingFeeUsd: 0m,
            TotalAmountUsd: 20230.00m,
            Currency: "USD",
            EstimatedLeadTimeDays: 14,
            ExpectedDeliveryDateUtc: DateTime.UtcNow.AddDays(4),
            TrackingNumber: "DHL-EXPRESS-928192841",
            Notes: "Routine container shipment via air cargo.",
            CreatedBy: "Autonomous Inventory Engine",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-10),
            SubmittedAtUtc: DateTime.UtcNow.AddDays(-10),
            ReceivedAtUtc: null);

        // PO 2: Acknowledged order from Addis Bean Collective
        var po2Id = Guid.Parse("33333333-3333-3333-3333-333333333332");
        var po2Line = new PurchaseOrderLineItemRecord(
            "ARABICA-COFFEE-ORG-1KG",
            "Organic Arabica Coffee Beans (1kg Bag)",
            Quantity: 300,
            UnitCostUsd: 14.50m,
            LineTotalUsd: 4350.00m,
            CalculatedDeficit: 275,
            AppliedMoq: 100,
            AppliedBatchMultiplier: 50);

        dict[po2Id] = new PurchaseOrderRecord(
            po2Id,
            tenantId,
            "PO-20261027-4109",
            "VND-ADDIS-COFFEE",
            "Addis Bean Collective",
            "export@addisbean.org",
            "WH-CENTRAL",
            "Central Fulfillment Hub",
            PurchaseOrderStatus.Acknowledged,
            new List<PurchaseOrderLineItemRecord> { po2Line },
            TotalUnits: 300,
            SubtotalUsd: 4350.00m,
            TaxAmountUsd: 304.50m,
            ShippingFeeUsd: 150.00m,
            TotalAmountUsd: 4804.50m,
            Currency: "USD",
            EstimatedLeadTimeDays: 21,
            ExpectedDeliveryDateUtc: DateTime.UtcNow.AddDays(19),
            TrackingNumber: null,
            Notes: "Fresh harvest beans roast batch Q4.",
            CreatedBy: "Autonomous Inventory Engine",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-2),
            SubmittedAtUtc: DateTime.UtcNow.AddDays(-2),
            ReceivedAtUtc: null);

        return dict;
    }

    private static List<int> GenerateDemands(int mean, int variance)
    {
        var list = new List<int>(30);
        for (int i = 0; i < 30; i++)
        {
            var delta = Random.Shared.Next(-variance, variance + 1);
            list.Add(Math.Max(0, mean + delta));
        }
        return list;
    }

    private static InventoryItemDto MapToDto(Guid tenantId, InventoryItemRecord record)
    {
        var totalStock = record.WarehouseStocks.Values.Sum(ws => ws.CurrentStock);
        var totalAlloc = record.WarehouseStocks.Values.Sum(ws => ws.AllocatedStock);
        var totalAvail = Math.Max(0, totalStock - totalAlloc);

        var combinedVelocity = record.WarehouseStocks.Values.Sum(ws => ws.DailyVelocity);
        var combinedDoir = CalculateDoir(totalStock, combinedVelocity);
        var combinedSafety = record.WarehouseStocks.Values.Sum(ws => ws.SafetyStockUnits);
        var combinedRop = record.WarehouseStocks.Values.Sum(ws => ws.ReorderPointUnits);
        var combinedEoq = record.WarehouseStocks.Values.Sum(ws => ws.EoqUnits);
        var totalAssetVal = Math.Round(totalStock * record.UnitCostUsd, 2);

        var whList = record.WarehouseStocks.Values
            .Select(ws => new WarehouseStockDto(
                ws.WarehouseId,
                ws.WarehouseName,
                ws.LocationCode,
                ws.City,
                ws.CurrentStock,
                ws.AllocatedStock,
                ws.AvailableStock,
                Math.Round(ws.DailyVelocity, 2),
                ws.DoirDays,
                ws.SafetyStockUnits,
                ws.ReorderPointUnits,
                ws.EoqUnits,
                ws.HealthTier,
                ws.LastAuditedAtUtc))
            .ToList();

        return new InventoryItemDto(
            record.Sku,
            record.ProductName,
            record.Category,
            record.Barcode,
            record.UnitCostUsd,
            record.RetailPriceUsd,
            record.PrimaryVendorId,
            record.PrimaryVendorName,
            totalStock,
            totalAlloc,
            totalAvail,
            Math.Round(combinedVelocity, 2),
            combinedDoir,
            combinedSafety,
            combinedRop,
            combinedEoq,
            record.AggregatedHealthTier,
            totalAssetVal,
            record.Past30DaysDemandHistory,
            whList,
            record.LastReplenishedAtUtc);
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrderRecord p) =>
        new(
            p.Id,
            p.TenantId,
            p.PoNumber,
            p.VendorId,
            p.VendorName,
            p.VendorContactEmail,
            p.DestinationWarehouseId,
            p.DestinationWarehouseName,
            p.Status,
            p.Items.Select(i => new PurchaseOrderLineItemDto(
                i.Sku,
                i.ProductName,
                i.Quantity,
                i.UnitCostUsd,
                i.LineTotalUsd,
                i.CalculatedDeficit,
                i.AppliedMoq,
                i.AppliedBatchMultiplier)).ToList(),
            p.TotalUnits,
            p.SubtotalUsd,
            p.TaxAmountUsd,
            p.ShippingFeeUsd,
            p.TotalAmountUsd,
            p.Currency,
            p.EstimatedLeadTimeDays,
            p.ExpectedDeliveryDateUtc,
            p.TrackingNumber,
            p.Notes,
            p.CreatedBy,
            p.CreatedAtUtc,
            p.SubmittedAtUtc,
            p.ReceivedAtUtc);

    private static WarehouseTransferProposalDto MapToDto(WarehouseTransferProposalRecord r) =>
        new(
            r.ProposalId,
            r.TenantId,
            r.Sku,
            r.ProductName,
            r.SourceWarehouseId,
            r.SourceWarehouseName,
            r.SourceStockBefore,
            r.SourceRop,
            Math.Round(r.SourceDoirBefore, 1),
            r.DestinationWarehouseId,
            r.DestinationWarehouseName,
            r.DestinationStockBefore,
            r.DestinationRop,
            Math.Round(r.DestinationDoirBefore, 1),
            r.SuggestedQuantity,
            r.EstimatedTransitDays,
            r.EstimatedFreightCostUsd,
            r.Urgency,
            r.Reason,
            r.Status,
            r.CreatedAtUtc);

    #endregion

    #region Internal Entity Records

    internal class VendorRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string Country { get; set; }
        public string Currency { get; set; }
        public int StandardLeadTimeDays { get; set; }
        public double LeadTimeVarianceFactor { get; set; }
        public int Moq { get; set; }
        public int BatchMultiplier { get; set; }
        public decimal StandardOrderCostUsd { get; set; }
        public decimal DefaultTaxRatePercent { get; set; }

        public VendorRecord(
            string Id,
            string Name,
            string ContactPerson,
            string ContactEmail,
            string ContactPhone,
            string Country,
            string Currency,
            int StandardLeadTimeDays,
            double LeadTimeVarianceFactor,
            int Moq,
            int BatchMultiplier,
            decimal StandardOrderCostUsd,
            decimal DefaultTaxRatePercent)
        {
            this.Id = Id;
            this.Name = Name;
            this.ContactPerson = ContactPerson;
            this.ContactEmail = ContactEmail;
            this.ContactPhone = ContactPhone;
            this.Country = Country;
            this.Currency = Currency;
            this.StandardLeadTimeDays = StandardLeadTimeDays;
            this.LeadTimeVarianceFactor = LeadTimeVarianceFactor;
            this.Moq = Moq;
            this.BatchMultiplier = BatchMultiplier;
            this.StandardOrderCostUsd = StandardOrderCostUsd;
            this.DefaultTaxRatePercent = DefaultTaxRatePercent;
        }
    }

    internal class WarehouseRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LocationCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public int CapacityUnits { get; set; }

        public WarehouseRecord(string Id, string Name, string LocationCode, string City, string Country, int CapacityUnits)
        {
            this.Id = Id;
            this.Name = Name;
            this.LocationCode = LocationCode;
            this.City = City;
            this.Country = Country;
            this.CapacityUnits = CapacityUnits;
        }
    }

    internal class WarehouseStockRecord
    {
        public string WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public string LocationCode { get; set; }
        public string City { get; set; }
        public int CurrentStock { get; set; }
        public int AllocatedStock { get; set; }
        public int AvailableStock => Math.Max(0, CurrentStock - AllocatedStock);
        public double DailyVelocity { get; set; }
        public double DoirDays { get; set; }
        public int SafetyStockUnits { get; set; }
        public int ReorderPointUnits { get; set; }
        public int EoqUnits { get; set; }
        public string HealthTier { get; set; }
        public List<int> DemandHistory { get; set; }
        public DateTime LastAuditedAtUtc { get; set; }

        public WarehouseStockRecord(
            string WarehouseId,
            string WarehouseName,
            string LocationCode,
            string City,
            int CurrentStock,
            int AllocatedStock,
            int AvailableStock,
            double DailyVelocity,
            double DoirDays,
            int SafetyStockUnits,
            int ReorderPointUnits,
            int EoqUnits,
            string HealthTier,
            List<int> DemandHistory,
            DateTime LastAuditedAtUtc)
        {
            this.WarehouseId = WarehouseId;
            this.WarehouseName = WarehouseName;
            this.LocationCode = LocationCode;
            this.City = City;
            this.CurrentStock = CurrentStock;
            this.AllocatedStock = AllocatedStock;
            this.DailyVelocity = DailyVelocity;
            this.DoirDays = DoirDays;
            this.SafetyStockUnits = SafetyStockUnits;
            this.ReorderPointUnits = ReorderPointUnits;
            this.EoqUnits = EoqUnits;
            this.HealthTier = HealthTier;
            this.DemandHistory = DemandHistory;
            this.LastAuditedAtUtc = LastAuditedAtUtc;
        }
    }

    internal class InventoryItemRecord
    {
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string Category { get; set; }
        public string Barcode { get; set; }
        public decimal UnitCostUsd { get; set; }
        public decimal RetailPriceUsd { get; set; }
        public string PrimaryVendorId { get; set; }
        public string PrimaryVendorName { get; set; }
        public string AggregatedHealthTier { get; set; } = StockHealthTier.Optimal;
        public List<int> Past30DaysDemandHistory { get; set; }
        public ConcurrentDictionary<string, WarehouseStockRecord> WarehouseStocks { get; set; } = new();
        public DateTime LastReplenishedAtUtc { get; set; } = DateTime.UtcNow.AddDays(-15);

        public InventoryItemRecord(
            string Sku,
            string ProductName,
            string Category,
            string Barcode,
            decimal UnitCostUsd,
            decimal RetailPriceUsd,
            string PrimaryVendorId,
            string PrimaryVendorName,
            List<int> Past30DaysDemandHistory)
        {
            this.Sku = Sku;
            this.ProductName = ProductName;
            this.Category = Category;
            this.Barcode = Barcode;
            this.UnitCostUsd = UnitCostUsd;
            this.RetailPriceUsd = RetailPriceUsd;
            this.PrimaryVendorId = PrimaryVendorId;
            this.PrimaryVendorName = PrimaryVendorName;
            this.Past30DaysDemandHistory = Past30DaysDemandHistory;
        }
    }

    internal class PurchaseOrderLineItemRecord
    {
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCostUsd { get; set; }
        public decimal LineTotalUsd { get; set; }
        public int CalculatedDeficit { get; set; }
        public int AppliedMoq { get; set; }
        public int AppliedBatchMultiplier { get; set; }

        public PurchaseOrderLineItemRecord(
            string Sku,
            string ProductName,
            int Quantity,
            decimal UnitCostUsd,
            decimal LineTotalUsd,
            int CalculatedDeficit,
            int AppliedMoq,
            int AppliedBatchMultiplier)
        {
            this.Sku = Sku;
            this.ProductName = ProductName;
            this.Quantity = Quantity;
            this.UnitCostUsd = UnitCostUsd;
            this.LineTotalUsd = LineTotalUsd;
            this.CalculatedDeficit = CalculatedDeficit;
            this.AppliedMoq = AppliedMoq;
            this.AppliedBatchMultiplier = AppliedBatchMultiplier;
        }
    }

    internal class PurchaseOrderRecord
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string PoNumber { get; set; }
        public string VendorId { get; set; }
        public string VendorName { get; set; }
        public string VendorContactEmail { get; set; }
        public string DestinationWarehouseId { get; set; }
        public string DestinationWarehouseName { get; set; }
        public string Status { get; set; }
        public List<PurchaseOrderLineItemRecord> Items { get; set; }
        public int TotalUnits { get; set; }
        public decimal SubtotalUsd { get; set; }
        public decimal TaxAmountUsd { get; set; }
        public decimal ShippingFeeUsd { get; set; }
        public decimal TotalAmountUsd { get; set; }
        public string Currency { get; set; }
        public int EstimatedLeadTimeDays { get; set; }
        public DateTime? ExpectedDeliveryDateUtc { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Notes { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime? ReceivedAtUtc { get; set; }

        public PurchaseOrderRecord(
            Guid Id,
            Guid TenantId,
            string PoNumber,
            string VendorId,
            string VendorName,
            string VendorContactEmail,
            string DestinationWarehouseId,
            string DestinationWarehouseName,
            string Status,
            List<PurchaseOrderLineItemRecord> Items,
            int TotalUnits,
            decimal SubtotalUsd,
            decimal TaxAmountUsd,
            decimal ShippingFeeUsd,
            decimal TotalAmountUsd,
            string Currency,
            int EstimatedLeadTimeDays,
            DateTime? ExpectedDeliveryDateUtc,
            string? TrackingNumber,
            string? Notes,
            string CreatedBy,
            DateTime CreatedAtUtc,
            DateTime? SubmittedAtUtc,
            DateTime? ReceivedAtUtc)
        {
            this.Id = Id;
            this.TenantId = TenantId;
            this.PoNumber = PoNumber;
            this.VendorId = VendorId;
            this.VendorName = VendorName;
            this.VendorContactEmail = VendorContactEmail;
            this.DestinationWarehouseId = DestinationWarehouseId;
            this.DestinationWarehouseName = DestinationWarehouseName;
            this.Status = Status;
            this.Items = Items;
            this.TotalUnits = TotalUnits;
            this.SubtotalUsd = SubtotalUsd;
            this.TaxAmountUsd = TaxAmountUsd;
            this.ShippingFeeUsd = ShippingFeeUsd;
            this.TotalAmountUsd = TotalAmountUsd;
            this.Currency = Currency;
            this.EstimatedLeadTimeDays = EstimatedLeadTimeDays;
            this.ExpectedDeliveryDateUtc = ExpectedDeliveryDateUtc;
            this.TrackingNumber = TrackingNumber;
            this.Notes = Notes;
            this.CreatedBy = CreatedBy;
            this.CreatedAtUtc = CreatedAtUtc;
            this.SubmittedAtUtc = SubmittedAtUtc;
            this.ReceivedAtUtc = ReceivedAtUtc;
        }
    }

    internal class WarehouseTransferProposalRecord
    {
        public Guid ProposalId { get; set; }
        public Guid TenantId { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string SourceWarehouseId { get; set; }
        public string SourceWarehouseName { get; set; }
        public int SourceStockBefore { get; set; }
        public int SourceRop { get; set; }
        public double SourceDoirBefore { get; set; }
        public string DestinationWarehouseId { get; set; }
        public string DestinationWarehouseName { get; set; }
        public int DestinationStockBefore { get; set; }
        public int DestinationRop { get; set; }
        public double DestinationDoirBefore { get; set; }
        public int SuggestedQuantity { get; set; }
        public int EstimatedTransitDays { get; set; }
        public decimal EstimatedFreightCostUsd { get; set; }
        public string Urgency { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public WarehouseTransferProposalRecord(
            Guid ProposalId,
            Guid TenantId,
            string Sku,
            string ProductName,
            string SourceWarehouseId,
            string SourceWarehouseName,
            int SourceStockBefore,
            int SourceRop,
            double SourceDoirBefore,
            string DestinationWarehouseId,
            string DestinationWarehouseName,
            int DestinationStockBefore,
            int DestinationRop,
            double DestinationDoirBefore,
            int SuggestedQuantity,
            int EstimatedTransitDays,
            decimal EstimatedFreightCostUsd,
            string Urgency,
            string Reason,
            string Status,
            DateTime CreatedAtUtc)
        {
            this.ProposalId = ProposalId;
            this.TenantId = TenantId;
            this.Sku = Sku;
            this.ProductName = ProductName;
            this.SourceWarehouseId = SourceWarehouseId;
            this.SourceWarehouseName = SourceWarehouseName;
            this.SourceStockBefore = SourceStockBefore;
            this.SourceRop = SourceRop;
            this.SourceDoirBefore = SourceDoirBefore;
            this.DestinationWarehouseId = DestinationWarehouseId;
            this.DestinationWarehouseName = DestinationWarehouseName;
            this.DestinationStockBefore = DestinationStockBefore;
            this.DestinationRop = DestinationRop;
            this.DestinationDoirBefore = DestinationDoirBefore;
            this.SuggestedQuantity = SuggestedQuantity;
            this.EstimatedTransitDays = EstimatedTransitDays;
            this.EstimatedFreightCostUsd = EstimatedFreightCostUsd;
            this.Urgency = Urgency;
            this.Reason = Reason;
            this.Status = Status;
            this.CreatedAtUtc = CreatedAtUtc;
        }
    }

    #endregion
}

#endregion
