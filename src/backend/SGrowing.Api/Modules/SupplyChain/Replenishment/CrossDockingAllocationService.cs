using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.SupplyChain.Replenishment;

/// <summary>
/// Service contract for JIT cross-docking: directly allocating inbound receipts to outbound customer backorders
/// without putaway bin storage, and generating carrier staging manifests.
/// </summary>
public interface ICrossDockingAllocationService
{
    /// <summary>
    /// Registers or updates pending customer backorders in the waiting fulfillment queue.
    /// </summary>
    void EnqueueBackorders(IEnumerable<CustomerBackorderOrder> orders);

    /// <summary>
    /// Retrieves all active pending backorders, optionally filtered by SKU.
    /// </summary>
    IReadOnlyList<CustomerBackorderOrder> GetPendingBackorders(string? sku = null);

    /// <summary>
    /// Ingests an inbound shipment receipt and matches it against waiting customer backorders
    /// by priority and FIFO order, directly routing items to outbound staging dock bays.
    /// </summary>
    CrossDockAllocateResponse AllocateInboundReceipt(
        InboundReceipt receipt,
        IReadOnlyList<CustomerBackorderOrder>? overridePendingOrders = null);

    /// <summary>
    /// Retrieves all generated cross-dock staging manifests.
    /// </summary>
    IReadOnlyList<CrossDockStagingManifest> GetStagingManifests();

    /// <summary>
    /// Retrieves a specific staging manifest by ID.
    /// </summary>
    CrossDockStagingManifest? GetStagingManifest(Guid manifestId);

    /// <summary>
    /// Retrieves all cross-dock allocation records for a specific inbound purchase order.
    /// </summary>
    IReadOnlyList<CrossDockAllocationRecord> GetAllocationsByInboundPo(Guid inboundPoId);

    /// <summary>
    /// Updates the physical staging status of an allocation (e.g., Staged -> Loaded).
    /// </summary>
    CrossDockAllocationRecord? UpdateAllocationStatus(Guid allocationId, CrossDockStatus newStatus);
}

/// <summary>
/// Autonomous JIT cross-docking allocation engine that maps dock-to-dock flows, enforces VIP & FIFO queues,
/// eliminates warehouse storage putaways, and generates courier staging manifests.
/// </summary>
public class CrossDockingAllocationService : ICrossDockingAllocationService
{
    private readonly ConcurrentDictionary<Guid, CustomerBackorderOrder> _backorderQueue = new();
    private readonly ConcurrentDictionary<Guid, CrossDockAllocationRecord> _allocations = new();
    private readonly ConcurrentDictionary<Guid, CrossDockStagingManifest> _manifests = new();

    /// <inheritdoc />
    public void EnqueueBackorders(IEnumerable<CustomerBackorderOrder> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        foreach (var order in orders)
        {
            _backorderQueue[order.OrderId] = order;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CustomerBackorderOrder> GetPendingBackorders(string? sku = null)
    {
        var query = _backorderQueue.Values.Where(o => o.BackorderedQuantity > 0m);

        if (!string.IsNullOrWhiteSpace(sku))
        {
            string normalizedSku = sku.Trim().ToUpperInvariant();
            query = query.Where(o => o.Sku.Trim().Equals(normalizedSku, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(o => o.PriorityLevel)
            .ThenBy(o => o.OrderDateUtc)
            .ToList();
    }

    /// <inheritdoc />
    public CrossDockAllocateResponse AllocateInboundReceipt(
        InboundReceipt receipt,
        IReadOnlyList<CustomerBackorderOrder>? overridePendingOrders = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (receipt.ReceivedQuantity <= 0m)
        {
            return new CrossDockAllocateResponse(
                Receipt: receipt,
                TotalReceived: 0m,
                TotalAllocated: 0m,
                RemainingToPutaway: 0m,
                Allocations: [],
                StagingManifests: []);
        }

        // 1. Determine candidate backorders for this SKU
        IReadOnlyList<CustomerBackorderOrder> candidateOrders;
        if (overridePendingOrders != null && overridePendingOrders.Count > 0)
        {
            candidateOrders = overridePendingOrders
                .Where(o => o.Sku.Trim().Equals(receipt.Sku.Trim(), StringComparison.OrdinalIgnoreCase) && o.BackorderedQuantity > 0m)
                .OrderBy(o => o.PriorityLevel)
                .ThenBy(o => o.OrderDateUtc)
                .ToList();

            // Also register in queue
            EnqueueBackorders(overridePendingOrders);
        }
        else
        {
            candidateOrders = GetPendingBackorders(receipt.Sku);
        }

        decimal remainingReceipt = receipt.ReceivedQuantity;
        var createdAllocations = new List<CrossDockAllocationRecord>();

        // 2. Direct Inbound-to-Outbound matching: Priority 1 first, then FIFO
        foreach (var order in candidateOrders)
        {
            if (remainingReceipt <= 0m)
            {
                break;
            }

            decimal allocQuantity = Math.Min(remainingReceipt, order.BackorderedQuantity);
            if (allocQuantity <= 0m)
            {
                continue;
            }

            remainingReceipt -= allocQuantity;

            // Outbound Dock Bay assignment based on carrier
            string carrierKey = string.IsNullOrWhiteSpace(order.TargetCarrier) ? "STANDARD" : order.TargetCarrier.Trim().ToUpperInvariant();
            string dockBay = AssignDockBayForCarrier(carrierKey);
            string waybill = $"WB-XDOCK-{order.OrderId.ToString("N")[..6].ToUpperInvariant()}-{DateTime.UtcNow:HHmmss}";

            var allocation = new CrossDockAllocationRecord(
                AllocationId: Guid.NewGuid(),
                InboundPoId: receipt.InboundPoId,
                OutboundOrderId: order.OrderId,
                Sku: receipt.Sku,
                AllocatedQuantity: allocQuantity,
                DockBay: dockBay,
                TargetCarrier: order.TargetCarrier,
                AllocatedAtUtc: DateTime.UtcNow,
                Status: CrossDockStatus.Staged,
                WaybillNumber: waybill,
                CustomerName: order.CustomerName);

            _allocations[allocation.AllocationId] = allocation;
            createdAllocations.Add(allocation);

            // Update remaining backorder amount in persistent queue
            decimal updatedBackorder = order.BackorderedQuantity - allocQuantity;
            if (updatedBackorder <= 0m)
            {
                _backorderQueue.TryRemove(order.OrderId, out _);
            }
            else
            {
                _backorderQueue[order.OrderId] = order with { BackorderedQuantity = updatedBackorder };
            }
        }

        decimal totalAllocated = receipt.ReceivedQuantity - remainingReceipt;
        decimal remainingToPutaway = remainingReceipt;

        // 3. Generate carrier-grouped staging manifests
        var manifestList = new List<CrossDockStagingManifest>();
        var groupedByBayAndCarrier = createdAllocations
            .GroupBy(a => (a.DockBay, a.TargetCarrier));

        foreach (var group in groupedByBayAndCarrier)
        {
            var groupAllocations = group.ToList();
            decimal groupUnits = groupAllocations.Sum(a => a.AllocatedQuantity);
            string carrierName = group.Key.TargetCarrier;
            string carrierCode = string.IsNullOrWhiteSpace(carrierName) ? "GEN" : carrierName.Replace(" ", "").ToUpperInvariant();
            string manifestRef = $"MANIFEST-{carrierCode}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

            var manifest = new CrossDockStagingManifest(
                ManifestId: Guid.NewGuid(),
                DockBay: group.Key.DockBay,
                TargetCarrier: group.Key.TargetCarrier,
                Allocations: groupAllocations,
                TotalAllocatedUnits: groupUnits,
                CourierWaybillReference: manifestRef,
                GeneratedAtUtc: DateTime.UtcNow);

            _manifests[manifest.ManifestId] = manifest;
            manifestList.Add(manifest);
        }

        return new CrossDockAllocateResponse(
            Receipt: receipt,
            TotalReceived: receipt.ReceivedQuantity,
            TotalAllocated: totalAllocated,
            RemainingToPutaway: remainingToPutaway,
            Allocations: createdAllocations,
            StagingManifests: manifestList);
    }

    /// <inheritdoc />
    public IReadOnlyList<CrossDockStagingManifest> GetStagingManifests()
    {
        return _manifests.Values
            .OrderByDescending(m => m.GeneratedAtUtc)
            .ToList();
    }

    /// <inheritdoc />
    public CrossDockStagingManifest? GetStagingManifest(Guid manifestId)
    {
        _manifests.TryGetValue(manifestId, out var manifest);
        return manifest;
    }

    /// <inheritdoc />
    public IReadOnlyList<CrossDockAllocationRecord> GetAllocationsByInboundPo(Guid inboundPoId)
    {
        return _allocations.Values
            .Where(a => a.InboundPoId == inboundPoId)
            .OrderBy(a => a.AllocatedAtUtc)
            .ToList();
    }

    /// <inheritdoc />
    public CrossDockAllocationRecord? UpdateAllocationStatus(Guid allocationId, CrossDockStatus newStatus)
    {
        if (_allocations.TryGetValue(allocationId, out var existing))
        {
            var updated = existing with { Status = newStatus };
            _allocations[allocationId] = updated;
            return updated;
        }

        return null;
    }

    private static string AssignDockBayForCarrier(string carrierKey)
    {
        return carrierKey switch
        {
            "YALIDINE" => "BAY-OUT-YALIDINE-01",
            "ZR_EXPRESS" or "ZREXPRESS" => "BAY-OUT-ZR-02",
            "DHL" or "DHL_EXPRESS" => "BAY-OUT-DHL-03",
            "FEDEX" => "BAY-OUT-FEDEX-04",
            "KAZI_TOUR" => "BAY-OUT-KAZI-05",
            _ => $"BAY-OUT-{carrierKey[..Math.Min(6, carrierKey.Length)]}-09"
        };
    }
}
