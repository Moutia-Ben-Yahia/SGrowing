using System.Collections.Concurrent;
using System.Text;

namespace SGrowing.Api.Modules.SupplyChain.EdiReplenishment;

public class EdiReplenishmentEngine
{
    private readonly ConcurrentDictionary<string, InventorySkuStock> _inventory = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SupplierProfile> _suppliers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, EdiPurchaseOrder> _orders = new(StringComparer.OrdinalIgnoreCase);

    public EdiReplenishmentEngine()
    {
        SeedData();
    }

    private void SeedData()
    {
        var supplier1 = new SupplierProfile(
            SupplierId: "sup_apex_logistics",
            SupplierName: "Apex Global Components",
            EdiPartnerId: "APEXGLOBAL01",
            ContactEmail: "edi-orders@apexcomponents.com",
            TypicalLeadTimeDays: 7,
            OnTimeDeliveryRate: 98.4,
            FillRatePercent: 99.1,
            MinOrderValue: 2500m
        );

        var supplier2 = new SupplierProfile(
            SupplierId: "sup_pacwest_mfg",
            SupplierName: "Pacific Western Assemblies",
            EdiPartnerId: "PACWESTMFG99",
            ContactEmail: "procurement@pacwestmfg.com",
            TypicalLeadTimeDays: 14,
            OnTimeDeliveryRate: 94.2,
            FillRatePercent: 96.5,
            MinOrderValue: 5000m
        );

        _suppliers[supplier1.SupplierId] = supplier1;
        _suppliers[supplier2.SupplierId] = supplier2;

        AddSku(new InventorySkuStock(
            SkuId: "SKU-OPTIC-400G",
            SkuName: "400G QSFP-DD Transceiver Module",
            WarehouseId: "wh_frankfurt_eu",
            OnHandQty: 85,
            AllocatedQty: 45,
            DailyVelocity: 12.5,
            LeadTimeDays: 7,
            SafetyStock: 50,
            ReorderPoint: 138, // (12.5 * 7) + 50 = 137.5 -> 138
            EconomicOrderQty: 250,
            UnitCost: 145.00m,
            StockStatus: "Critical"
        ));

        AddSku(new InventorySkuStock(
            SkuId: "SKU-RACK-PDU",
            SkuName: "Smart Switched 32A Zero-U PDU",
            WarehouseId: "wh_frankfurt_eu",
            OnHandQty: 210,
            AllocatedQty: 30,
            DailyVelocity: 8.0,
            LeadTimeDays: 14,
            SafetyStock: 60,
            ReorderPoint: 172,
            EconomicOrderQty: 180,
            UnitCost: 280.00m,
            StockStatus: "Optimal"
        ));

        AddSku(new InventorySkuStock(
            SkuId: "SKU-FIBER-LC10M",
            SkuName: "OM4 Duplex LC-LC Patch Cable 10m",
            WarehouseId: "wh_chicago_us",
            OnHandQty: 120,
            AllocatedQty: 40,
            DailyVelocity: 15.0,
            LeadTimeDays: 7,
            SafetyStock: 45,
            ReorderPoint: 150,
            EconomicOrderQty: 500,
            UnitCost: 18.50m,
            StockStatus: "Warning"
        ));
    }

    private void AddSku(InventorySkuStock sku)
    {
        _inventory[sku.SkuId] = sku;
    }

    public Task<IReadOnlyList<ReplenishmentRecommendation>> GetReplenishmentRecommendationsAsync(string? warehouseId = null, CancellationToken ct = default)
    {
        var recommendations = new List<ReplenishmentRecommendation>();

        foreach (var sku in _inventory.Values)
        {
            if (!string.IsNullOrEmpty(warehouseId) && !sku.WarehouseId.Equals(warehouseId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var available = sku.OnHandQty - sku.AllocatedQty;
            if (available <= sku.ReorderPoint)
            {
                var severity = available <= sku.SafetyStock ? "Critical" : "Warning";
                var daysUntilStockout = sku.DailyVelocity > 0 ? (int)Math.Max(0, available / sku.DailyVelocity) : 99;
                var estimatedCost = sku.EconomicOrderQty * sku.UnitCost;

                var supplierId = _suppliers.Keys.FirstOrDefault() ?? "sup_apex_logistics";

                recommendations.Add(new ReplenishmentRecommendation(
                    SkuId: sku.SkuId,
                    SkuName: sku.SkuName,
                    WarehouseId: sku.WarehouseId,
                    CurrentAvailable: available,
                    ReorderPoint: sku.ReorderPoint,
                    SuggestedOrderQty: sku.EconomicOrderQty,
                    EstimatedCost: estimatedCost,
                    RecommendedSupplierId: supplierId,
                    Severity: severity,
                    EstimatedDaysUntilStockout: daysUntilStockout
                ));
            }
        }

        return Task.FromResult<IReadOnlyList<ReplenishmentRecommendation>>(recommendations.OrderBy(r => r.EstimatedDaysUntilStockout).ToList());
    }

    public Task<EdiPurchaseOrder> GeneratePurchaseOrderAsync(GeneratePoRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_suppliers.TryGetValue(request.SupplierId, out var supplier))
        {
            throw new KeyNotFoundException($"Supplier '{request.SupplierId}' not found.");
        }

        var poNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var lineItems = new List<EdiLineItem>();
        decimal totalAmount = 0m;
        var lineNumber = 1;

        foreach (var skuId in request.SkuIds)
        {
            if (_inventory.TryGetValue(skuId, out var sku))
            {
                var qty = sku.EconomicOrderQty;
                var extended = qty * sku.UnitCost;
                totalAmount += extended;

                lineItems.Add(new EdiLineItem(
                    LineNumber: lineNumber++,
                    SkuId: sku.SkuId,
                    Description: sku.SkuName,
                    Quantity: qty,
                    UnitPrice: sku.UnitCost,
                    ExtendedAmount: extended
                ));
            }
        }

        if (lineItems.Count == 0)
        {
            throw new ArgumentException("No valid SKUs provided to generate Purchase Order.", nameof(request));
        }

        var edi850Payload = BuildAnsiX12Edi850(poNumber, supplier, request.WarehouseId, lineItems, totalAmount);

        var po = new EdiPurchaseOrder(
            PoNumber: poNumber,
            SupplierId: supplier.SupplierId,
            WarehouseId: request.WarehouseId,
            LineItems: lineItems,
            TotalAmount: totalAmount,
            Edi850RawPayload: edi850Payload,
            Status: "Staged",
            CreatedAtUtc: DateTime.UtcNow
        );

        _orders[po.PoNumber] = po;
        return Task.FromResult(po);
    }

    public Task<EdiPurchaseOrder> DispatchPurchaseOrderAsync(string poNumber, CancellationToken ct = default)
    {
        if (!_orders.TryGetValue(poNumber, out var po))
        {
            throw new KeyNotFoundException($"Purchase order '{poNumber}' not found.");
        }

        var dispatched = po with
        {
            Status = "Dispatched",
            DispatchedAtUtc = DateTime.UtcNow
        };

        _orders[po.PoNumber] = dispatched;
        return Task.FromResult(dispatched);
    }

    public Task<IReadOnlyList<SupplierScorecard>> GetSupplierScorecardsAsync(CancellationToken ct = default)
    {
        var scorecards = _suppliers.Values.Select(s => new SupplierScorecard(
            SupplierId: s.SupplierId,
            SupplierName: s.SupplierName,
            TotalOrdersFulfilled: 142,
            OnTimeDeliveryPercent: s.OnTimeDeliveryRate,
            FillRatePercent: s.FillRatePercent,
            QualityScoreOutOf100: Math.Round((s.OnTimeDeliveryRate * 0.5) + (s.FillRatePercent * 0.5), 1),
            ReliabilityTier: s.OnTimeDeliveryRate >= 98.0 ? "Tier-1 Strategic" : "Tier-2 Verified"
        )).ToList();

        return Task.FromResult<IReadOnlyList<SupplierScorecard>>(scorecards);
    }

    private static string BuildAnsiX12Edi850(
        string poNumber,
        SupplierProfile supplier,
        string warehouseId,
        IReadOnlyList<EdiLineItem> lineItems,
        decimal totalAmount)
    {
        var sb = new StringBuilder();
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");
        var timeStr = DateTime.UtcNow.ToString("HHmm");

        sb.AppendLine($"ISA*00*          *00*          *ZZ*GROWTHOS       *ZZ*{supplier.EdiPartnerId,-15}*{dateStr}*{timeStr}*U*00401*000000001*0*P*>~");
        sb.AppendLine($"GS*PO*GROWTHOS*{supplier.EdiPartnerId}*{DateTime.UtcNow:yyyyMMdd}*{timeStr}*1*X*004010~");
        sb.AppendLine("ST*850*0001~");
        sb.AppendLine($"BEG*00*NE*{poNumber}**{DateTime.UtcNow:yyyyMMdd}~");
        sb.AppendLine($"N1*ST*{warehouseId}*92*{warehouseId}~");

        foreach (var item in lineItems)
        {
            sb.AppendLine($"PO1*{item.LineNumber}*{item.Quantity}*EA*{item.UnitPrice:F2}**VC*{item.SkuId}~");
            sb.AppendLine($"PID*F****{item.Description}~");
        }

        sb.AppendLine($"CTT*{lineItems.Count}*{lineItems.Sum(i => i.Quantity)}~");
        sb.AppendLine($"AMT*TT*{totalAmount:F2}~");
        sb.AppendLine($"SE*{6 + (lineItems.Count * 2)}*0001~");
        sb.AppendLine("GE*1*1~");
        sb.AppendLine("IEA*1*000000001~");

        return sb.ToString();
    }
}
