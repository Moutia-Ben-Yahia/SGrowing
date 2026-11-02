namespace SGrowing.Api.Modules.Finance.AccountsPayableThreeWayMatching;

public interface IApThreeWayMatchEngine
{
    ThreeWayMatchResult ReconcileInvoice(ThreeWayMatchRequest request);
}

public class ApThreeWayMatchEngine : IApThreeWayMatchEngine
{
    public ThreeWayMatchResult ReconcileInvoice(ThreeWayMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var poMap = request.PoLines.ToDictionary(p => p.ItemSku, StringComparer.OrdinalIgnoreCase);
        var grnMap = request.GrnLines.GroupBy(g => g.ItemSku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var discrepancies = new List<MatchDiscrepancy>();
        decimal totalPo = request.PoLines.Sum(p => (decimal)p.OrderedQuantity * p.UnitPrice);
        decimal totalInvoice = request.InvoiceLines.Sum(i => i.LineTotal);
        decimal totalGrn = 0.0m;

        foreach (var invLine in request.InvoiceLines)
        {
            if (!poMap.TryGetValue(invLine.ItemSku, out var poLine))
            {
                discrepancies.Add(new MatchDiscrepancy(
                    ItemSku: invLine.ItemSku,
                    DiscrepancyType: "UnmatchedPoItem",
                    VarianceAmount: invLine.LineTotal,
                    VariancePercentage: 100.0,
                    Description: $"Invoiced item {invLine.ItemSku} does not exist on Purchase Order {request.PurchaseOrderNumber}.",
                    IsAutoResolvable: false
                ));
                continue;
            }

            // Check GRN
            bool hasGrn = grnMap.TryGetValue(invLine.ItemSku, out var grnEntries);
            double acceptedQty = hasGrn ? grnEntries!.Sum(g => g.AcceptedQuantity) : 0.0;
            totalGrn += (decimal)acceptedQty * poLine.UnitPrice;

            // 1. Quantity Check: Invoiced Qty vs Accepted Goods Receipt Qty (when GRN exists)
            if (hasGrn && Math.Abs(invLine.InvoicedQuantity - acceptedQty) > 1e-4)
            {
                if (invLine.InvoicedQuantity > acceptedQty)
                {
                    var overBilledUnits = (decimal)(invLine.InvoicedQuantity - acceptedQty);
                    var overBilledAmount = overBilledUnits * invLine.InvoicedUnitPrice;
                    var qtyVarPct = acceptedQty > 0 ? ((invLine.InvoicedQuantity - acceptedQty) / acceptedQty) * 100.0 : 100.0;

                    discrepancies.Add(new MatchDiscrepancy(
                        ItemSku: invLine.ItemSku,
                        DiscrepancyType: "OverBilledQuantity",
                        VarianceAmount: overBilledAmount,
                        VariancePercentage: Math.Round(qtyVarPct, 2),
                        Description: $"Invoiced for {invLine.InvoicedQuantity} units but Goods Receipt accepted only {acceptedQty} units.",
                        IsAutoResolvable: false
                    ));
                }
            }

            // 2. Unit Price Check: Invoiced Unit Price vs PO Contracted Unit Price
            var priceVariancePerUnit = invLine.InvoicedUnitPrice - poLine.UnitPrice;
            if (priceVariancePerUnit != 0.0m)
            {
                var linePriceVariance = priceVariancePerUnit * (decimal)invLine.InvoicedQuantity;
                var pctVariance = poLine.UnitPrice > 0.0m
                    ? (double)(priceVariancePerUnit / poLine.UnitPrice) * 100.0
                    : 100.0;

                var isWithinTolerance = Math.Abs((decimal)pctVariance) <= poLine.TolerancePercentage
                    || Math.Abs(linePriceVariance) <= poLine.MaxPriceToleranceAmount;

                if (!isWithinTolerance || priceVariancePerUnit > 0.0m)
                {
                    discrepancies.Add(new MatchDiscrepancy(
                        ItemSku: invLine.ItemSku,
                        DiscrepancyType: "PriceVariance",
                        VarianceAmount: linePriceVariance,
                        VariancePercentage: Math.Round(pctVariance, 2),
                        Description: $"Invoiced unit price {invLine.InvoicedUnitPrice:C} deviates from PO contracted price {poLine.UnitPrice:C} ({pctVariance:+0.0;-0.0}%).",
                        IsAutoResolvable: isWithinTolerance
                    ));
                }
            }
        }

        // Check for Missing GRNs (Ordered on PO, Invoiced, but 0 Goods Receipt)
        foreach (var invLine in request.InvoiceLines)
        {
            if (poMap.ContainsKey(invLine.ItemSku) && (!grnMap.ContainsKey(invLine.ItemSku) || grnMap[invLine.ItemSku].Sum(g => g.ReceivedQuantity) <= 0.0))
            {
                discrepancies.Add(new MatchDiscrepancy(
                    ItemSku: invLine.ItemSku,
                    DiscrepancyType: "MissingGoodsReceipt",
                    VarianceAmount: invLine.LineTotal,
                    VariancePercentage: 100.0,
                    Description: $"No Goods Receipt Note recorded for SKU {invLine.ItemSku}.",
                    IsAutoResolvable: false
                ));
            }
        }

        decimal totalVariance = Math.Abs(totalInvoice - totalPo);
        string matchStatus;
        string recommendedAction;
        bool isAutoApproved;

        if (discrepancies.Count == 0 && totalVariance == 0.0m)
        {
            matchStatus = "PerfectMatch";
            recommendedAction = "ApproveForPayment";
            isAutoApproved = true;
        }
        else if (discrepancies.All(d => d.IsAutoResolvable) && totalVariance <= request.AutoApprovalMaxDiscrepancy)
        {
            matchStatus = "WithinTolerance";
            recommendedAction = "ApproveForPayment";
            isAutoApproved = true;
        }
        else if (discrepancies.Any(d => d.DiscrepancyType == "UnmatchedPoItem" || d.DiscrepancyType == "OverBilledQuantity"))
        {
            matchStatus = "DiscrepancyBlocked";
            recommendedAction = discrepancies.Any(d => d.DiscrepancyType == "OverBilledQuantity")
                ? "IssueVendorDebitMemo"
                : "RejectInvoice";
            isAutoApproved = false;
        }
        else if (discrepancies.Any(d => d.DiscrepancyType == "MissingGoodsReceipt"))
        {
            matchStatus = "UnderReview";
            recommendedAction = "HoldPaymentRequireApproval";
            isAutoApproved = false;
        }
        else
        {
            matchStatus = "DiscrepancyBlocked";
            recommendedAction = "HoldPaymentRequireApproval";
            isAutoApproved = false;
        }

        return new ThreeWayMatchResult(
            MatchId: $"3WM-{Guid.NewGuid():N}",
            TenantId: request.TenantId,
            InvoiceNumber: request.InvoiceNumber,
            PurchaseOrderNumber: request.PurchaseOrderNumber,
            MatchStatus: matchStatus,
            TotalPoAmount: totalPo,
            TotalGrnAmount: totalGrn,
            TotalInvoiceAmount: totalInvoice,
            TotalVarianceAmount: totalVariance,
            Discrepancies: discrepancies,
            RecommendedAction: recommendedAction,
            IsAutoApproved: isAutoApproved,
            ReconciledAtUtc: DateTime.UtcNow
        );
    }
}
