using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;

public record RebillingCalculationResult(
    string ServiceType,
    decimal WholesaleCost,
    decimal MarkupMultiplier,
    decimal RetailCharge,
    decimal NetAgencyProfit
);

public interface ITenantRebillingService
{
    Task<TenantRebillingConfig> SetRebillingConfigAsync(
        Guid agencyTenantId,
        Guid subAccountTenantId,
        decimal smsMultiplier,
        decimal voiceMultiplier,
        decimal aiMultiplier);

    Task<TenantRebillingConfig?> GetRebillingConfigAsync(
        Guid agencyTenantId,
        Guid subAccountTenantId);

    Task<RebillingCalculationResult> CalculateRebilledCostAsync(
        Guid agencyTenantId,
        Guid subAccountTenantId,
        string serviceType,
        decimal wholesaleCost);
}
