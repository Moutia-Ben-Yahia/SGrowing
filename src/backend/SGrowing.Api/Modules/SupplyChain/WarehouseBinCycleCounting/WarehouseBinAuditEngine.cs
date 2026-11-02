namespace SGrowing.Api.Modules.SupplyChain.WarehouseBinCycleCounting;

public interface IWarehouseBinAuditEngine
{
    CycleCountAuditResult ProcessCycleCountAudit(CycleCountAuditRequest request);
}

public class WarehouseBinAuditEngine : IWarehouseBinAuditEngine
{
    public CycleCountAuditResult ProcessCycleCountAudit(CycleCountAuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bookStockMap = request.BookStock.ToDictionary(
            b => $"{b.BinCode}|{b.Sku}",
            b => b,
            StringComparer.OrdinalIgnoreCase
        );

        var physicalCountsGrouped = request.PhysicalCounts.GroupBy(
            c => $"{c.BinCode}|{c.Sku}",
            StringComparer.OrdinalIgnoreCase
        ).ToDictionary(
            g => g.Key,
            g => g.Sum(c => c.CountedQuantity),
            StringComparer.OrdinalIgnoreCase
        );

        var allKeys = new HashSet<string>(bookStockMap.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in physicalCountsGrouped.Keys)
        {
            allKeys.Add(key);
        }

        var adjustments = new List<CycleCountAdjustment>();
        int correctMatches = 0;
        decimal totalNetVariance = 0.0m;
        decimal totalAbsoluteDiscrepancy = 0.0m;

        foreach (var key in allKeys)
        {
            bookStockMap.TryGetValue(key, out var bookItem);
            physicalCountsGrouped.TryGetValue(key, out var physicalQty);

            double bookQty = bookItem?.BookQuantity ?? 0.0;
            decimal unitCost = bookItem?.UnitCost ?? 10.0m;
            string sku = bookItem?.Sku ?? key.Split('|')[1];
            string binCode = bookItem?.BinCode ?? key.Split('|')[0];

            double varianceQty = physicalQty - bookQty;

            if (Math.Abs(varianceQty) < 1e-4)
            {
                correctMatches++;
            }
            else
            {
                decimal varianceVal = (decimal)varianceQty * unitCost;
                totalNetVariance += varianceVal;
                totalAbsoluteDiscrepancy += Math.Abs(varianceVal);

                string reason = varianceQty < 0 ? "Shrinkage" : "Overage";
                bool requiresSignoff = Math.Abs(varianceVal) >= request.HighValueThresholdForSignoff
                    || string.Equals(bookItem?.AbcCategory, "A", StringComparison.OrdinalIgnoreCase);

                adjustments.Add(new CycleCountAdjustment(
                    Sku: sku,
                    BinCode: binCode,
                    BookQuantity: bookQty,
                    PhysicalQuantity: physicalQty,
                    VarianceQuantity: varianceQty,
                    VarianceAmount: varianceVal,
                    AdjustmentReason: reason,
                    RequiresSupervisorSignoff: requiresSignoff
                ));
            }
        }

        int totalAudited = allKeys.Count;
        double accuracyPct = totalAudited > 0
            ? Math.Round(((double)correctMatches / totalAudited) * 100.0, 2)
            : 100.0;

        string auditGrade;
        if (accuracyPct >= 98.0)
        {
            auditGrade = "AuditGradeA_Exceptional";
        }
        else if (accuracyPct >= 95.0)
        {
            auditGrade = "AuditGradeB_Compliant";
        }
        else
        {
            auditGrade = "AuditGradeC_RequiresRecount";
        }

        var uniqueBins = request.PhysicalCounts.Select(c => c.BinCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return new CycleCountAuditResult(
            AuditId: $"BIN-AUDIT-{Guid.NewGuid():N}",
            TenantId: request.TenantId,
            WarehouseId: request.WarehouseId,
            CompletedAtUtc: DateTime.UtcNow,
            TotalBinsAudited: uniqueBins,
            TotalItemsAudited: totalAudited,
            NetInventoryAccuracyPercentage: accuracyPct,
            TotalNetVarianceValue: totalNetVariance,
            TotalAbsoluteDiscrepancyValue: totalAbsoluteDiscrepancy,
            Adjustments: adjustments,
            AdjustmentsRequiringSignoffCount: adjustments.Count(a => a.RequiresSupervisorSignoff),
            AuditGrade: auditGrade
        );
    }
}
