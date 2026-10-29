namespace SGrowing.Core.Entities;

public class CallCenterOrderLockRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid LockedByUserId { get; set; }
    public string LockedByUserName { get; set; } = string.Empty;
    public DateTime LockedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(3);
    public bool IsActive { get; set; } = true;

    public static CallCenterOrderLockRecord Create(
        Guid tenantId,
        Guid orderId,
        string orderNumber,
        Guid userId,
        string userName,
        TimeSpan? ttl = null)
    {
        var duration = ttl ?? TimeSpan.FromMinutes(3);
        return new CallCenterOrderLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            OrderNumber = orderNumber.Trim(),
            LockedByUserId = userId,
            LockedByUserName = userName.Trim(),
            LockedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(duration),
            IsActive = true
        };
    }

    public void Renew(TimeSpan extension)
    {
        ExpiresAtUtc = DateTime.UtcNow.Add(extension);
        IsActive = true;
    }

    public void Release()
    {
        IsActive = false;
        ExpiresAtUtc = DateTime.UtcNow;
    }
}

public class CodNetMarginLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal GrossRevenue { get; set; }
    public decimal Cogs { get; set; }
    public decimal PackagingCost { get; set; }
    public decimal AdSpendCost { get; set; }
    public decimal CourierShippingFee { get; set; }
    public decimal ReturnRatePercent { get; set; }
    public decimal ReturnPenaltyFee { get; set; }
    public decimal ReturnRiskCost { get; set; }
    public decimal NetProfit { get; set; }
    public decimal NetMarginPercent { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CodNetMarginLedgerEntry Calculate(
        Guid tenantId,
        Guid orderId,
        string orderNumber,
        decimal grossRevenue,
        decimal cogs,
        decimal packagingCost,
        decimal adSpendCost,
        decimal courierShippingFee,
        decimal returnRatePercent,
        decimal returnPenaltyFee)
    {
        var normalizedReturnRate = Math.Clamp(returnRatePercent, 0m, 1m);
        var returnRiskCost = Math.Round(normalizedReturnRate * returnPenaltyFee, 2);
        var netProfit = Math.Round(grossRevenue - cogs - packagingCost - adSpendCost - courierShippingFee - returnRiskCost, 2);
        var netMarginPercent = grossRevenue > 0 ? Math.Round((netProfit / grossRevenue) * 100m, 2) : 0m;

        return new CodNetMarginLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            OrderNumber = orderNumber.Trim(),
            GrossRevenue = grossRevenue,
            Cogs = cogs,
            PackagingCost = packagingCost,
            AdSpendCost = adSpendCost,
            CourierShippingFee = courierShippingFee,
            ReturnRatePercent = normalizedReturnRate,
            ReturnPenaltyFee = returnPenaltyFee,
            ReturnRiskCost = returnRiskCost,
            NetProfit = netProfit,
            NetMarginPercent = netMarginPercent,
            CalculatedAtUtc = DateTime.UtcNow
        };
    }
}
