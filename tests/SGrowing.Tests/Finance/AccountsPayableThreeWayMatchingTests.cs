using FluentAssertions;
using SGrowing.Api.Modules.Finance.AccountsPayableThreeWayMatching;
using Xunit;

namespace SGrowing.Tests.Finance;

public class AccountsPayableThreeWayMatchingTests
{
    private readonly ApThreeWayMatchEngine _engine = new();

    [Fact]
    public void ReconcileInvoice_WhenPoGrnAndInvoiceMatchExactly_ReturnsPerfectMatch()
    {
        var request = new ThreeWayMatchRequest(
            TenantId: "tenant-erp-01",
            VendorId: "vend-500",
            InvoiceNumber: "INV-2026-001",
            PurchaseOrderNumber: "PO-8901",
            Currency: "USD",
            PoLines: new List<PurchaseOrderLine>
            {
                new("po-1", "SKU-BEARING", "Ball Bearings 10mm", 100, 5.00m)
            },
            GrnLines: new List<GoodsReceiptLine>
            {
                new("grn-1", "SKU-BEARING", ReceivedQuantity: 100, AcceptedQuantity: 100, RejectedQuantity: 0, DateTime.UtcNow)
            },
            InvoiceLines: new List<VendorInvoiceLine>
            {
                new("inv-1", "SKU-BEARING", InvoicedQuantity: 100, InvoicedUnitPrice: 5.00m, LineTotal: 500.00m)
            }
        );

        var result = _engine.ReconcileInvoice(request);

        result.Should().NotBeNull();
        result.MatchStatus.Should().Be("PerfectMatch");
        result.RecommendedAction.Should().Be("ApproveForPayment");
        result.IsAutoApproved.Should().BeTrue();
        result.TotalVarianceAmount.Should().Be(0.0m);
        result.Discrepancies.Should().BeEmpty();
    }

    [Fact]
    public void ReconcileInvoice_WhenPriceVarianceWithinTolerance_ReturnsWithinTolerance()
    {
        var request = new ThreeWayMatchRequest(
            TenantId: "tenant-erp-01",
            VendorId: "vend-500",
            InvoiceNumber: "INV-2026-002",
            PurchaseOrderNumber: "PO-8902",
            Currency: "USD",
            PoLines: new List<PurchaseOrderLine>
            {
                new("po-1", "SKU-BOLT", "Hex Bolt M8", 1000, 0.10m, TolerancePercentage: 5.0m, MaxPriceToleranceAmount: 5.0m)
            },
            GrnLines: new List<GoodsReceiptLine>
            {
                new("grn-1", "SKU-BOLT", ReceivedQuantity: 1000, AcceptedQuantity: 1000, RejectedQuantity: 0, DateTime.UtcNow)
            },
            InvoiceLines: new List<VendorInvoiceLine>
            {
                // Price is 0.102 (+2% variance within 5% tolerance)
                new("inv-1", "SKU-BOLT", InvoicedQuantity: 1000, InvoicedUnitPrice: 0.102m, LineTotal: 102.00m)
            },
            AutoApprovalMaxDiscrepancy: 5.0m
        );

        var result = _engine.ReconcileInvoice(request);

        result.MatchStatus.Should().Be("WithinTolerance");
        result.RecommendedAction.Should().Be("ApproveForPayment");
        result.IsAutoApproved.Should().BeTrue();
        result.TotalVarianceAmount.Should().Be(2.00m);
    }

    [Fact]
    public void ReconcileInvoice_WhenVendorOverBillsQuantity_BlocksAndRecommendsDebitMemo()
    {
        var request = new ThreeWayMatchRequest(
            TenantId: "tenant-erp-01",
            VendorId: "vend-600",
            InvoiceNumber: "INV-2026-003",
            PurchaseOrderNumber: "PO-8903",
            Currency: "USD",
            PoLines: new List<PurchaseOrderLine>
            {
                new("po-1", "SKU-CABLE", "Cat6 Ethernet Cable", 50, 20.00m)
            },
            GrnLines: new List<GoodsReceiptLine>
            {
                new("grn-1", "SKU-CABLE", ReceivedQuantity: 40, AcceptedQuantity: 40, RejectedQuantity: 0, DateTime.UtcNow)
            },
            InvoiceLines: new List<VendorInvoiceLine>
            {
                // Invoiced for 50, but only 40 received and accepted!
                new("inv-1", "SKU-CABLE", InvoicedQuantity: 50, InvoicedUnitPrice: 20.00m, LineTotal: 1000.00m)
            }
        );

        var result = _engine.ReconcileInvoice(request);

        result.MatchStatus.Should().Be("DiscrepancyBlocked");
        result.RecommendedAction.Should().Be("IssueVendorDebitMemo");
        result.IsAutoApproved.Should().BeFalse();
        result.Discrepancies.Should().Contain(d => d.DiscrepancyType == "OverBilledQuantity");
    }

    [Fact]
    public void ReconcileInvoice_WhenMissingGoodsReceipt_PlacesOnHoldUnderReview()
    {
        var request = new ThreeWayMatchRequest(
            TenantId: "tenant-erp-01",
            VendorId: "vend-700",
            InvoiceNumber: "INV-2026-004",
            PurchaseOrderNumber: "PO-8904",
            Currency: "USD",
            PoLines: new List<PurchaseOrderLine>
            {
                new("po-1", "SKU-SERVER", "1U Rackmount Server", 1, 1500.00m)
            },
            GrnLines: new List<GoodsReceiptLine>(), // No GRN recorded yet!
            InvoiceLines: new List<VendorInvoiceLine>
            {
                new("inv-1", "SKU-SERVER", InvoicedQuantity: 1, InvoicedUnitPrice: 1500.00m, LineTotal: 1500.00m)
            }
        );

        var result = _engine.ReconcileInvoice(request);

        result.MatchStatus.Should().Be("UnderReview");
        result.RecommendedAction.Should().Be("HoldPaymentRequireApproval");
        result.IsAutoApproved.Should().BeFalse();
        result.Discrepancies.Should().Contain(d => d.DiscrepancyType == "MissingGoodsReceipt");
    }

    [Fact]
    public void ReconcileInvoice_WhenItemNotInPo_FlagsUnmatchedItem()
    {
        var request = new ThreeWayMatchRequest(
            TenantId: "tenant-erp-01",
            VendorId: "vend-800",
            InvoiceNumber: "INV-2026-005",
            PurchaseOrderNumber: "PO-8905",
            Currency: "USD",
            PoLines: new List<PurchaseOrderLine>
            {
                new("po-1", "SKU-KEYBOARD", "Mechanical Keyboard", 10, 50.00m)
            },
            GrnLines: new List<GoodsReceiptLine>
            {
                new("grn-1", "SKU-KEYBOARD", 10, 10, 0, DateTime.UtcNow)
            },
            InvoiceLines: new List<VendorInvoiceLine>
            {
                new("inv-1", "SKU-MOUSE-GHOST", InvoicedQuantity: 5, InvoicedUnitPrice: 30.00m, LineTotal: 150.00m)
            }
        );

        var result = _engine.ReconcileInvoice(request);

        result.MatchStatus.Should().Be("DiscrepancyBlocked");
        result.IsAutoApproved.Should().BeFalse();
        result.Discrepancies.Should().Contain(d => d.DiscrepancyType == "UnmatchedPoItem");
    }
}
