using FluentAssertions;
using SGrowing.Api.Modules.Logistics.CashOnDeliverySettlement;
using Xunit;

namespace SGrowing.Tests.Logistics;

public class CashOnDeliverySettlementTests
{
    private readonly CodSettlementEngine _engine = new();

    [Fact]
    public void ReconcileSettlement_WhenCarrierRemittanceMatchesNetPayable_ReturnsBalancedAndSettled()
    {
        var request = new CodSettlementRequest(
            TenantId: "tenant-erp-01",
            CarrierId: "carrier-aramex-tn",
            SettlementBatchDateUtc: DateTime.UtcNow,
            Remittance: new CarrierRemittanceDeposit(
                RemittanceReference: "REMIT-2026-11-01",
                CarrierId: "carrier-aramex-tn",
                TotalTransferredAmount: 485.00m, // 500 collected - (2 delivered * 5 fee = 10 fee) - (1 RTO * 2.5 fee = 2.5) = 487.5
                BankDepositDateUtc: DateTime.UtcNow,
                BankTransactionId: "TXN-BANK-9912"
            ),
            ManifestOrders: new List<CodShipmentOrder>
            {
                new("TRK-001", "ORD-101", "Sami Trabelsi", CodAmountDue: 250.00m, "carrier-aramex-tn", "DeliveredCollected", CollectedCashAmount: 250.00m),
                new("TRK-002", "ORD-102", "Mouna Cherif", CodAmountDue: 250.00m, "carrier-aramex-tn", "DeliveredCollected", CollectedCashAmount: 250.00m)
            },
            CarrierShippingCommissionPerOrder: 5.0m,
            ReturnToOriginFeePerOrder: 2.5m
        );

        // 500 collected - (2 * 5 = 10) = 490 expected
        var modifiedRequest = request with
        {
            Remittance = request.Remittance with { TotalTransferredAmount = 490.00m }
        };

        var result = _engine.ReconcileSettlement(modifiedRequest);

        result.Should().NotBeNull();
        result.DeliveredOrdersCount.Should().Be(2);
        result.ReturnedToOriginCount.Should().Be(0);
        result.TotalCashCollected.Should().Be(500.00m);
        result.TotalCarrierDeliveryFees.Should().Be(10.00m);
        result.NetPayableToMerchant.Should().Be(490.00m);
        result.IsBalanced.Should().BeTrue();
        result.SettlementStatus.Should().Be("BalancedAndSettled");
        result.Discrepancies.Should().BeEmpty();
    }

    [Fact]
    public void ReconcileSettlement_WithReturnToOriginOrder_DeductsRtoFeeWithoutCash()
    {
        var request = new CodSettlementRequest(
            TenantId: "tenant-erp-01",
            CarrierId: "carrier-yalidine",
            SettlementBatchDateUtc: DateTime.UtcNow,
            Remittance: new CarrierRemittanceDeposit(
                RemittanceReference: "REMIT-2026-11-02",
                CarrierId: "carrier-yalidine",
                // 300 collected - 5 delivery fee - 2.5 RTO fee = 292.50
                TotalTransferredAmount: 292.50m,
                BankDepositDateUtc: DateTime.UtcNow,
                BankTransactionId: "TXN-BANK-9913"
            ),
            ManifestOrders: new List<CodShipmentOrder>
            {
                new("TRK-101", "ORD-201", "Customer A", CodAmountDue: 300.00m, "carrier-yalidine", "DeliveredCollected", 300.00m),
                new("TRK-102", "ORD-202", "Customer B", CodAmountDue: 150.00m, "carrier-yalidine", "ReturnedToOrigin", 0.00m)
            },
            CarrierShippingCommissionPerOrder: 5.0m,
            ReturnToOriginFeePerOrder: 2.5m
        );

        var result = _engine.ReconcileSettlement(request);

        result.DeliveredOrdersCount.Should().Be(1);
        result.ReturnedToOriginCount.Should().Be(1);
        result.TotalCarrierDeliveryFees.Should().Be(5.00m);
        result.TotalCarrierRtoFees.Should().Be(2.50m);
        result.NetPayableToMerchant.Should().Be(292.50m);
        result.IsBalanced.Should().BeTrue();
        result.SettlementStatus.Should().Be("BalancedAndSettled");
    }

    [Fact]
    public void ReconcileSettlement_WhenCarrierUnderRemits_FlagsVarianceAndStatus()
    {
        var request = new CodSettlementRequest(
            TenantId: "tenant-erp-01",
            CarrierId: "carrier-dhl-express",
            SettlementBatchDateUtc: DateTime.UtcNow,
            Remittance: new CarrierRemittanceDeposit(
                RemittanceReference: "REMIT-2026-11-03",
                CarrierId: "carrier-dhl-express",
                // Carrier transferred only 400 when net expected is 490 (under-remittance of 90)
                TotalTransferredAmount: 400.00m,
                BankDepositDateUtc: DateTime.UtcNow,
                BankTransactionId: "TXN-BANK-9914"
            ),
            ManifestOrders: new List<CodShipmentOrder>
            {
                new("TRK-201", "ORD-301", "Customer C", CodAmountDue: 500.00m, "carrier-dhl-express", "DeliveredCollected", 500.00m)
            },
            CarrierShippingCommissionPerOrder: 10.0m
        );

        var result = _engine.ReconcileSettlement(request);

        result.NetPayableToMerchant.Should().Be(490.00m);
        result.SettlementVariance.Should().Be(-90.00m);
        result.IsBalanced.Should().BeFalse();
        result.SettlementStatus.Should().Be("CarrierUnderRemitted");
    }

    [Fact]
    public void ReconcileSettlement_WhenCourierReportsShortfallOnOrder_CreatesDiscrepancy()
    {
        var request = new CodSettlementRequest(
            TenantId: "tenant-erp-01",
            CarrierId: "carrier-local",
            SettlementBatchDateUtc: DateTime.UtcNow,
            Remittance: new CarrierRemittanceDeposit(
                RemittanceReference: "REMIT-2026-11-04",
                CarrierId: "carrier-local",
                TotalTransferredAmount: 85.00m,
                BankDepositDateUtc: DateTime.UtcNow,
                BankTransactionId: "TXN-BANK-9915"
            ),
            ManifestOrders: new List<CodShipmentOrder>
            {
                // Due was 100, but courier only collected 90 (shortfall of 10)
                new("TRK-301", "ORD-401", "Customer D", CodAmountDue: 100.00m, "carrier-local", "DeliveredCollected", CollectedCashAmount: 90.00m)
            },
            CarrierShippingCommissionPerOrder: 5.0m
        );

        var result = _engine.ReconcileSettlement(request);

        result.Discrepancies.Should().ContainSingle();
        var disc = result.Discrepancies[0];
        disc.DiscrepancyType.Should().Be("Shortfall");
        disc.ExpectedCodAmount.Should().Be(100.00m);
        disc.CarrierReportedAmount.Should().Be(90.00m);
        disc.VarianceAmount.Should().Be(10.00m);
    }

    [Fact]
    public void ReconcileSettlement_WithInTransitFailedDeliveries_ExcludesFeesUntilFinalStatus()
    {
        var request = new CodSettlementRequest(
            TenantId: "tenant-erp-01",
            CarrierId: "carrier-local",
            SettlementBatchDateUtc: DateTime.UtcNow,
            Remittance: new CarrierRemittanceDeposit(
                RemittanceReference: "REMIT-2026-11-05",
                CarrierId: "carrier-local",
                TotalTransferredAmount: 0.00m,
                BankDepositDateUtc: DateTime.UtcNow,
                BankTransactionId: "TXN-BANK-9916"
            ),
            ManifestOrders: new List<CodShipmentOrder>
            {
                // Customer not home, delivery rescheduled (DeliveryFailed)
                new("TRK-401", "ORD-501", "Customer E", CodAmountDue: 75.00m, "carrier-local", "DeliveryFailed", CollectedCashAmount: 0.00m)
            },
            CarrierShippingCommissionPerOrder: 5.0m
        );

        var result = _engine.ReconcileSettlement(request);

        result.DeliveredOrdersCount.Should().Be(0);
        result.ReturnedToOriginCount.Should().Be(0);
        result.TotalCarrierDeliveryFees.Should().Be(0.00m);
        result.NetPayableToMerchant.Should().Be(0.00m);
        result.IsBalanced.Should().BeTrue();
    }
}
