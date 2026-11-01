using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.SupplyChain.Replenishment;

/// <summary>
/// Service contract for warehouse inventory evaluation, PO proposal sizing, and EDI/JSON PO transmission.
/// </summary>
public interface ISupplierAutoReplenishmentService
{
    /// <summary>
    /// Registers or updates an SKU's velocity and replenishment parameters in the warehouse catalogue.
    /// </summary>
    void RegisterSkuVelocity(SkuVelocityRecord record);

    /// <summary>
    /// Retrieves all registered SKU velocity records for a specific warehouse.
    /// </summary>
    IReadOnlyList<SkuVelocityRecord> GetWarehouseSkus(Guid warehouseId);

    /// <summary>
    /// Evaluates warehouse stock positions against ROP and generates PO proposals for SKUs where (Current + OnOrder) &lt;= ROP.
    /// Proposal size is computed as max(EOQ, deficit).
    /// </summary>
    Task<EvaluateStockResponse> EvaluateWarehouseStockAsync(
        Guid warehouseId,
        IReadOnlyList<SkuVelocityRecord>? skuRecords = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dispatches a purchase order proposal to the supplier, transitioning status to Dispatched
    /// and generating an ANSI X12 850 compliant EDI/JSON order transmission payload.
    /// </summary>
    Task<DispatchPoResponse> DispatchPurchaseOrderAsync(
        Guid proposalId,
        CancellationToken ct = default);

    /// <summary>
    /// Acknowledges a dispatched purchase order, updating delivery estimate and on-order stock.
    /// </summary>
    Task<AcknowledgePoResponse> AcknowledgePurchaseOrderAsync(
        Guid proposalId,
        DateTime estimatedDeliveryDate,
        string? supplierNote = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a purchase order proposal by ID.
    /// </summary>
    PurchaseOrderProposal? GetProposal(Guid proposalId);

    /// <summary>
    /// Retrieves all proposals for a warehouse.
    /// </summary>
    IReadOnlyList<PurchaseOrderProposal> GetProposalsForWarehouse(Guid warehouseId);
}

/// <summary>
/// Autonomous auto-replenishment service handling continuous inventory scanning, deficit calculations,
/// PO proposal generation, and ANSI X12 850 EDI transmission.
/// </summary>
public class SupplierAutoReplenishmentService : ISupplierAutoReplenishmentService
{
    private readonly ConcurrentDictionary<string, SkuVelocityRecord> _skuCatalog = new();
    private readonly ConcurrentDictionary<Guid, PurchaseOrderProposal> _proposals = new();
    private readonly ISupplyDemandForecastingEngine _forecastingEngine;

    public SupplierAutoReplenishmentService(ISupplyDemandForecastingEngine forecastingEngine)
    {
        _forecastingEngine = forecastingEngine ?? throw new ArgumentNullException(nameof(forecastingEngine));
    }

    private static string GetCatalogKey(Guid warehouseId, string sku) => $"{warehouseId:N}:{sku.Trim().ToUpperInvariant()}";

    /// <inheritdoc />
    public void RegisterSkuVelocity(SkuVelocityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        string key = GetCatalogKey(record.WarehouseId, record.Sku);
        _skuCatalog[key] = record;
    }

    /// <inheritdoc />
    public IReadOnlyList<SkuVelocityRecord> GetWarehouseSkus(Guid warehouseId)
    {
        return _skuCatalog.Values
            .Where(r => r.WarehouseId == warehouseId)
            .ToList();
    }

    /// <inheritdoc />
    public Task<EvaluateStockResponse> EvaluateWarehouseStockAsync(
        Guid warehouseId,
        IReadOnlyList<SkuVelocityRecord>? skuRecords = null,
        CancellationToken ct = default)
    {
        // 1. Determine which records to evaluate
        IReadOnlyList<SkuVelocityRecord> recordsToEvaluate;
        if (skuRecords != null && skuRecords.Count > 0)
        {
            recordsToEvaluate = skuRecords;
            foreach (var rec in skuRecords)
            {
                RegisterSkuVelocity(rec);
            }
        }
        else
        {
            recordsToEvaluate = GetWarehouseSkus(warehouseId);
        }

        var generatedProposals = new List<PurchaseOrderProposal>();

        // 2. Scan every SKU
        foreach (var record in recordsToEvaluate)
        {
            ct.ThrowIfCancellationRequested();

            decimal inventoryPosition = record.CurrentStock + record.OnOrderStock;

            // Trigger condition: (CurrentStock + OnOrderStock) <= ROP
            if (inventoryPosition <= record.ReorderPoint)
            {
                // Calculate stock deficit: shortfall to restore inventory above ROP + SafetyStock
                decimal targetStock = record.ReorderPoint + record.SafetyStock;
                decimal deficit = targetStock - inventoryPosition;
                if (deficit <= 0m)
                {
                    deficit = record.ReorderPoint - inventoryPosition + 1m;
                }

                // Proposal order quantity = max(EOQ, deficit)
                decimal orderQuantity = Math.Max(record.EconomicOrderQuantity, deficit);
                if (orderQuantity <= 0m)
                {
                    orderQuantity = 1m;
                }

                orderQuantity = Math.Ceiling(orderQuantity);

                // Priority determination
                PoPriority priority;
                string reason;

                if (record.CurrentStock <= 0m)
                {
                    priority = PoPriority.Urgent;
                    reason = $"Stockout breach! Current stock is {record.CurrentStock}. Position ({inventoryPosition}) <= ROP ({record.ReorderPoint}). Critical replenishment required.";
                }
                else if (record.CurrentStock <= record.SafetyStock)
                {
                    priority = PoPriority.Urgent;
                    reason = $"Safety stock breached! Current stock ({record.CurrentStock}) <= SafetyStock ({record.SafetyStock}). Position ({inventoryPosition}) <= ROP ({record.ReorderPoint}).";
                }
                else
                {
                    priority = PoPriority.Normal;
                    reason = $"Automated replenishment triggered. Inventory position ({inventoryPosition}) <= ROP ({record.ReorderPoint}). Sized to max(EOQ={record.EconomicOrderQuantity}, Deficit={deficit}).";
                }

                decimal unitCost = record.UnitCost > 0m ? record.UnitCost : 50m;
                decimal totalCost = Math.Round(orderQuantity * unitCost, 2);
                Guid supplierId = record.SupplierId ?? Guid.NewGuid();

                var proposal = new PurchaseOrderProposal(
                    ProposalId: Guid.NewGuid(),
                    SupplierId: supplierId,
                    Sku: record.Sku,
                    Quantity: orderQuantity,
                    UnitCost: unitCost,
                    TotalEstimatedCost: totalCost,
                    Priority: priority,
                    Reason: reason,
                    Status: PoStatus.Proposed,
                    CreatedAtUtc: DateTime.UtcNow,
                    WarehouseId: warehouseId);

                _proposals[proposal.ProposalId] = proposal;
                generatedProposals.Add(proposal);

                // Update catalog record evaluation timestamp
                string key = GetCatalogKey(record.WarehouseId, record.Sku);
                _skuCatalog[key] = record with { LastEvaluatedAtUtc = DateTime.UtcNow };
            }
        }

        var response = new EvaluateStockResponse(
            WarehouseId: warehouseId,
            TotalSkusEvaluated: recordsToEvaluate.Count,
            ReplenishmentTriggeredCount: generatedProposals.Count,
            GeneratedProposals: generatedProposals,
            EvaluatedAtUtc: DateTime.UtcNow);

        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public Task<DispatchPoResponse> DispatchPurchaseOrderAsync(
        Guid proposalId,
        CancellationToken ct = default)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
        {
            throw new KeyNotFoundException($"Purchase order proposal '{proposalId}' was not found.");
        }

        if (proposal.Status != PoStatus.Proposed)
        {
            return Task.FromResult(new DispatchPoResponse(
                Proposal: proposal,
                TransmissionPayload: proposal.TransmissionPayload ?? string.Empty,
                Success: false,
                Message: $"Cannot dispatch proposal '{proposalId}' because its status is '{proposal.Status}'. Only 'Proposed' orders can be dispatched."));
        }

        // Generate structured ANSI X12 850 EDI/JSON payload
        string ediPayload = GenerateEdi850Payload(proposal);

        var updatedProposal = proposal with
        {
            Status = PoStatus.Dispatched,
            DispatchedAtUtc = DateTime.UtcNow,
            TransmissionPayload = ediPayload
        };

        _proposals[proposalId] = updatedProposal;

        return Task.FromResult(new DispatchPoResponse(
            Proposal: updatedProposal,
            TransmissionPayload: ediPayload,
            Success: true,
            Message: $"Purchase order '{proposalId}' for SKU '{proposal.Sku}' successfully dispatched to supplier '{proposal.SupplierId}'."));
    }

    /// <inheritdoc />
    public Task<AcknowledgePoResponse> AcknowledgePurchaseOrderAsync(
        Guid proposalId,
        DateTime estimatedDeliveryDate,
        string? supplierNote = null,
        CancellationToken ct = default)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
        {
            throw new KeyNotFoundException($"Purchase order proposal '{proposalId}' was not found.");
        }

        if (proposal.Status != PoStatus.Dispatched)
        {
            return Task.FromResult(new AcknowledgePoResponse(
                Proposal: proposal,
                Success: false,
                Message: $"Cannot acknowledge proposal '{proposalId}' because its status is '{proposal.Status}'. Expected 'Dispatched'."));
        }

        var acknowledgedProposal = proposal with
        {
            Status = PoStatus.Acknowledged,
            AcknowledgedAtUtc = DateTime.UtcNow,
            EstimatedDeliveryDate = estimatedDeliveryDate
        };

        _proposals[proposalId] = acknowledgedProposal;

        // Automatically update SKU OnOrderStock in the warehouse catalog
        string catalogKey = GetCatalogKey(proposal.WarehouseId, proposal.Sku);
        if (_skuCatalog.TryGetValue(catalogKey, out var existingRecord))
        {
            _skuCatalog[catalogKey] = existingRecord with
            {
                OnOrderStock = existingRecord.OnOrderStock + proposal.Quantity,
                LastEvaluatedAtUtc = DateTime.UtcNow
            };
        }

        return Task.FromResult(new AcknowledgePoResponse(
            Proposal: acknowledgedProposal,
            Success: true,
            Message: $"Purchase order '{proposalId}' acknowledged by supplier. Estimated delivery: {estimatedDeliveryDate:yyyy-MM-dd}. Added {proposal.Quantity} units to on-order stock."));
    }

    /// <inheritdoc />
    public PurchaseOrderProposal? GetProposal(Guid proposalId)
    {
        _proposals.TryGetValue(proposalId, out var proposal);
        return proposal;
    }

    /// <inheritdoc />
    public IReadOnlyList<PurchaseOrderProposal> GetProposalsForWarehouse(Guid warehouseId)
    {
        return _proposals.Values
            .Where(p => p.WarehouseId == warehouseId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();
    }

    private static string GenerateEdi850Payload(PurchaseOrderProposal proposal)
    {
        string icn = $"ICN-{DateTime.UtcNow:yyyyMMdd}-{proposal.ProposalId.ToString("N")[..8].ToUpperInvariant()}";
        string poNumber = $"PO-{proposal.WarehouseId.ToString("N")[..6].ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMdd}-{proposal.Sku}";

        var payloadObj = new
        {
            EdiStandard = "ANSI-X12-850",
            InterchangeControlNumber = icn,
            TransactionSetId = "850",
            TransactionSetPurpose = "00-Original",
            PurchaseOrderNumber = poNumber,
            WarehouseId = proposal.WarehouseId,
            BuyerParty = "SGrowing Autonomous Fulfillment Central",
            SupplierId = proposal.SupplierId,
            OrderDate = proposal.CreatedAtUtc.ToString("o"),
            DispatchedDate = DateTime.UtcNow.ToString("o"),
            Currency = "DZD",
            Priority = proposal.Priority.ToString(),
            LineItems = new[]
            {
                new
                {
                    LineNumber = 1,
                    Sku = proposal.Sku,
                    QuantityOrdered = proposal.Quantity,
                    UnitPrice = proposal.UnitCost,
                    ExtendedPrice = proposal.TotalEstimatedCost,
                    Uom = "EA"
                }
            },
            TotalAmount = proposal.TotalEstimatedCost,
            ChecksumSha256 = ComputeChecksum($"{icn}:{poNumber}:{proposal.Quantity}:{proposal.TotalEstimatedCost}")
        };

        return JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ComputeChecksum(string raw)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(bytes);
    }
}
