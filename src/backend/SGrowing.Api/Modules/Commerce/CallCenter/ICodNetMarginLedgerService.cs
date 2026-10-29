using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CallCenter;

public record CodMarginSummaryDto(
    Guid TenantId,
    int TotalOrdersEvaluated,
    decimal TotalGrossRevenue,
    decimal TotalCogs,
    decimal TotalPackaging,
    decimal TotalAdSpend,
    decimal TotalCourierFees,
    decimal TotalReturnRiskCost,
    decimal TotalNetProfit,
    decimal OverallNetMarginPercent,
    decimal AverageOrderValue
);

public interface ICodNetMarginLedgerService
{
    Task<CodNetMarginLedgerEntry> CalculateAndRecordMarginAsync(
        Guid tenantId,
        Guid orderId,
        string orderNumber,
        decimal grossRevenue,
        decimal cogs,
        decimal packagingCost,
        decimal adSpendCost,
        decimal courierShippingFee,
        decimal returnRatePercent,
        decimal returnPenaltyFee);

    Task<CodNetMarginLedgerEntry?> GetOrderMarginAsync(Guid tenantId, Guid orderId);

    Task<CodMarginSummaryDto> GetTenantMarginSummaryAsync(Guid tenantId, DateTime? fromUtc = null);
}
