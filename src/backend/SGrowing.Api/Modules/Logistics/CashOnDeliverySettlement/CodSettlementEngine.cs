namespace SGrowing.Api.Modules.Logistics.CashOnDeliverySettlement;

public interface ICodSettlementEngine
{
    CodSettlementResult ReconcileSettlement(CodSettlementRequest request);
}

public class CodSettlementEngine : ICodSettlementEngine
{
    public CodSettlementResult ReconcileSettlement(CodSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        int deliveredCount = 0;
        int rtoCount = 0;
        decimal totalCodDue = 0.0m;
        decimal totalCashCollected = 0.0m;
        decimal totalDeliveryFees = 0.0m;
        decimal totalRtoFees = 0.0m;

        var discrepancies = new List<CodOrderDiscrepancy>();

        foreach (var order in request.ManifestOrders)
        {
            totalCodDue += order.CodAmountDue;

            if (string.Equals(order.DeliveryStatus, "DeliveredCollected", StringComparison.OrdinalIgnoreCase))
            {
                deliveredCount++;
                totalCashCollected += order.CollectedCashAmount;
                totalDeliveryFees += request.CarrierShippingCommissionPerOrder;

                var diff = order.CodAmountDue - order.CollectedCashAmount;
                if (Math.Abs(diff) > 0.001m)
                {
                    discrepancies.Add(new CodOrderDiscrepancy(
                        TrackingNumber: order.TrackingNumber,
                        OrderId: order.OrderId,
                        ExpectedCodAmount: order.CodAmountDue,
                        CarrierReportedAmount: order.CollectedCashAmount,
                        VarianceAmount: diff,
                        DiscrepancyType: diff > 0 ? "Shortfall" : "OverCollection"
                    ));
                }
            }
            else if (string.Equals(order.DeliveryStatus, "ReturnedToOrigin", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(order.DeliveryStatus, "CustomerRefused", StringComparison.OrdinalIgnoreCase))
            {
                rtoCount++;
                totalRtoFees += request.ReturnToOriginFeePerOrder;

                if (order.CollectedCashAmount > 0.0m)
                {
                    discrepancies.Add(new CodOrderDiscrepancy(
                        TrackingNumber: order.TrackingNumber,
                        OrderId: order.OrderId,
                        ExpectedCodAmount: 0.0m,
                        CarrierReportedAmount: order.CollectedCashAmount,
                        VarianceAmount: -order.CollectedCashAmount,
                        DiscrepancyType: "OverCollection"
                    ));
                }
            }
            else if (string.Equals(order.DeliveryStatus, "DeliveryFailed", StringComparison.OrdinalIgnoreCase))
            {
                // In transit or rescheduled; no fees yet
            }
        }

        decimal netExpectedMerchant = totalCashCollected - totalDeliveryFees - totalRtoFees;
        decimal actualReceived = request.Remittance.TotalTransferredAmount;
        decimal settlementVariance = actualReceived - netExpectedMerchant;

        bool isBalanced = Math.Abs(settlementVariance) < 0.01m;
        string settlementStatus;

        if (isBalanced && discrepancies.Count == 0)
        {
            settlementStatus = "BalancedAndSettled";
        }
        else if (settlementVariance < 0.0m)
        {
            settlementStatus = "CarrierUnderRemitted";
        }
        else if (settlementVariance > 0.0m)
        {
            settlementStatus = "CarrierOverRemitted";
        }
        else
        {
            settlementStatus = "RequiresInvestigation";
        }

        return new CodSettlementResult(
            SettlementId: $"COD-SETTLE-{Guid.NewGuid():N}",
            TenantId: request.TenantId,
            CarrierId: request.CarrierId,
            RemittanceReference: request.Remittance.RemittanceReference,
            ReconciledAtUtc: DateTime.UtcNow,
            TotalShipmentsProcessed: request.ManifestOrders.Count,
            DeliveredOrdersCount: deliveredCount,
            ReturnedToOriginCount: rtoCount,
            TotalCodDue: totalCodDue,
            TotalCashCollected: totalCashCollected,
            TotalCarrierDeliveryFees: totalDeliveryFees,
            TotalCarrierRtoFees: totalRtoFees,
            NetPayableToMerchant: netExpectedMerchant,
            ActualRemittanceReceived: actualReceived,
            SettlementVariance: settlementVariance,
            Discrepancies: discrepancies,
            IsBalanced: isBalanced,
            SettlementStatus: settlementStatus
        );
    }
}
