namespace SGrowing.Api.Modules.Manufacturing.MaterialRequirementsPlanning;

public interface IMrpEngine
{
    MrpExplosionResult ExplodeRequirements(MrpExplosionRequest request);
}

public class MrpEngine : IMrpEngine
{
    public MrpExplosionResult ExplodeRequirements(MrpExplosionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bucketSize = Math.Max(1, request.BucketSizeDays);
        var totalBuckets = Math.Max(1, (int)Math.Ceiling((double)request.PlanningHorizonDays / bucketSize));
        var itemMap = request.Items.ToDictionary(i => i.Sku, StringComparer.OrdinalIgnoreCase);
        var bomMap = request.BillOfMaterials.GroupBy(b => b.ParentSku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var allDemands = new List<MrpGrossDemand>(request.GrossDemands);
        var itemBuckets = new Dictionary<string, List<MrpTimeBucket>>(StringComparer.OrdinalIgnoreCase);
        var actionMessages = new List<MrpActionMessage>();
        int totalPlannedOrders = 0;
        double stockoutExposure = 0.0;

        // Process parent items first, then explode components
        var processedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var executionQueue = new Queue<string>(itemMap.Keys);

        while (executionQueue.Count > 0)
        {
            var sku = executionQueue.Dequeue();
            if (processedSkus.Contains(sku)) continue;

            if (!itemMap.TryGetValue(sku, out var item)) continue;

            var buckets = new List<MrpTimeBucket>();
            var currentOnHand = Math.Max(0.0, item.CurrentOnHand - item.AllocatedStock);
            var leadTimeBuckets = (int)Math.Ceiling((double)item.LeadTimeDays / bucketSize);

            // Collect demands and receipts for this SKU
            var skuDemands = allDemands.Where(d => string.Equals(d.Sku, sku, StringComparison.OrdinalIgnoreCase)).ToList();
            var skuReceipts = request.ScheduledReceipts.Where(r => string.Equals(r.Sku, sku, StringComparison.OrdinalIgnoreCase)).ToList();

            for (int b = 0; b < totalBuckets; b++)
            {
                var bucketDate = request.PlanStartDate.AddDays(b * bucketSize);
                var bucketEndDate = bucketDate.AddDays(bucketSize);

                var gross = skuDemands
                    .Where(d => d.RequiredDate >= bucketDate && d.RequiredDate < bucketEndDate)
                    .Sum(d => d.Quantity);

                var receipts = skuReceipts
                    .Where(r => r.ArrivalDate >= bucketDate && r.ArrivalDate < bucketEndDate)
                    .Sum(r => r.Quantity);

                var projectedBeforePlan = currentOnHand + receipts - gross;
                double netRequirement = 0.0;
                double plannedReceipt = 0.0;
                double plannedRelease = 0.0;

                if (projectedBeforePlan < item.SafetyStock)
                {
                    var deficit = item.SafetyStock - projectedBeforePlan;
                    netRequirement = deficit;
                    stockoutExposure += Math.Max(0.0, -projectedBeforePlan);

                    // Lot sizing calculation: Lot-for-Lot with Minimum Order Quantity and Batch Multiples
                    var orderQty = Math.Max(item.MinimumOrderQuantity, netRequirement);
                    if (item.BatchMultiple > 0)
                    {
                        var remainder = orderQty % item.BatchMultiple;
                        if (remainder > 1e-6)
                        {
                            orderQty += (item.BatchMultiple - remainder);
                        }
                    }

                    plannedReceipt = orderQty;
                    plannedRelease = orderQty;
                    currentOnHand = projectedBeforePlan + plannedReceipt;
                    totalPlannedOrders++;

                    var releaseBucketIndex = Math.Max(0, b - leadTimeBuckets);
                    var releaseDate = request.PlanStartDate.AddDays(releaseBucketIndex * bucketSize);

                    actionMessages.Add(new MrpActionMessage(
                        Sku: sku,
                        ActionType: "ReleaseOrder",
                        ActionDate: releaseDate,
                        Quantity: plannedRelease,
                        Reason: $"Net requirement of {netRequirement:F1} units in bucket {b} ({bucketDate:yyyy-MM-dd})"
                    ));

                    // BOM Explosion: Create gross demands for sub-components
                    if (bomMap.TryGetValue(sku, out var components))
                    {
                        foreach (var comp in components)
                        {
                            var scrapMultiplier = 1.0 + (comp.ScrapPercentage / 100.0);
                            var componentDemandQty = plannedRelease * comp.QuantityPerParent * scrapMultiplier;

                            allDemands.Add(new MrpGrossDemand(
                                DemandId: $"MRP-BOM-{Guid.NewGuid():N}",
                                Sku: comp.ComponentSku,
                                RequiredDate: releaseDate,
                                Quantity: componentDemandQty,
                                Source: $"MRP Planned Order for {sku}"
                            ));

                            if (!processedSkus.Contains(comp.ComponentSku))
                            {
                                executionQueue.Enqueue(comp.ComponentSku);
                            }
                        }
                    }
                }
                else
                {
                    currentOnHand = projectedBeforePlan;
                }

                buckets.Add(new MrpTimeBucket(
                    BucketIndex: b,
                    BucketDate: bucketDate,
                    GrossRequirements: gross,
                    ScheduledReceipts: receipts,
                    ProjectedOnHand: currentOnHand,
                    NetRequirements: netRequirement,
                    PlannedOrderReceipts: plannedReceipt,
                    PlannedOrderReleases: plannedRelease
                ));
            }

            itemBuckets[sku] = buckets;
            processedSkus.Add(sku);
        }

        return new MrpExplosionResult(
            PlanId: $"MRP-{Guid.NewGuid():N}",
            TenantId: request.TenantId,
            GeneratedAtUtc: DateTime.UtcNow,
            TotalBuckets: totalBuckets,
            ItemBuckets: itemBuckets,
            ActionMessages: actionMessages,
            TotalPlannedOrdersCount: totalPlannedOrders,
            TotalStockoutExposureUnits: stockoutExposure
        );
    }
}
