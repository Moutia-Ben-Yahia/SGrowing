using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Finance.Factoring;
using Xunit;

namespace SGrowing.Tests.Finance;

/// <summary>
/// Comprehensive test suite for Phase 41:
/// Autonomous Reverse Factoring, Dynamic Discounting &amp; Early Supplier Payment Matrix.
/// </summary>
public sealed class ReverseFactoringAndDynamicDiscountingTests
{
    private readonly DynamicDiscountingEngine _discountingEngine = new();
    private readonly ReverseFactoringLiquidityPoolService _liquidityPool = new();
    private readonly SupplierDiscountAcceptanceBandit _bandit = new(seed: 42);

    #region 1. Sliding-Scale Discount Rate & Annualized APR Yield Calculations

    [Fact]
    public void CalculateEarlyPaymentOffer_StandardMaturity_AppliesSlidingScaleFormulaAccurately()
    {
        // Arrange
        var invoiceDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = invoiceDate.AddDays(60);
        var targetPaymentDate = invoiceDate.AddDays(30); // 30 days early

        var invoice = new InvoicePayable(
            InvoiceId: "INV-STANDARD-01",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 1_000_000m,
            InvoiceDateUtc: invoiceDate,
            DueDateUtc: dueDate,
            PaymentTermsDays: 60,
            ApprovedForPayment: true
        );

        // Act: annualAprTarget = 0.24 (24% APR)
        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetPaymentDate, annualAprTarget: 0.24m);

        // Assert:
        // DaysEarly = 30
        // DiscountRate = 0.24 * (30 / 360) = 0.02 (2.0%)
        // BuyerSavings = 1,000,000 * 0.02 = 20,000 DZD
        // NetPayment = 980,000 DZD
        // Annualized APR Yield = (0.02 / 0.98) * (360 / 30) = 0.244898 -> 24.4898%
        offer.Should().NotBeNull();
        offer.InvoiceId.Should().Be("INV-STANDARD-01");
        offer.DaysEarly.Should().Be(30);
        offer.DiscountRatePct.Should().Be(2.0000m);
        offer.BuyerSavingsDzd.Should().Be(20_000.00m);
        offer.NetPaymentAmountDzd.Should().Be(980_000.00m);
        offer.AnnualizedAprYieldPct.Should().Be(24.4898m);
        offer.FundingType.Should().Be(FactoringType.DynamicDiscounting);
        offer.Status.Should().Be(InvoiceFundingStatus.PendingOffer);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_TwoTenNetThirty_YieldsApproximately36Point7PercentApr()
    {
        // Arrange: Classic "2/10 Net 30" discount terms:
        // Payment made on Day 10 on a 30-day invoice -> 20 days early.
        // 2% discount taken 20 days prior to due date.
        var invoiceDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = invoiceDate.AddDays(30);
        var targetPaymentDate = invoiceDate.AddDays(10); // Day 10 -> 20 days early

        var invoice = new InvoicePayable(
            InvoiceId: "INV-2-10-NET-30",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 500_000m,
            InvoiceDateUtc: invoiceDate,
            DueDateUtc: dueDate,
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        // To achieve a 2% discount at 20 days early under standard 360-day year:
        // DiscountRate = APR * (20 / 360) = 0.02 => APR = 0.36 (36%)
        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetPaymentDate, annualAprTarget: 0.36m);

        // Assert:
        // Annualized APR Yield = (0.02 / 0.98) * (360 / 20) = 0.02040816 * 18 = 0.367347 -> ~36.73% APR
        offer.DaysEarly.Should().Be(20);
        offer.DiscountRatePct.Should().Be(2.0000m);
        offer.BuyerSavingsDzd.Should().Be(10_000.00m);
        offer.NetPaymentAmountDzd.Should().Be(490_000.00m);
        offer.AnnualizedAprYieldPct.Should().BeApproximately(36.7347m, 0.01m);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_DifferentAprTargets_ScalesDiscountLinearly()
    {
        // Arrange
        var invoiceDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoice = new InvoicePayable(
            InvoiceId: "INV-SCALE-01",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 2_000_000m,
            InvoiceDateUtc: invoiceDate,
            DueDateUtc: invoiceDate.AddDays(45),
            PaymentTermsDays: 45,
            ApprovedForPayment: true
        );

        var targetDate = invoiceDate.AddDays(15); // 30 days early

        // Act
        var offer12 = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetDate, annualAprTarget: 0.12m);
        var offer24 = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetDate, annualAprTarget: 0.24m);
        var offer36 = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetDate, annualAprTarget: 0.36m);

        // Assert
        offer12.DiscountRatePct.Should().Be(1.0000m);
        offer24.DiscountRatePct.Should().Be(2.0000m);
        offer36.DiscountRatePct.Should().Be(3.0000m);

        offer24.BuyerSavingsDzd.Should().Be(offer12.BuyerSavingsDzd * 2);
        offer36.BuyerSavingsDzd.Should().Be(offer12.BuyerSavingsDzd * 3);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_DynamicCurve_AppliesSurchargeForShortMaturity()
    {
        // Arrange
        var invoiceDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoice = new InvoicePayable(
            InvoiceId: "INV-CURVE-01",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 1_000_000m,
            InvoiceDateUtc: invoiceDate,
            DueDateUtc: invoiceDate.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var targetDate = invoiceDate.AddDays(25); // 5 days early (short notice)

        // Act
        var standardOffer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetDate, 0.24m, applyDynamicCurve: false);
        var dynamicOffer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, targetDate, 0.24m, applyDynamicCurve: true);

        // Assert
        dynamicOffer.DiscountRatePct.Should().BeGreaterThan(standardOffer.DiscountRatePct);
        dynamicOffer.BuyerSavingsDzd.Should().BeGreaterThan(standardOffer.BuyerSavingsDzd);
        dynamicOffer.AnnualizedAprYieldPct.Should().BeGreaterThan(standardOffer.AnnualizedAprYieldPct);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_WhenUnapproved_ThrowsInvalidOperationException()
    {
        var invoice = new InvoicePayable(
            InvoiceId: "INV-UNAPPROVED",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 100_000m,
            InvoiceDateUtc: DateTime.UtcNow,
            DueDateUtc: DateTime.UtcNow.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: false // Unapproved
        );

        var act = () => _discountingEngine.CalculateEarlyPaymentOffer(invoice, DateTime.UtcNow.AddDays(5));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not approved for payment*");
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_WhenTargetDateEqualsDueDate_ThrowsArgumentException()
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-SAME-DAY",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 100_000m,
            InvoiceDateUtc: now.AddDays(-30),
            DueDateUtc: now,
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var act = () => _discountingEngine.CalculateEarlyPaymentOffer(invoice, now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*strictly prior to the invoice due date*");
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_WhenTargetDateAfterDueDate_ThrowsArgumentException()
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-PAST-DUE",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 100_000m,
            InvoiceDateUtc: now.AddDays(-60),
            DueDateUtc: now.AddDays(-10),
            PaymentTermsDays: 50,
            ApprovedForPayment: true
        );

        var act = () => _discountingEngine.CalculateEarlyPaymentOffer(invoice, now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*strictly prior to the invoice due date*");
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_WhenDaysEarlyExceedsPaymentTerms_ThrowsArgumentException()
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-EXCEED-TERMS",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 100_000m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(90),
            PaymentTermsDays: 30, // Terms is 30, but remaining days is 80
            ApprovedForPayment: true
        );

        var act = () => _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(10)); // Days early = 80 > 30

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed the invoice payment terms*");
    }

    #endregion

    #region 2. Boundary Conditions, Validation & Expiration

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void CalculateEarlyPaymentOffer_WhenInvoiceAmountIsZeroOrNegative_ThrowsArgumentException(decimal invalidAmount)
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-INVALID-AMT",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: invalidAmount,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var act = () => _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(10));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*strictly greater than zero*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.05)]
    [InlineData(3.5)]
    public void CalculateEarlyPaymentOffer_WhenAprTargetOutOfBounds_ThrowsArgumentOutOfRangeException(decimal invalidApr)
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-INVALID-APR",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 100_000m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var act = () => _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(10), annualAprTarget: invalidApr);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AcceptOffer_ValidOffer_TransitionsStatusToOfferAccepted()
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-ACCEPT-01",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 250_000m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(40),
            PaymentTermsDays: 40,
            ApprovedForPayment: true
        );

        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(10));
        var accepted = _discountingEngine.AcceptOffer(offer.OfferId, "SUPPLIER-DZ-99");

        accepted.Should().NotBeNull();
        accepted.OfferId.Should().Be(offer.OfferId);
        accepted.Status.Should().Be(InvoiceFundingStatus.OfferAccepted);

        var stored = _discountingEngine.GetOffer(offer.OfferId);
        stored!.Status.Should().Be(InvoiceFundingStatus.OfferAccepted);
    }

    [Fact]
    public void AcceptOffer_NonExistentOffer_ThrowsKeyNotFoundException()
    {
        var act = () => _discountingEngine.AcceptOffer("OFFER-DOES-NOT-EXIST", "SUPPLIER-99");

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public void AcceptOffer_AlreadyAcceptedOffer_IsIdempotent()
    {
        var now = DateTime.UtcNow;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-IDEMP-01",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 300_000m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(5));
        var firstAccept = _discountingEngine.AcceptOffer(offer.OfferId, "SUPPLIER-DZ-99");
        var secondAccept = _discountingEngine.AcceptOffer(offer.OfferId, "SUPPLIER-DZ-99");

        firstAccept.Status.Should().Be(InvoiceFundingStatus.OfferAccepted);
        secondAccept.Status.Should().Be(InvoiceFundingStatus.OfferAccepted);
        secondAccept.OfferId.Should().Be(firstAccept.OfferId);
    }

    [Fact]
    public void GetActiveOffers_OnlyReturnsPendingNonExpiredOffers()
    {
        var now = DateTime.UtcNow;
        var engine = new DynamicDiscountingEngine();

        var inv1 = new InvoicePayable("INV-ACTIVE-1", "B1", "S1", 100_000m, now, now.AddDays(30), 30, true);
        var inv2 = new InvoicePayable("INV-ACTIVE-2", "B1", "S1", 200_000m, now, now.AddDays(30), 30, true);

        var o1 = engine.CalculateEarlyPaymentOffer(inv1, now.AddDays(10));
        var o2 = engine.CalculateEarlyPaymentOffer(inv2, now.AddDays(10));

        engine.AcceptOffer(o1.OfferId, "S1");

        var activeOffers = engine.GetActiveOffers();
        activeOffers.Should().ContainSingle(o => o.OfferId == o2.OfferId);
        activeOffers.Should().NotContain(o => o.OfferId == o1.OfferId);
    }

    #endregion

    #region 3. Liquidity Allocation & Offer Generation

    [Fact]
    public void GenerateOffersForPayables_WithinLiquidityConstraint_AllocatesGreedilyByHighestYield()
    {
        var now = DateTime.UtcNow.Date;
        var targetDate = now.AddDays(1);

        // 3 Invoices with different remaining maturities (giving different APR yields under dynamic curve)
        var inv1 = new InvoicePayable("INV-A", "B1", "S1", 400_000m, now.AddDays(-10), now.AddDays(20), 30, true);
        var inv2 = new InvoicePayable("INV-B", "B1", "S1", 500_000m, now.AddDays(-5), now.AddDays(10), 15, true);
        var inv3 = new InvoicePayable("INV-C", "B1", "S1", 600_000m, now.AddDays(-2), now.AddDays(40), 42, true);

        // Total net payments would be around ~1,450,000 DZD. Let's provide 1,000,000 DZD liquidity.
        var offers = _discountingEngine.GenerateOffersForPayables(
            [inv1, inv2, inv3],
            availableLiquidityDzd: 1_000_000m,
            targetPaymentDateUtc: targetDate,
            annualAprTarget: 0.24m,
            applyDynamicCurve: true);

        offers.Should().NotBeEmpty();
        var totalAllocated = offers.Sum(o => o.NetPaymentAmountDzd);
        totalAllocated.Should().BeLessThanOrEqualTo(1_000_000m);

        // Offers should be sorted in descending order of yield / buyer savings
        offers.Should().BeInDescendingOrder(o => o.AnnualizedAprYieldPct);
    }

    [Fact]
    public void GenerateOffersForPayables_WhenLiquidityIsZero_ReturnsEmpty()
    {
        var now = DateTime.UtcNow.Date;
        var inv = new InvoicePayable("INV-ZERO-LIQ", "B1", "S1", 100_000m, now, now.AddDays(30), 30, true);

        var offers = _discountingEngine.GenerateOffersForPayables([inv], availableLiquidityDzd: 0m);

        offers.Should().BeEmpty();
    }

    [Fact]
    public void GenerateOffersForPayables_FiltersOutUnapprovedInvoices()
    {
        var now = DateTime.UtcNow.Date;
        var invApproved = new InvoicePayable("INV-APP", "B1", "S1", 100_000m, now, now.AddDays(30), 30, true);
        var invUnapproved = new InvoicePayable("INV-UNAPP", "B1", "S1", 100_000m, now, now.AddDays(30), 30, false);

        var offers = _discountingEngine.GenerateOffersForPayables([invApproved, invUnapproved], 1_000_000m);

        offers.Should().ContainSingle(o => o.InvoiceId == "INV-APP");
        offers.Should().NotContain(o => o.InvoiceId == "INV-UNAPP");
    }

    [Fact]
    public void GenerateOffersForPayables_FiltersOutPastDueInvoices()
    {
        var now = DateTime.UtcNow.Date;
        var invPastDue = new InvoicePayable("INV-PAST", "B1", "S1", 100_000m, now.AddDays(-60), now.AddDays(-5), 55, true);
        var invValid = new InvoicePayable("INV-VALID", "B1", "S1", 100_000m, now, now.AddDays(25), 25, true);

        var offers = _discountingEngine.GenerateOffersForPayables([invPastDue, invValid], 1_000_000m);

        offers.Should().ContainSingle(o => o.InvoiceId == "INV-VALID");
    }

    [Fact]
    public void GenerateOffersForPayables_InsufficientLiquidityForSecondInvoice_FundsOnlyFirst()
    {
        var now = DateTime.UtcNow.Date;
        var targetDate = now.AddDays(1);

        var inv1 = new InvoicePayable("INV-FIRST", "B1", "S1", 500_000m, now, now.AddDays(30), 30, true);
        var inv2 = new InvoicePayable("INV-SECOND", "B1", "S1", 500_000m, now, now.AddDays(30), 30, true);

        // Net payment for one is ~490,000 DZD. Provide 600,000 DZD liquidity (enough for only 1).
        var offers = _discountingEngine.GenerateOffersForPayables(
            [inv1, inv2],
            availableLiquidityDzd: 600_000m,
            targetPaymentDateUtc: targetDate);

        offers.Should().HaveCount(1);
        offers[0].NetPaymentAmountDzd.Should().BeLessThanOrEqualTo(600_000m);
    }

    [Fact]
    public void FactoringSummary_ReflectsCumulativeOperationsAndSavings()
    {
        var engine = new DynamicDiscountingEngine();
        var now = DateTime.UtcNow.Date;

        var inv = new InvoicePayable("INV-SUMM-1", "B1", "S1", 1_000_000m, now, now.AddDays(30), 30, true);
        var offer = engine.CalculateEarlyPaymentOffer(inv, now.AddDays(10));
        engine.AcceptOffer(offer.OfferId, "S1");

        var summary = engine.GetFactoringSummary();

        summary.TotalInvoicesVolumeDzd.Should().Be(1_000_000m);
        summary.TotalEarlyPaidDzd.Should().Be(offer.NetPaymentAmountDzd);
        summary.TotalBuyerSavingsDzd.Should().Be(offer.BuyerSavingsDzd);
        summary.CompletedAssignmentsCount.Should().Be(1);
    }

    #endregion

    #region 4. Buyer Savings & Net Amounts across DZD Currency Amounts

    [Fact]
    public void CalculateEarlyPaymentOffer_MicroInvoice_1000Dzd_RoundsToCentsAccurately()
    {
        var now = DateTime.UtcNow.Date;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-MICRO",
            BuyerTenantId: "BUYER-DZ",
            SupplierTenantId: "SUPPLIER-DZ",
            InvoiceAmountDzd: 1_000.00m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(15), annualAprTarget: 0.24m);

        // 15 days early: DiscountRate = 0.24 * (15/360) = 0.01 (1%)
        // BuyerSavings = 1,000 * 0.01 = 10.00 DZD
        // NetPayment = 990.00 DZD
        offer.BuyerSavingsDzd.Should().Be(10.00m);
        offer.NetPaymentAmountDzd.Should().Be(990.00m);
        (offer.NetPaymentAmountDzd + offer.BuyerSavingsDzd).Should().Be(invoice.InvoiceAmountDzd);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_LargeEnterpriseInvoice_50MillionDzd_MaintainsExactPrecision()
    {
        var now = DateTime.UtcNow.Date;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-ENTERPRISE-50M",
            BuyerTenantId: "BUYER-DZ-SONATRACH",
            SupplierTenantId: "SUPPLIER-DZ-ENGINEERING",
            InvoiceAmountDzd: 50_000_000.00m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(60),
            PaymentTermsDays: 60,
            ApprovedForPayment: true
        );

        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(30), annualAprTarget: 0.24m);

        // 30 days early: DiscountRate = 0.24 * (30/360) = 0.02 (2%)
        // BuyerSavings = 50,000,000 * 0.02 = 1,000,000.00 DZD
        // NetPayment = 49,000,000.00 DZD
        offer.BuyerSavingsDzd.Should().Be(1_000_000.00m);
        offer.NetPaymentAmountDzd.Should().Be(49_000_000.00m);
        (offer.NetPaymentAmountDzd + offer.BuyerSavingsDzd).Should().Be(50_000_000.00m);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_OddCentsAmount_RoundsAccuratelyWithoutDrift()
    {
        var now = DateTime.UtcNow.Date;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-ODD-CENTS",
            BuyerTenantId: "B1",
            SupplierTenantId: "S1",
            InvoiceAmountDzd: 1_234_567.89m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(45),
            PaymentTermsDays: 45,
            ApprovedForPayment: true
        );

        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(15), annualAprTarget: 0.18m);

        // Sum of NetPayment and BuyerSavings must strictly match the original invoice amount
        (offer.NetPaymentAmountDzd + offer.BuyerSavingsDzd).Should().Be(invoice.InvoiceAmountDzd);
    }

    [Fact]
    public void CalculateEarlyPaymentOffer_LongTermsMaturity_ComputesSubstantialSavings()
    {
        var now = DateTime.UtcNow.Date;
        var invoice = new InvoicePayable(
            InvoiceId: "INV-LONG-TERM",
            BuyerTenantId: "B1",
            SupplierTenantId: "S1",
            InvoiceAmountDzd: 10_000_000m,
            InvoiceDateUtc: now,
            DueDateUtc: now.AddDays(120),
            PaymentTermsDays: 120,
            ApprovedForPayment: true
        );

        // 90 days early out of 120
        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(30), annualAprTarget: 0.20m);

        // DiscountRate = 0.20 * (90/360) = 0.05 (5.0%)
        // BuyerSavings = 10,000,000 * 0.05 = 500,000 DZD
        offer.DaysEarly.Should().Be(90);
        offer.DiscountRatePct.Should().Be(5.0000m);
        offer.BuyerSavingsDzd.Should().Be(500_000.00m);
        offer.NetPaymentAmountDzd.Should().Be(9_500_000.00m);
    }

    [Fact]
    public void GetOffer_ExistingAndNonExisting_ReturnsCorrectly()
    {
        var now = DateTime.UtcNow.Date;
        var invoice = new InvoicePayable("INV-FIND", "B1", "S1", 100_000m, now, now.AddDays(30), 30, true);
        var offer = _discountingEngine.CalculateEarlyPaymentOffer(invoice, now.AddDays(10));

        var found = _discountingEngine.GetOffer(offer.OfferId);
        var notFound = _discountingEngine.GetOffer("NON-EXISTENT-ID");

        found.Should().NotBeNull();
        found!.OfferId.Should().Be(offer.OfferId);
        notFound.Should().BeNull();
    }

    #endregion

    #region 5. Partner Bank Assignment, Credit Lines & Non-Recourse Covenants

    [Fact]
    public void AssignInvoiceToBank_ValidInvoice_AssignsWithNonRecourseProtectionAndCalculatesFee()
    {
        var invoice = new InvoicePayable(
            InvoiceId: "INV-BANK-BNA-01",
            BuyerTenantId: "BUYER-DZ-01",
            SupplierTenantId: "SUPPLIER-DZ-99",
            InvoiceAmountDzd: 10_000_000.00m,
            InvoiceDateUtc: DateTime.UtcNow.AddDays(-10),
            DueDateUtc: DateTime.UtcNow.AddDays(50),
            PaymentTermsDays: 60,
            ApprovedForPayment: true
        );

        // Act: Assign to BNA_Algeria with 1.75% bank fee (0.0175)
        var assignment = _liquidityPool.AssignInvoiceToBank(
            invoice,
            PartnerBankCode.BNA_Algeria,
            bankDiscountFeePct: 0.0175m);

        // Assert:
        // Bank Fee = 10,000,000 * 0.0175 = 175,000.00 DZD
        // Net Advance = 9,825,000.00 DZD
        assignment.Should().NotBeNull();
        assignment.InvoiceId.Should().Be("INV-BANK-BNA-01");
        assignment.PartnerBank.Should().Be(PartnerBankCode.BNA_Algeria);
        assignment.FinancedAmountDzd.Should().Be(9_825_000.00m);
        assignment.BankFeeDzd.Should().Be(175_000.00m);
        assignment.NonRecourseProtection.Should().BeTrue();
        assignment.Status.Should().Be(InvoiceFundingStatus.AssignedToBank);

        var retrieved = _liquidityPool.GetBankFundingStatus(assignment.AssignmentId);
        retrieved.Should().NotBeNull();
        retrieved!.AssignmentId.Should().Be(assignment.AssignmentId);
    }

    [Fact]
    public void AssignInvoiceToBank_DeductsFromAvailableLiquidityAndIncreasesUtilized()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        var initialSummary = pool.GetConsortiumLiquiditySummary();
        var bnaInitial = initialSummary.BankFacilities.First(b => b.BankCode == PartnerBankCode.BNA_Algeria);

        var invoice = new InvoicePayable(
            InvoiceId: "INV-CAPACITY-CHECK",
            BuyerTenantId: "BUYER-1",
            SupplierTenantId: "SUPP-1",
            InvoiceAmountDzd: 25_000_000m,
            InvoiceDateUtc: DateTime.UtcNow,
            DueDateUtc: DateTime.UtcNow.AddDays(60),
            PaymentTermsDays: 60,
            ApprovedForPayment: true
        );

        pool.AssignInvoiceToBank(invoice, PartnerBankCode.BNA_Algeria, 0.0175m);

        var updatedSummary = pool.GetConsortiumLiquiditySummary();
        var bnaUpdated = updatedSummary.BankFacilities.First(b => b.BankCode == PartnerBankCode.BNA_Algeria);

        bnaUpdated.UtilizedCreditDzd.Should().Be(bnaInitial.UtilizedCreditDzd + 25_000_000m);
        bnaUpdated.AvailableLiquidityDzd.Should().Be(bnaInitial.AvailableLiquidityDzd - 25_000_000m);
    }

    [Fact]
    public void AssignInvoiceToBank_WhenExceedingCreditLimit_ThrowsInvalidOperationException()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        // BNA default limit is 500,000,000 DZD. Attempting 500,000,001 DZD must be rejected.
        var massiveInvoice = new InvoicePayable(
            InvoiceId: "INV-TOO-LARGE",
            BuyerTenantId: "B1",
            SupplierTenantId: "S1",
            InvoiceAmountDzd: 500_000_001m,
            InvoiceDateUtc: DateTime.UtcNow,
            DueDateUtc: DateTime.UtcNow.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: true
        );

        var act = () => pool.AssignInvoiceToBank(massiveInvoice, PartnerBankCode.BNA_Algeria, 0.0175m);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*insufficient credit facility liquidity*");
    }

    [Fact]
    public void AssignInvoiceToBank_WhenUnapprovedInvoice_ThrowsInvalidOperationException()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        var unapproved = new InvoicePayable(
            InvoiceId: "INV-BANK-UNAPP",
            BuyerTenantId: "B1",
            SupplierTenantId: "S1",
            InvoiceAmountDzd: 5_000_000m,
            InvoiceDateUtc: DateTime.UtcNow,
            DueDateUtc: DateTime.UtcNow.AddDays(30),
            PaymentTermsDays: 30,
            ApprovedForPayment: false // Unapproved
        );

        var act = () => pool.AssignInvoiceToBank(unapproved, PartnerBankCode.CPA_Algeria, 0.0185m);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not approved for payment*");
    }

    [Fact]
    public void AssignInvoiceToBank_MultipleBanks_TracksIndependentCreditFacilities()
    {
        var pool = new ReverseFactoringLiquidityPoolService();

        var invCpa = new InvoicePayable("INV-CPA", "B1", "S1", 10_000_000m, DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 30, true);
        var invDib = new InvoicePayable("INV-DIB", "B1", "S2", 20_000_000m, DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 30, true);

        pool.AssignInvoiceToBank(invCpa, PartnerBankCode.CPA_Algeria, 0.0185m);
        pool.AssignInvoiceToBank(invDib, PartnerBankCode.DubaiIslamicBank_UAE, 0.0150m);

        var summary = pool.GetConsortiumLiquiditySummary();
        var cpa = summary.BankFacilities.First(b => b.BankCode == PartnerBankCode.CPA_Algeria);
        var dib = summary.BankFacilities.First(b => b.BankCode == PartnerBankCode.DubaiIslamicBank_UAE);

        cpa.UtilizedCreditDzd.Should().Be(10_000_000m);
        dib.UtilizedCreditDzd.Should().Be(20_000_000m);
    }

    [Fact]
    public void SettleAssignment_RestoresBankCreditCapacity()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        var invoice = new InvoicePayable("INV-SETTLE", "B1", "S1", 15_000_000m, DateTime.UtcNow, DateTime.UtcNow.AddDays(45), 45, true);

        var assignment = pool.AssignInvoiceToBank(invoice, PartnerBankCode.SocGen_Algeria, 0.0210m);
        var beforeSettle = pool.GetConsortiumLiquiditySummary().BankFacilities.First(b => b.BankCode == PartnerBankCode.SocGen_Algeria);
        beforeSettle.UtilizedCreditDzd.Should().Be(15_000_000m);

        var settled = pool.SettleAssignment(assignment.AssignmentId);
        settled.Status.Should().Be(InvoiceFundingStatus.SettledEarly);

        var afterSettle = pool.GetConsortiumLiquiditySummary().BankFacilities.First(b => b.BankCode == PartnerBankCode.SocGen_Algeria);
        afterSettle.UtilizedCreditDzd.Should().Be(0m);
    }

    [Fact]
    public void MarkDefaulted_BuyerDefaultUnderNonRecourse_SupplierProtectedAndLossAbsorbedByBank()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        var invoice = new InvoicePayable("INV-DEFAULT", "BUYER-INSOLVENT", "SUPP-VICTIM", 8_000_000m, DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 30, true);

        var assignment = pool.AssignInvoiceToBank(invoice, PartnerBankCode.BNP_Algeria, 0.0200m);
        var defaulted = pool.MarkDefaulted(assignment.AssignmentId);

        defaulted.Status.Should().Be(InvoiceFundingStatus.Defaulted);
        // Non-recourse protection remains active to guarantee supplier is not liable
        defaulted.NonRecourseProtection.Should().BeTrue();
        defaulted.FinancedAmountDzd.Should().Be(7_840_000.00m); // Net payout intact
    }

    [Fact]
    public void GetConsortiumLiquiditySummary_ReflectsConsortiumAggregates()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        var summary = pool.GetConsortiumLiquiditySummary();

        // 500M (BNA) + 400M (CPA) + 350M (SocGen) + 300M (BNP) + 600M (DubaiIslamic) = 2,150,000,000 DZD
        summary.TotalConsortiumCapacityDzd.Should().Be(2_150_000_000.00m);
        summary.TotalUtilizedDzd.Should().Be(0m);
        summary.AvailableConsortiumLiquidityDzd.Should().Be(2_150_000_000.00m);
        summary.BankFacilities.Should().HaveCount(5);
    }

    #endregion

    #region 6. Supplier Cash Urgency Profiling & Multi-Armed Bandit

    [Fact]
    public void Bandit_PriorsInitializedUniformly_AlphaBetaEqualOne()
    {
        var supplierId = "SUPPLIER-BANDIT-INIT";
        var state = _bandit.GetSupplierBanditState(supplierId);

        state.Should().NotBeNull();
        state.SupplierId.Should().Be(supplierId);
        state.Arms.Should().HaveCount(5);

        foreach (var arm in state.Arms)
        {
            arm.Alpha.Should().Be(1.0);
            arm.Beta.Should().Be(1.0);
            arm.TotalPulls.Should().Be(0);
            arm.Successes.Should().Be(0);
            arm.MeanAcceptanceRate.Should().Be(0.5);
        }
    }

    [Fact]
    public void Bandit_RecordAcceptedDecision_IncreasesAlphaByOne()
    {
        var supplierId = "SUPPLIER-ACC-TEST";
        _bandit.RecordSupplierDecision(supplierId, offeredDiscountRate: 0.020m, accepted: true);

        var state = _bandit.GetSupplierBanditState(supplierId);
        var arm20 = state.Arms.First(a => a.DiscountRatePct == 2.00m);

        arm20.Alpha.Should().Be(2.0);
        arm20.Beta.Should().Be(1.0);
        arm20.TotalPulls.Should().Be(1);
        arm20.Successes.Should().Be(1);
    }

    [Fact]
    public void Bandit_RecordRejectedDecision_IncreasesBetaByOne()
    {
        var supplierId = "SUPPLIER-REJ-TEST";
        _bandit.RecordSupplierDecision(supplierId, offeredDiscountRate: 0.025m, accepted: false);

        var state = _bandit.GetSupplierBanditState(supplierId);
        var arm25 = state.Arms.First(a => a.DiscountRatePct == 2.50m);

        arm25.Alpha.Should().Be(1.0);
        arm25.Beta.Should().Be(2.0);
        arm25.TotalPulls.Should().Be(1);
        arm25.Successes.Should().Be(0);
    }

    [Fact]
    public void Bandit_RecordExpiredDecision_IncreasesBetaByHalf()
    {
        var supplierId = "SUPPLIER-EXP-TEST";
        _bandit.RecordSupplierOutcome(supplierId, offeredDiscountRate: 0.030m, SupplierOfferOutcome.Expired);

        var state = _bandit.GetSupplierBanditState(supplierId);
        var arm30 = state.Arms.First(a => a.DiscountRatePct == 3.00m);

        arm30.Alpha.Should().Be(1.0);
        arm30.Beta.Should().Be(1.5);
        arm30.TotalPulls.Should().Be(1);
    }

    [Fact]
    public void Bandit_CriticalUrgencySupplier_RecommendsHigherDiscountRate()
    {
        var criticalSupplier = new SupplierCashUrgencyProfile(
            SupplierId: "SUPP-CRITICAL",
            UrgencyScore: 0.95,
            HistoricalAcceptanceRate: 0.90,
            WorkingCapitalBand: "Critical",
            RecommendedDiscountMaxPct: 3.0m
        );

        var recommendation = _bandit.GetOptimalDiscountRecommendation("SUPP-CRITICAL", criticalSupplier);

        recommendation.Should().NotBeNull();
        recommendation.RecommendedDiscountRatePct.Should().BeGreaterThanOrEqualTo(2.00m);
        recommendation.UrgencyBand.Should().Be("Critical");
        recommendation.StrategyRationale.Should().Contain("Critical");
    }

    [Fact]
    public void Bandit_EnforcesRecommendedDiscountMaxPct_NeverExceedsCeiling()
    {
        var cappedSupplier = new SupplierCashUrgencyProfile(
            SupplierId: "SUPP-CAPPED",
            UrgencyScore: 0.85,
            HistoricalAcceptanceRate: 0.70,
            WorkingCapitalBand: "Moderate",
            RecommendedDiscountMaxPct: 1.50m // Hard policy ceiling at 1.5%
        );

        // Run multiple recommendations to test stochastic Thompson sampling
        for (var i = 0; i < 10; i++)
        {
            var rate = _bandit.PredictOptimalDiscountRate("SUPP-CAPPED", cappedSupplier);
            rate.Should().BeLessThanOrEqualTo(0.015m);
        }
    }

    [Fact]
    public void UpdateBankCreditLimit_DynamicallyIncreasesCapacity()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        pool.UpdateBankCreditLimit(PartnerBankCode.BNA_Algeria, 750_000_000m);

        var summary = pool.GetConsortiumLiquiditySummary();
        var bna = summary.BankFacilities.First(b => b.BankCode == PartnerBankCode.BNA_Algeria);

        bna.TotalCreditLineDzd.Should().Be(750_000_000m);
        bna.AvailableLiquidityDzd.Should().Be(750_000_000m);
    }

    #endregion

    #region 7. Concurrency & ServiceCollection Registration (Rule 5 Compliance)

    [Fact]
    public void DynamicDiscountingEngine_ConcurrentOfferGeneration_ThreadSafe()
    {
        var engine = new DynamicDiscountingEngine();
        var now = DateTime.UtcNow.Date;
        const int iterations = 30;

        Parallel.For(0, iterations, i =>
        {
            var invoice = new InvoicePayable(
                InvoiceId: $"INV-CONC-{i}",
                BuyerTenantId: "BUYER-PARALLEL",
                SupplierTenantId: $"SUPPLIER-{i % 5}",
                InvoiceAmountDzd: 100_000m + (i * 1000m),
                InvoiceDateUtc: now,
                DueDateUtc: now.AddDays(30),
                PaymentTermsDays: 30,
                ApprovedForPayment: true
            );

            var offer = engine.CalculateEarlyPaymentOffer(invoice, now.AddDays(10));
            offer.Should().NotBeNull();
        });

        var summary = engine.GetFactoringSummary();
        summary.ActiveOffersCount.Should().Be(iterations);
    }

    [Fact]
    public void ReverseFactoringLiquidityPool_ConcurrentAssignments_MaintainsCreditBalanceInvariant()
    {
        var pool = new ReverseFactoringLiquidityPoolService();
        const int threadCount = 10;
        const decimal invoiceAmount = 5_000_000m;

        Parallel.For(0, threadCount, i =>
        {
            var invoice = new InvoicePayable(
                InvoiceId: $"INV-POOL-CONC-{i}",
                BuyerTenantId: "B1",
                SupplierTenantId: $"S-{i}",
                InvoiceAmountDzd: invoiceAmount,
                InvoiceDateUtc: DateTime.UtcNow,
                DueDateUtc: DateTime.UtcNow.AddDays(45),
                PaymentTermsDays: 45,
                ApprovedForPayment: true
            );

            pool.AssignInvoiceToBank(invoice, PartnerBankCode.DubaiIslamicBank_UAE, 0.0150m);
        });

        var summary = pool.GetConsortiumLiquiditySummary();
        var dib = summary.BankFacilities.First(b => b.BankCode == PartnerBankCode.DubaiIslamicBank_UAE);

        // 10 * 5,000,000 = 50,000,000 DZD utilized
        dib.UtilizedCreditDzd.Should().Be(50_000_000m);
        dib.AvailableLiquidityDzd.Should().Be(600_000_000m - 50_000_000m);
    }

    [Fact]
    public void Bandit_ConcurrentFeedback_ThreadSafeUpdates()
    {
        var bandit = new SupplierDiscountAcceptanceBandit();
        const string supplierId = "SUPP-CONC-BANDIT";
        const int feedbackCount = 40;

        Parallel.For(0, feedbackCount, i =>
        {
            var accepted = i % 2 == 0;
            bandit.RecordSupplierDecision(supplierId, offeredDiscountRate: 0.020m, accepted: accepted);
        });

        var state = bandit.GetSupplierBanditState(supplierId);
        var arm20 = state.Arms.First(a => a.DiscountRatePct == 2.00m);

        arm20.TotalPulls.Should().Be(feedbackCount);
        arm20.Successes.Should().Be(feedbackCount / 2);
        arm20.Alpha.Should().Be(1.0 + (feedbackCount / 2));
        arm20.Beta.Should().Be(1.0 + (feedbackCount / 2));
    }

    [Fact]
    public void AddFactoringServices_RegistersAllServicesAsSingletons_WithoutBuildServiceProvider()
    {
        // Assert directly on ServiceCollection descriptors (Rule 5 requirement)
        var services = new ServiceCollection();

        services.AddFactoringServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDynamicDiscountingEngine) &&
            d.ImplementationType == typeof(DynamicDiscountingEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IReverseFactoringLiquidityPoolService) &&
            d.ImplementationType == typeof(ReverseFactoringLiquidityPoolService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISupplierDiscountAcceptanceBandit) &&
            d.ImplementationType == typeof(SupplierDiscountAcceptanceBandit) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
