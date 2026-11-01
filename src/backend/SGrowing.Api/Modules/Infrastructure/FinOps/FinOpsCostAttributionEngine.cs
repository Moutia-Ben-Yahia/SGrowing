namespace SGrowing.Api.Modules.Infrastructure.FinOps;

/// <summary>
/// FinOps engine attributing micro-metered cloud and on-premise infrastructure costs
/// down to individual tenants and business units with gross margin calculation.
/// </summary>
public sealed class FinOpsCostAttributionEngine
{
    private const decimal RatePerCpuMillicoreHourDzd = 0.045m;
    private const decimal RatePerMemoryGbHourDzd = 0.018m;
    private const decimal RatePerStorageGbMonthDzd = 12.00m;
    private const decimal RatePerEgressGbDzd = 8.50m;

    /// <summary>
    /// Computes full cost allocation statement for a tenant's recorded usage metrics.
    /// </summary>
    public TenantCostAllocationStatement GenerateStatement(GenerateCostStatementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(request));
        }

        var totalCpuMh = request.Usages.Sum(u => u.CpuMillicoresHours);
        var totalMemGbh = request.Usages.Sum(u => u.MemoryGigabyteHours);
        var totalStorageGb = request.Usages.Sum(u => u.StorageGigabytes);
        var totalEgressGb = request.Usages.Sum(u => u.EgressGigabytes);

        var computeCost = Math.Round((decimal)totalCpuMh * RatePerCpuMillicoreHourDzd, 2);
        var memoryCost = Math.Round((decimal)totalMemGbh * RatePerMemoryGbHourDzd, 2);
        var storageCost = Math.Round((decimal)totalStorageGb * RatePerStorageGbMonthDzd, 2);
        var networkCost = Math.Round((decimal)totalEgressGb * RatePerEgressGbDzd, 2);

        var totalCost = computeCost + memoryCost + storageCost + networkCost;
        var marginMultiplier = Math.Max(1.0m, request.MarginMultiplier);
        var allocatedPrice = Math.Round(totalCost * marginMultiplier, 2);
        var grossMargin = allocatedPrice - totalCost;

        var statementId = $"STMT-{request.TenantId}-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid():N}"[..26].ToUpperInvariant();

        return new TenantCostAllocationStatement(
            StatementId: statementId,
            TenantId: request.TenantId,
            BillingPeriod: request.BillingPeriod,
            ComputeCostDzd: computeCost,
            MemoryCostDzd: memoryCost,
            StorageCostDzd: storageCost,
            NetworkEgressCostDzd: networkCost,
            TotalCostDzd: totalCost,
            AllocatedPriceWithMarginDzd: allocatedPrice,
            GrossMarginDzd: grossMargin,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
