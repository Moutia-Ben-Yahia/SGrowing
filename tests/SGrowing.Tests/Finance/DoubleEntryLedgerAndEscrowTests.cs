using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.Ledger;
using Xunit;

namespace SGrowing.Tests.Finance;

public class DoubleEntryLedgerAndEscrowTests
{
    private readonly DoubleEntryLedgerService _ledgerService;
    private readonly NorthAfricanPayoutAdapter _northAfricanAdapter;
    private readonly InternationalPayoutAdapter _internationalAdapter;
    private readonly VirtualWalletPayoutService _walletService;
    private readonly EscrowSettlementEngine _escrowEngine;

    public DoubleEntryLedgerAndEscrowTests()
    {
        _ledgerService = new DoubleEntryLedgerService();
        _northAfricanAdapter = new NorthAfricanPayoutAdapter();
        _internationalAdapter = new InternationalPayoutAdapter();

        List<IPayoutGatewayAdapter> adapters = [_northAfricanAdapter, _internationalAdapter];
        _walletService = new VirtualWalletPayoutService(adapters, _ledgerService);
        _escrowEngine = new EscrowSettlementEngine(_ledgerService, _walletService);
    }

    #region 1. Double-Entry Invariant & Balance Tests

    [Fact]
    public async Task PostEntry_WhenDebitsEqualCredits_ShouldPostSuccessfullyAndGenerateSequenceNumber()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "InitialDeposit",
            ReferenceId: "DEP-001",
            Description: "Cash float deposit",
            Currency: "DZD",
            Lines:
            [
                new JournalLineRequest(StandardAccountCodes.CashAndCourierFloat, Debit: 5000m, Credit: 0m, Description: "Float deposit"),
                new JournalLineRequest(StandardAccountCodes.CustomerEscrowHeld, Debit: 0m, Credit: 5000m, Description: "Customer escrow hold")
            ]
        );

        // Act
        var entry = await _ledgerService.PostEntryAsync(request);

        // Assert
        entry.Should().NotBeNull();
        entry.SequenceNumber.Should().Be(1);
        entry.TotalDebits.Should().Be(5000m);
        entry.TotalCredits.Should().Be(5000m);
        entry.IntegrityHash.Should().NotBeNullOrWhiteSpace();
        entry.IntegrityHash.Length.Should().Be(64); // SHA-256 hex string
        entry.PreviousHash.Should().BeNull();
    }

    [Fact]
    public async Task PostEntry_WhenUnbalancedByPositiveOneCent_ShouldThrowUnbalancedJournalEntryException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "UnbalancedTest",
            ReferenceId: "UNBAL-001",
            Description: "Imbalance test +0.01",
            Currency: "DZD",
            Lines:
            [
                new JournalLineRequest(StandardAccountCodes.CashAndCourierFloat, Debit: 100.01m, Credit: 0m),
                new JournalLineRequest(StandardAccountCodes.CustomerEscrowHeld, Debit: 0m, Credit: 100.00m)
            ]
        );

        // Act
        var act = async () => await _ledgerService.PostEntryAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<UnbalancedJournalEntryException>();
        ex.Which.TotalDebits.Should().Be(100.01m);
        ex.Which.TotalCredits.Should().Be(100.00m);
        ex.Which.Difference.Should().Be(0.01m);
        ex.Which.ReferenceId.Should().Be("UNBAL-001");
    }

    [Fact]
    public async Task PostEntry_WhenUnbalancedByNegativeOneCent_ShouldThrowUnbalancedJournalEntryException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "UnbalancedTest",
            ReferenceId: "UNBAL-002",
            Description: "Imbalance test -0.01",
            Currency: "DZD",
            Lines:
            [
                new JournalLineRequest(StandardAccountCodes.CashAndCourierFloat, Debit: 100.00m, Credit: 0m),
                new JournalLineRequest(StandardAccountCodes.CustomerEscrowHeld, Debit: 0m, Credit: 100.01m)
            ]
        );

        // Act
        var act = async () => await _ledgerService.PostEntryAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<UnbalancedJournalEntryException>();
        ex.Which.Difference.Should().Be(0.01m);
    }

    [Fact]
    public async Task PostEntry_WhenMajorImbalance_ShouldThrowWithExactImbalanceDetails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "MajorImbalance",
            ReferenceId: "UNBAL-003",
            Description: "Major imbalance test",
            Currency: "DZD",
            Lines:
            [
                new JournalLineRequest(StandardAccountCodes.CashAndCourierFloat, Debit: 10000m, Credit: 0m),
                new JournalLineRequest(StandardAccountCodes.MerchantAccountsPayable, Debit: 0m, Credit: 7500m)
            ]
        );

        // Act
        var act = async () => await _ledgerService.PostEntryAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<UnbalancedJournalEntryException>();
        ex.Which.TotalDebits.Should().Be(10000m);
        ex.Which.TotalCredits.Should().Be(7500m);
        ex.Which.Difference.Should().Be(2500m);
    }

    [Fact]
    public async Task PostEntry_WhenLinesHaveNegativeAmounts_ShouldThrowArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "NegativeLineTest",
            ReferenceId: "NEG-001",
            Description: "Negative amounts are illegal in double-entry",
            Currency: "DZD",
            Lines:
            [
                new JournalLineRequest(StandardAccountCodes.CashAndCourierFloat, Debit: -100m, Credit: 0m),
                new JournalLineRequest(StandardAccountCodes.CustomerEscrowHeld, Debit: 0m, Credit: -100m)
            ]
        );

        // Act
        var act = async () => await _ledgerService.PostEntryAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public async Task PostEntry_WhenBothDebitAndCreditOnSameLine_ShouldThrowArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "BothDebitAndCredit",
            ReferenceId: "BOTH-001",
            Description: "A line cannot have both debit and credit",
            Currency: "DZD",
            Lines:
            [
                new JournalLineRequest(StandardAccountCodes.CashAndCourierFloat, Debit: 100m, Credit: 50m),
                new JournalLineRequest(StandardAccountCodes.CustomerEscrowHeld, Debit: 0m, Credit: 50m)
            ]
        );

        // Act
        var act = async () => await _ledgerService.PostEntryAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot have both debit and credit*");
    }

    [Fact]
    public async Task PostEntry_WhenNoLinesProvided_ShouldThrowArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new PostJournalEntryRequest(
            TenantId: tenantId,
            ReferenceType: "EmptyLines",
            ReferenceId: "EMPTY-001",
            Description: "No lines",
            Currency: "DZD",
            Lines: []
        );

        // Act
        var act = async () => await _ledgerService.PostEntryAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IntegrityChain_WhenMultipleEntriesPosted_ShouldFormTamperEvidentHashChain()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act - Post 4 consecutive balanced entries
        var e1 = await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "Ref1", "ID-1", "Entry 1",
            [new(StandardAccountCodes.CashAndCourierFloat, 100m, 0m), new(StandardAccountCodes.CustomerEscrowHeld, 0m, 100m)]));

        var e2 = await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "Ref2", "ID-2", "Entry 2",
            [new(StandardAccountCodes.CashAndCourierFloat, 200m, 0m), new(StandardAccountCodes.CustomerEscrowHeld, 0m, 200m)]));

        var e3 = await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "Ref3", "ID-3", "Entry 3",
            [new(StandardAccountCodes.CashAndCourierFloat, 300m, 0m), new(StandardAccountCodes.CustomerEscrowHeld, 0m, 300m)]));

        // Assert
        e1.SequenceNumber.Should().Be(1);
        e1.PreviousHash.Should().BeNull();

        e2.SequenceNumber.Should().Be(2);
        e2.PreviousHash.Should().Be(e1.IntegrityHash);

        e3.SequenceNumber.Should().Be(3);
        e3.PreviousHash.Should().Be(e2.IntegrityHash);

        var isChainValid = await _ledgerService.VerifyIntegrityChainAsync(tenantId);
        isChainValid.Should().BeTrue();
    }

    #endregion

    #region 2. Account Balance & Normal Balance Rules

    [Fact]
    public async Task GetAccountBalance_ForAssetAccounts_NormalBalanceIsDebitsMinusCredits()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Cash Float (1000): Debit 12,000 DZD
        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "Deposit", "DEP-1", "Debit float",
            [new(StandardAccountCodes.CashAndCourierFloat, 12000m, 0m), new(StandardAccountCodes.CustomerEscrowHeld, 0m, 12000m)]));

        // Cash Float (1000): Credit 4,000 DZD
        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "Disbursement", "DIS-1", "Credit float",
            [new(StandardAccountCodes.MerchantAccountsPayable, 4000m, 0m), new(StandardAccountCodes.CashAndCourierFloat, 0m, 4000m)]));

        // Act
        var balance = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CashAndCourierFloat, "DZD");
        var detail = await _ledgerService.GetAccountBalanceDetailAsync(tenantId, StandardAccountCodes.CashAndCourierFloat, "DZD");

        // Assert: 12000 - 4000 = 8000 (Asset normal balance is Debit)
        balance.Should().Be(8000m);
        detail.Category.Should().Be(AccountCategory.Asset);
        detail.TotalDebits.Should().Be(12000m);
        detail.TotalCredits.Should().Be(4000m);
        detail.NormalBalance.Should().Be(8000m);
    }

    [Fact]
    public async Task GetAccountBalance_ForLiabilityAccounts_NormalBalanceIsCreditsMinusDebits()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Merchant Accounts Payable (2000): Credit 25,000 DZD
        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "CreditPayable", "PAY-1", "Order completed payable",
            [new(StandardAccountCodes.CustomerEscrowHeld, 25000m, 0m), new(StandardAccountCodes.MerchantAccountsPayable, 0m, 25000m)]));

        // Merchant Accounts Payable (2000): Debit 5,000 DZD (Payout)
        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "PayoutPayable", "PAY-2", "Payout made",
            [new(StandardAccountCodes.MerchantAccountsPayable, 5000m, 0m), new(StandardAccountCodes.CashAndCourierFloat, 0m, 5000m)]));

        // Act
        var balance = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.MerchantAccountsPayable, "DZD");
        var detail = await _ledgerService.GetAccountBalanceDetailAsync(tenantId, StandardAccountCodes.MerchantAccountsPayable, "DZD");

        // Assert: 25000 - 5000 = 20000 (Liability normal balance is Credit)
        balance.Should().Be(20000m);
        detail.Category.Should().Be(AccountCategory.Liability);
        detail.TotalCredits.Should().Be(25000m);
        detail.TotalDebits.Should().Be(5000m);
        detail.NormalBalance.Should().Be(20000m);
    }

    [Fact]
    public async Task GetAccountBalance_ForRevenueAndExpense_NormalBalancesFollowAccountingRules()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Credit Revenue (4000): 3,500 DZD
        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "PlatformTakeRate", "REV-1", "Platform fee earned",
            [new(StandardAccountCodes.CustomerEscrowHeld, 3500m, 0m), new(StandardAccountCodes.PlatformSaaSTakeRateRevenue, 0m, 3500m)]));

        // Debit Expense (5000): 1,200 DZD
        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "FreightExpense", "EXP-1", "Return logistics cost",
            [new(StandardAccountCodes.ShippingAndFreightExpense, 1200m, 0m), new(StandardAccountCodes.CourierRemittancePayable, 0m, 1200m)]));

        // Act
        var revDetail = await _ledgerService.GetAccountBalanceDetailAsync(tenantId, StandardAccountCodes.PlatformSaaSTakeRateRevenue, "DZD");
        var expDetail = await _ledgerService.GetAccountBalanceDetailAsync(tenantId, StandardAccountCodes.ShippingAndFreightExpense, "DZD");

        // Assert
        revDetail.Category.Should().Be(AccountCategory.Revenue);
        revDetail.NormalBalance.Should().Be(3500m); // Credit normal balance

        expDetail.Category.Should().Be(AccountCategory.Expense);
        expDetail.NormalBalance.Should().Be(1200m); // Debit normal balance
    }

    [Fact]
    public async Task ReverseEntry_ShouldCreateInverseEntryAndLinkReversalIds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var origEntry = await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "Original", "ORIG-1", "Original transaction",
            [
                new(StandardAccountCodes.CashAndCourierFloat, 5000m, 0m),
                new(StandardAccountCodes.CustomerEscrowHeld, 0m, 5000m)
            ]));

        // Act
        var reversalEntry = await _ledgerService.ReverseEntryAsync(origEntry.EntryId, "Customer cancelled order");

        // Assert
        reversalEntry.Should().NotBeNull();
        reversalEntry.ReversalOfEntryId.Should().Be(origEntry.EntryId);

        // Inverse lines: 1100 debited, 1000 credited
        reversalEntry.Lines.Should().Contain(l => l.AccountCode == StandardAccountCodes.CustomerEscrowHeld && l.Debit == 5000m);
        reversalEntry.Lines.Should().Contain(l => l.AccountCode == StandardAccountCodes.CashAndCourierFloat && l.Credit == 5000m);

        // Check original entry was updated
        var updatedOrig = await _ledgerService.GetEntryByIdAsync(origEntry.EntryId);
        updatedOrig.Should().NotBeNull();
        updatedOrig!.IsReversed.Should().BeTrue();
        updatedOrig.ReversedByEntryId.Should().Be(reversalEntry.EntryId);

        // Account balances should now net to 0
        var floatBalance = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CashAndCourierFloat, "DZD");
        var escrowBalance = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CustomerEscrowHeld, "DZD");
        floatBalance.Should().Be(0m);
        escrowBalance.Should().Be(0m);
    }

    [Fact]
    public async Task ReverseEntry_WhenAlreadyReversed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entry = await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "DoubleRev", "REV-X", "Testing double reversal",
            [new(StandardAccountCodes.CashAndCourierFloat, 100m, 0m), new(StandardAccountCodes.CustomerEscrowHeld, 0m, 100m)]));

        await _ledgerService.ReverseEntryAsync(entry.EntryId, "First reversal");

        // Act
        var act = async () => await _ledgerService.ReverseEntryAsync(entry.EntryId, "Attempt second reversal");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been reversed*");
    }

    [Fact]
    public async Task TrialBalance_AcrossMultipleTransactions_ShouldBePerfectlyBalanced()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "T1", "1", "T1",
            [new(StandardAccountCodes.CashAndCourierFloat, 10000m, 0m), new(StandardAccountCodes.CustomerEscrowHeld, 0m, 10000m)]));

        await _ledgerService.PostEntryAsync(new PostJournalEntryRequest(
            tenantId, "T2", "2", "T2",
            [
                new(StandardAccountCodes.CustomerEscrowHeld, 10000m, 0m),
                new(StandardAccountCodes.MerchantAccountsPayable, 0m, 8000m),
                new(StandardAccountCodes.CourierRemittancePayable, 0m, 1500m),
                new(StandardAccountCodes.PlatformSaaSTakeRateRevenue, 0m, 500m)
            ]));

        // Act
        var trialBalance = await _ledgerService.GetTrialBalanceAsync(tenantId, "DZD");

        // Assert
        trialBalance.IsBalanced.Should().BeTrue();
        trialBalance.TotalDebitBalances.Should().Be(trialBalance.TotalCreditBalances);
        trialBalance.TotalDebitBalances.Should().Be(10000m);
    }

    #endregion

    #region 3. Escrow Settlement Engine Tests (Stages 1, 2, 3)

    [Fact]
    public void CalculateSettlementSplit_ShouldGuaranteeZeroRoundingLeakage()
    {
        // Arrange: Unfavorable fractions (3333.33 DZD, 5% take rate, 7.5% affiliate commission, 650 DZD courier fee)
        decimal gross = 3333.33m;
        decimal courier = 650.00m;
        decimal takeRate = 0.05m;
        decimal affiliateRate = 0.075m;

        // Act
        var split = _escrowEngine.CalculateSettlementSplit(gross, courier, takeRate, affiliateRate, "DZD");

        // Assert: Zero penny leakage
        (split.MerchantNet + split.CourierFee + split.AffiliateCommission + split.PlatformTakeRateFee).Should().Be(gross);
        split.GrossAmount.Should().Be(gross);
        split.CourierFee.Should().Be(650.00m);
        split.PlatformTakeRateFee.Should().Be(166.67m); // Math.Round(3333.33 * 0.05, 2)
        split.AffiliateCommission.Should().Be(250.00m); // Math.Round(3333.33 * 0.075, 2)
        split.MerchantNet.Should().Be(2266.66m); // 3333.33 - (650 + 166.67 + 250)
    }

    [Fact]
    public async Task Escrow_LockOnCheckout_ShouldHoldCustomerCashAndIncreaseRestrictedEscrow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-ALGIERS-101";
        var request = new EscrowLockRequest(
            TenantId: tenantId,
            OrderId: orderId,
            CustomerId: "CUST-999",
            MerchantId: "MERCH-404",
            CourierId: "YALIDINE-EXP",
            AffiliateId: "INFLUENCER-DZ",
            GrossAmount: 14500m,
            Currency: "DZD",
            Notes: "COD checkout in Bab Ezzouar"
        );

        // Act
        var escrowRecord = await _escrowEngine.LockOnCheckoutAsync(request);

        // Assert
        escrowRecord.Should().NotBeNull();
        escrowRecord.Status.Should().Be(EscrowStatus.Locked);
        escrowRecord.GrossAmount.Should().Be(14500m);

        // Check Ledger Accounts
        var escrowHeld = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CustomerEscrowHeld, "DZD");
        var cashFloat = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CashAndCourierFloat, "DZD");

        escrowHeld.Should().Be(14500m); // Restricted cash held
        cashFloat.Should().Be(14500m); // Cash in transit / courier float
    }

    [Fact]
    public async Task Escrow_LockOnCheckout_ShouldSegregateMerchantWalletBalanceIntoEscrowLocked()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-ORAN-202";
        var merchantId = "MERCH-ORAN-1";

        var request = new EscrowLockRequest(
            TenantId: tenantId,
            OrderId: orderId,
            CustomerId: "CUST-10",
            MerchantId: merchantId,
            CourierId: "ZR-EXPRESS",
            AffiliateId: null,
            GrossAmount: 8500m,
            Currency: "DZD"
        );

        // Act
        await _escrowEngine.LockOnCheckoutAsync(request);

        // Assert
        var wallet = await _walletService.FindWalletByEntityAsync(tenantId, merchantId, "DZD");
        wallet.Should().NotBeNull();
        wallet!.AvailableBalance.Should().Be(0m);
        wallet.EscrowLockedBalance.Should().Be(8500m);
        wallet.TotalBalance.Should().Be(8500m);
    }

    [Fact]
    public async Task Escrow_ReleaseOnProofOfDelivery_ShouldExecuteFourWaySettlementSplit()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-CONSTANTINE-303";
        var merchantId = "MERCH-CONST-1";
        var courierId = "MAYSTRO-DELIV";
        var affiliateId = "AFF-TIKTOK-DZ";

        // Stage 1: Checkout lock 10,000 DZD
        await _escrowEngine.LockOnCheckoutAsync(new EscrowLockRequest(
            TenantId: tenantId,
            OrderId: orderId,
            CustomerId: "CUST-33",
            MerchantId: merchantId,
            CourierId: courierId,
            AffiliateId: affiliateId,
            GrossAmount: 10000m,
            Currency: "DZD"
        ));

        // Stage 2: POD confirmation
        var podRequest = new ProofOfDeliveryConfirmationRequest(
            TenantId: tenantId,
            OrderId: orderId,
            TrackingNumber: "TRK-987654321",
            ProofSignatureOrPin: "OTP-4829",
            DeliveredAtUtc: DateTimeOffset.UtcNow,
            CourierFee: 800m,
            PlatformTakeRatePercentage: 0.05m, // 5% -> 500 DZD
            AffiliateCommissionPercentage: 0.07m // 7% -> 700 DZD
        );

        // Act
        var result = await _escrowEngine.ReleaseOnProofOfDeliveryAsync(podRequest);

        // Assert
        result.Success.Should().BeTrue();
        result.EscrowRecord.Status.Should().Be(EscrowStatus.Settled);
        result.EscrowRecord.SettledAtUtc.Should().NotBeNull();

        // 4-way split breakdown:
        // Merchant Net = 10000 - 800 - 500 - 700 = 8000 DZD
        result.SplitBreakdown.GrossAmount.Should().Be(10000m);
        result.SplitBreakdown.CourierFee.Should().Be(800m);
        result.SplitBreakdown.PlatformTakeRateFee.Should().Be(500m);
        result.SplitBreakdown.AffiliateCommission.Should().Be(700m);
        result.SplitBreakdown.MerchantNet.Should().Be(8000m);

        // Invariant check: sum equals gross
        (result.SplitBreakdown.MerchantNet +
         result.SplitBreakdown.CourierFee +
         result.SplitBreakdown.PlatformTakeRateFee +
         result.SplitBreakdown.AffiliateCommission).Should().Be(10000m);

        // Check Ledger Accounts:
        // 1100 Customer Escrow Held must be exactly 0 (cleared!)
        var escrowHeld = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CustomerEscrowHeld, "DZD");
        escrowHeld.Should().Be(0m);

        // Liabilities & Revenue
        var merchantPayable = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.MerchantAccountsPayable, "DZD");
        var courierPayable = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CourierRemittancePayable, "DZD");
        var affiliatePayable = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.AffiliateCommissionPayable, "DZD");
        var platformRevenue = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.PlatformSaaSTakeRateRevenue, "DZD");

        merchantPayable.Should().Be(8000m);
        courierPayable.Should().Be(800m);
        affiliatePayable.Should().Be(700m);
        platformRevenue.Should().Be(500m);
    }

    [Fact]
    public async Task Escrow_ReleaseOnProofOfDelivery_ShouldUpdateMerchantCourierAndAffiliateWallets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-ANNABA-404";
        var merchantId = "MERCH-ANNABA";
        var courierId = "COURIER-ANNABA";
        var affiliateId = "AFF-ANNABA";

        await _escrowEngine.LockOnCheckoutAsync(new EscrowLockRequest(
            tenantId, orderId, "CUST-4", merchantId, courierId, affiliateId, 20000m, "DZD"));

        var podRequest = new ProofOfDeliveryConfirmationRequest(
            TenantId: tenantId,
            OrderId: orderId,
            TrackingNumber: "TRK-ANN-1",
            ProofSignatureOrPin: "SIG-OK",
            DeliveredAtUtc: DateTimeOffset.UtcNow,
            CourierFee: 1200m,
            PlatformTakeRatePercentage: 0.04m, // 800 DZD
            AffiliateCommissionPercentage: 0.05m // 1000 DZD
        );

        // Act
        await _escrowEngine.ReleaseOnProofOfDeliveryAsync(podRequest);

        // Assert Wallets
        var merchantWallet = await _walletService.FindWalletByEntityAsync(tenantId, merchantId, "DZD");
        var courierWallet = await _walletService.FindWalletByEntityAsync(tenantId, courierId, "DZD");
        var affiliateWallet = await _walletService.FindWalletByEntityAsync(tenantId, affiliateId, "DZD");

        // Merchant: EscrowLocked drops from 20,000 to 0. Available increases by 18,000 (20000 - 1200 - 800 - 1000)
        merchantWallet!.EscrowLockedBalance.Should().Be(0m);
        merchantWallet.AvailableBalance.Should().Be(17000m);

        // Courier: Available increases by 1200
        courierWallet!.AvailableBalance.Should().Be(1200m);

        // Affiliate: Available increases by 1000
        affiliateWallet!.AvailableBalance.Should().Be(1000m);
    }

    [Fact]
    public async Task Escrow_ReleaseOnProofOfDelivery_WhenAlreadySettled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-DOUBLE-SETTLE";

        await _escrowEngine.LockOnCheckoutAsync(new EscrowLockRequest(
            tenantId, orderId, "CUST-X", "MERCH-X", "COURIER-X", null, 5000m, "DZD"));

        var podRequest = new ProofOfDeliveryConfirmationRequest(
            tenantId, orderId, "TRK-X", "PIN-1234", DateTimeOffset.UtcNow, 500m, 0.05m);

        await _escrowEngine.ReleaseOnProofOfDeliveryAsync(podRequest);

        // Act - Attempt to release again
        var act = async () => await _escrowEngine.ReleaseOnProofOfDeliveryAsync(podRequest);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current status is 'Settled'*");
    }

    [Fact]
    public async Task Escrow_RefundOnReturn_PreDeliveryRefusal_ShouldReverseLockAndClearEscrow()
    {
        // Arrange: Package refused at delivery doorstep (COD RTO)
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-REFUSED-505";
        var merchantId = "MERCH-REFUSED";

        await _escrowEngine.LockOnCheckoutAsync(new EscrowLockRequest(
            tenantId, orderId, "CUST-5", merchantId, "COURIER-5", null, 7000m, "DZD"));

        var refundRequest = new EscrowRefundRequest(
            TenantId: tenantId,
            OrderId: orderId,
            Reason: "CustomerRefusedAtDoorstep",
            ReturnShippingFee: 0m
        );

        // Act
        var result = await _escrowEngine.RefundOnReturnAsync(refundRequest);

        // Assert
        result.Success.Should().BeTrue();
        result.EscrowRecord.Status.Should().Be(EscrowStatus.Refunded);

        // Ledger: Escrow Held (1100) and Cash Float (1000) are both restored to 0
        var escrowHeld = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CustomerEscrowHeld, "DZD");
        var cashFloat = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CashAndCourierFloat, "DZD");
        escrowHeld.Should().Be(0m);
        cashFloat.Should().Be(0m);

        // Merchant wallet escrow locked balance is cleared
        var merchantWallet = await _walletService.FindWalletByEntityAsync(tenantId, merchantId, "DZD");
        merchantWallet!.EscrowLockedBalance.Should().Be(0m);
        merchantWallet.AvailableBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Escrow_RefundOnReturn_WithReturnFreightFee_ShouldDebitFreightExpenseAndCreditCourier()
    {
        // Arrange: Package returned, courier charges return shipping fee of 450 DZD
        var tenantId = Guid.NewGuid();
        var orderId = "ORD-RTO-FREIGHT-606";
        var courierId = "COURIER-RTO-CARRIER";

        await _escrowEngine.LockOnCheckoutAsync(new EscrowLockRequest(
            tenantId, orderId, "CUST-6", "MERCH-6", courierId, null, 12000m, "DZD"));

        var refundRequest = new EscrowRefundRequest(
            TenantId: tenantId,
            OrderId: orderId,
            Reason: "PackageDamagedInTransit",
            ReturnShippingFee: 450m
        );

        // Act
        var result = await _escrowEngine.RefundOnReturnAsync(refundRequest);

        // Assert
        result.Success.Should().BeTrue();

        // 5000 Shipping & Freight Expense: Debit 450 DZD
        var freightExpense = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.ShippingAndFreightExpense, "DZD");
        freightExpense.Should().Be(450m);

        // 2100 Courier Remittance Payable: Credit 450 DZD
        var courierPayable = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CourierRemittancePayable, "DZD");
        courierPayable.Should().Be(450m);

        // Courier wallet credited with 450 DZD
        var courierWallet = await _walletService.FindWalletByEntityAsync(tenantId, courierId, "DZD");
        courierWallet!.AvailableBalance.Should().Be(450m);
    }

    #endregion

    #region 4. Virtual Wallet, Payouts, & Gateway Adapters

    [Fact]
    public async Task VirtualWallet_BalanceSegregation_ShouldMaintainSeparateTiers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-TIER-1", WalletOwnerType.Merchant, "DZD");

        // Act - add available, lock escrow
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 5000m, "Seed", "S1", "Initial funds");
        await _walletService.LockEscrowBalanceAsync(wallet.WalletId, 3000m, "REF-ESCROW");

        // Assert
        var updated = await _walletService.GetWalletAsync(wallet.WalletId);
        updated.AvailableBalance.Should().Be(5000m);
        updated.EscrowLockedBalance.Should().Be(3000m);
        updated.PendingPayoutBalance.Should().Be(0m);
        updated.TotalBalance.Should().Be(8000m);
    }

    [Fact]
    public async Task VirtualWallet_Payout_WhenAvailableFundsSufficient_ShouldMoveToPendingPayout()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-PAYOUT-OK", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 10000m, "Seed", "S1", "Initial");

        var payoutRequest = new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "IDEMP-PAYOUT-001",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Omar Belkacem", PayoutMethod.BaridiMob, "0770123456", PhoneNumber: "0770123456"),
                    Amount: 3500m,
                    Reference: "PO-OMAR-01"
                )
            ]
        );

        // Act
        var batch = await _walletService.CreatePayoutBatchAsync(payoutRequest);

        // Assert
        batch.Should().NotBeNull();
        batch.Status.Should().Be(PayoutBatchStatus.Processing);
        batch.TotalAmount.Should().Be(3500m);

        var updatedWallet = await _walletService.GetWalletAsync(wallet.WalletId);
        updatedWallet.AvailableBalance.Should().Be(6500m); // 10000 - 3500
        updatedWallet.PendingPayoutBalance.Should().Be(3500m);
        updatedWallet.TotalBalance.Should().Be(10000m); // Total preserved during processing
    }

    [Fact]
    public async Task VirtualWallet_Payout_WhenInsufficientAvailableBalance_ShouldThrowInsufficientFundsException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-INSUFFICIENT", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 1000m, "Seed", "S1", "Small balance");
        await _walletService.LockEscrowBalanceAsync(wallet.WalletId, 5000m, "REF-LOCKED"); // 5000 is locked in escrow

        var payoutRequest = new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "IDEMP-INSUF-001",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Karim Hadj", PayoutMethod.BaridiMob, "0661234567"),
                    Amount: 2000m, // Exceeds available 1000, even though total is 6000
                    Reference: "PO-KARIM-01"
                )
            ]
        );

        // Act
        var act = async () => await _walletService.CreatePayoutBatchAsync(payoutRequest);

        // Assert: Locked escrow cannot be tapped for payouts
        var ex = await act.Should().ThrowAsync<InsufficientFundsException>();
        ex.Which.RequestedAmount.Should().Be(2000m);
        ex.Which.AvailableBalance.Should().Be(1000m);
        ex.Which.EscrowLockedBalance.Should().Be(5000m);
    }

    [Fact]
    public async Task VirtualWallet_Payout_WhenFraudHoldActive_ShouldThrowFraudHoldBlockedException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-FRAUD-HOLD", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 50000m, "Seed", "S1", "High balance");

        // Apply Fraud Hold
        await _walletService.SetFraudHoldAsync(wallet.WalletId, true, "Suspicious velocity anomaly & AML screening alert");

        var payoutRequest = new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "IDEMP-FRAUD-001",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Suspicious Merchant", PayoutMethod.BaridiMob, "0550112233"),
                    Amount: 10000m,
                    Reference: "FRAUD-TRY-01"
                )
            ]
        );

        // Act
        var act = async () => await _walletService.CreatePayoutBatchAsync(payoutRequest);

        // Assert
        var ex = await act.Should().ThrowAsync<FraudHoldBlockedException>();
        ex.Which.WalletId.Should().Be(wallet.WalletId);
        ex.Which.EntityId.Should().Be("MERCH-FRAUD-HOLD");
        ex.Which.Reason.Should().Contain("AML screening");
    }

    [Fact]
    public async Task VirtualWallet_Payout_WhenActiveDisputesPresent_ShouldThrowFraudHoldBlockedException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-DISPUTED", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 20000m, "Seed", "S1", "Balance");

        // Record 2 active customer chargebacks
        await _walletService.RecordDisputeAsync(wallet.WalletId, "DISPUTE-101", isOpened: true);
        await _walletService.RecordDisputeAsync(wallet.WalletId, "DISPUTE-102", isOpened: true);

        var payoutRequest = new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "IDEMP-DISPUTE-001",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Merchant in Dispute", PayoutMethod.BaridiMob, "0555998877"),
                    Amount: 5000m,
                    Reference: "TRY-DISPUTE-01"
                )
            ]
        );

        // Act
        var act = async () => await _walletService.CreatePayoutBatchAsync(payoutRequest);

        // Assert
        var ex = await act.Should().ThrowAsync<FraudHoldBlockedException>();
        ex.Which.ActiveDisputes.Should().Be(2);
    }

    [Fact]
    public async Task VirtualWallet_Payout_Idempotency_ShouldReturnExistingBatchWithoutDoubleDeducting()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-IDEMP", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 10000m, "Seed", "S1", "Balance");

        var payoutRequest = new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "SHARED-IDEMPOTENCY-KEY-999",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Yacine Ben", PayoutMethod.BaridiMob, "0770554433"),
                    Amount: 3000m,
                    Reference: "IDEMP-TEST-01"
                )
            ]
        );

        // Act - Call twice with identical idempotency key
        var firstBatch = await _walletService.CreatePayoutBatchAsync(payoutRequest);
        var secondBatch = await _walletService.CreatePayoutBatchAsync(payoutRequest);

        // Assert: Same batch object returned
        secondBatch.BatchId.Should().Be(firstBatch.BatchId);
        secondBatch.IdempotencyKey.Should().Be("SHARED-IDEMPOTENCY-KEY-999");

        // Available balance should only have been deducted ONCE (10000 - 3000 = 7000, NOT 4000!)
        var currentWallet = await _walletService.GetWalletAsync(wallet.WalletId);
        currentWallet.AvailableBalance.Should().Be(7000m);
        currentWallet.PendingPayoutBalance.Should().Be(3000m);
    }

    [Fact]
    public async Task VirtualWallet_PayoutBatch_ConfirmProcessed_ShouldClearPendingAndPostDoubleEntry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-CONFIRM", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 15000m, "Seed", "S1", "Balance");

        var batch = await _walletService.CreatePayoutBatchAsync(new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "CONFIRM-BATCH-1",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Rachid Zouaoui", PayoutMethod.BaridiMob, "0660998877"),
                    Amount: 6000m,
                    Reference: "CONF-01"
                )
            ]
        ));

        // Act - Confirm processed by bank / BaridiMob
        var settledBatch = await _walletService.ConfirmPayoutBatchProcessedAsync(batch.BatchId, "SATIM-TXN-SUCCESS-9921");

        // Assert
        settledBatch.Status.Should().Be(PayoutBatchStatus.Settled);
        settledBatch.CompletedAtUtc.Should().NotBeNull();
        settledBatch.SettlementJournalEntryId.Should().NotBeNull();

        // Wallet: PendingPayoutBalance cleared to 0
        var updatedWallet = await _walletService.GetWalletAsync(wallet.WalletId);
        updatedWallet.PendingPayoutBalance.Should().Be(0m);
        updatedWallet.AvailableBalance.Should().Be(9000m);

        // Ledger: Check double-entry journal entry was posted
        // Debit: 2000 Merchant Accounts Payable (6000 DZD)
        // Credit: 1000 Cash Float (6000 DZD)
        var journalEntry = await _ledgerService.GetEntryByIdAsync(settledBatch.SettlementJournalEntryId!.Value);
        journalEntry.Should().NotBeNull();
        journalEntry!.TotalDebits.Should().Be(6000m);
        journalEntry.TotalCredits.Should().Be(6000m);
    }

    [Fact]
    public async Task VirtualWallet_PayoutBatch_Reject_ShouldRestoreFundsBackToAvailableBalance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var wallet = await _walletService.GetOrCreateWalletAsync(tenantId, "MERCH-REJECT", WalletOwnerType.Merchant, "DZD");
        await _walletService.CreditAvailableBalanceAsync(wallet.WalletId, 12000m, "Seed", "S1", "Balance");

        var batch = await _walletService.CreatePayoutBatchAsync(new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "REJECT-BATCH-1",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: wallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Invalid Phone", PayoutMethod.BaridiMob, "0555000000"),
                    Amount: 4000m,
                    Reference: "REJ-01"
                )
            ]
        ));

        // Act - Gateway rejected due to invalid beneficiary
        var rejectedBatch = await _walletService.RejectPayoutBatchAsync(batch.BatchId, "Beneficiary mobile wallet inactive or closed");

        // Assert
        rejectedBatch.Status.Should().Be(PayoutBatchStatus.Rejected);
        rejectedBatch.FailureReason.Should().Contain("inactive");

        // Funds restored: PendingPayout drops to 0, Available restored back to 12000
        var updatedWallet = await _walletService.GetWalletAsync(wallet.WalletId);
        updatedWallet.PendingPayoutBalance.Should().Be(0m);
        updatedWallet.AvailableBalance.Should().Be(12000m);
    }

    [Fact]
    public async Task NorthAfricanPayoutAdapter_BaridiMob_ShouldValidateMobileAndEdahabiaCard()
    {
        // Valid 10-digit Algerian numbers
        var v1 = await _northAfricanAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("User 1", PayoutMethod.BaridiMob, "0550123456"), PayoutMethod.BaridiMob, "DZD");
        v1.IsValid.Should().BeTrue();

        var v2 = await _northAfricanAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("User 2", PayoutMethod.BaridiMob, "0661998877"), PayoutMethod.BaridiMob, "DZD");
        v2.IsValid.Should().BeTrue();

        var v3 = await _northAfricanAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("User 3", PayoutMethod.BaridiMob, "0770334455"), PayoutMethod.BaridiMob, "DZD");
        v3.IsValid.Should().BeTrue();

        // Valid 16-digit Edahabia card
        var v4 = await _northAfricanAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("User 4", PayoutMethod.BaridiMob, "6280512345678901"), PayoutMethod.BaridiMob, "DZD");
        v4.IsValid.Should().BeTrue();

        // Invalid phone prefix (021 is fixed line, not mobile)
        var v5 = await _northAfricanAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("User 5", PayoutMethod.BaridiMob, "021998877"), PayoutMethod.BaridiMob, "DZD");
        v5.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task NorthAfricanPayoutAdapter_AlgeriePosteCcp_ShouldValidateCleAndGenerateTwentyDigitRip()
    {
        // Account: 1234567, Key: 45
        var beneficiary = new PayoutBeneficiary(
            BeneficiaryName: "Ahmed Mansouri",
            Method: PayoutMethod.AlgeriePosteCcp,
            AccountOrCardNumber: "1234567",
            BankOrPostalIdentifier: "45"
        );

        // Act
        var result = await _northAfricanAdapter.ValidateBeneficiaryAsync(beneficiary, PayoutMethod.AlgeriePosteCcp, "DZD");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FormattedIdentifier.Should().NotBeNull();
        result.FormattedIdentifier!.Length.Should().Be(20);
        result.FormattedIdentifier.Should().StartWith("00799999");
        result.FormattedIdentifier.Should().Be("00799999000123456745");
    }

    [Fact]
    public async Task InternationalPayoutAdapter_SepaXml_ShouldGenerateIso20022Pain001Xml()
    {
        // Arrange
        var batch = new PayoutBatch
        {
            BatchId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IdempotencyKey = "SEPA-INT-001",
            GatewayType = PayoutGatewayType.International,
            Currency = "EUR",
            TotalAmount = 2450.50m,
            Items =
            [
                new PayoutItem
                {
                    ItemId = Guid.NewGuid(),
                    Amount = 2450.50m,
                    Currency = "EUR",
                    Reference = "SEPA-INV-8812",
                    Beneficiary = new PayoutBeneficiary(
                        BeneficiaryName: "Jean Dupont SAS",
                        Method: PayoutMethod.SepaCreditTransfer,
                        AccountOrCardNumber: "FR7630006000011234567890189",
                        BankOrPostalIdentifier: "BNPAFR21"
                    )
                }
            ]
        };

        // Act
        var payload = await _internationalAdapter.FormatBatchPayloadAsync(batch);

        // Assert: ISO 20022 pain.001.001.03 XML
        payload.Should().Contain("urn:iso:std:iso:20022:tech:xsd:pain.001.001.03");
        payload.Should().Contain("<CstmrCdtTrfInitn>");
        payload.Should().Contain("<CtrlSum>2450.50</CtrlSum>");
        payload.Should().Contain("<IBAN>FR7630006000011234567890189</IBAN>");
        payload.Should().Contain("<BIC>BNPAFR21</BIC>");
        payload.Should().Contain("<Nm>Jean Dupont SAS</Nm>");
    }

    [Fact]
    public async Task InternationalPayoutAdapter_MultiCurrency_ShouldSupportEurUsdAed()
    {
        _internationalAdapter.SupportedCurrencies.Should().Contain(["EUR", "USD", "AED"]);

        var vEur = await _internationalAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("EU Corp", PayoutMethod.StripeConnect, "acct_1N23456789"), PayoutMethod.StripeConnect, "EUR");
        vEur.IsValid.Should().BeTrue();

        var vUsd = await _internationalAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("US Corp", PayoutMethod.StripeConnect, "acct_1US9876543"), PayoutMethod.StripeConnect, "USD");
        vUsd.IsValid.Should().BeTrue();

        var vAed = await _internationalAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("Dubai LLC", PayoutMethod.WiseTransfer, "wise_profile_5544"), PayoutMethod.WiseTransfer, "AED");
        vAed.IsValid.Should().BeTrue();

        // Unsupported currency (GBP not in supported list)
        var vGbp = await _internationalAdapter.ValidateBeneficiaryAsync(
            new PayoutBeneficiary("UK Corp", PayoutMethod.StripeConnect, "acct_1UK0000"), PayoutMethod.StripeConnect, "GBP");
        vGbp.IsValid.Should().BeFalse();
    }

    #endregion

    #region 5. End-to-End Enterprise Convergence Lifecycle

    [Fact]
    public async Task EndToEndEnterpriseFlow_CheckoutToPodToBatchPayout_ShouldMaintainZeroLedgerDrift()
    {
        // 1. Setup tenant & entities
        var tenantId = Guid.NewGuid();
        var merchantId = "ENTERPRISE-MERCHANT-DZ";
        var courierId = "LOGISTICS-NATIONAL-DZ";
        var affiliateId = "GROWTH-AFFILIATE-DZ";
        var orderId = "ORD-E2E-ENTERPRISE-9900";

        // 2. Stage 1: Checkout Lock for 50,000 DZD
        var lockRecord = await _escrowEngine.LockOnCheckoutAsync(new EscrowLockRequest(
            TenantId: tenantId,
            OrderId: orderId,
            CustomerId: "VIP-CUSTOMER-1",
            MerchantId: merchantId,
            CourierId: courierId,
            AffiliateId: affiliateId,
            GrossAmount: 50000m,
            Currency: "DZD"
        ));
        lockRecord.Status.Should().Be(EscrowStatus.Locked);

        // Check trial balance stage 1
        var tb1 = await _ledgerService.GetTrialBalanceAsync(tenantId, "DZD");
        tb1.IsBalanced.Should().BeTrue();
        tb1.TotalDebitBalances.Should().Be(50000m);

        // 3. Stage 2: Proof of Delivery (POD)
        // Split: Gross 50,000 DZD
        // Courier Fee: 2,500 DZD
        // Platform Take Rate: 6% (3,000 DZD)
        // Affiliate Commission: 8% (4,000 DZD)
        // Merchant Net: 40,500 DZD
        var podResult = await _escrowEngine.ReleaseOnProofOfDeliveryAsync(new ProofOfDeliveryConfirmationRequest(
            TenantId: tenantId,
            OrderId: orderId,
            TrackingNumber: "TRK-E2E-GLOBAL-01",
            ProofSignatureOrPin: "BIOMETRIC-CONFIRMED",
            DeliveredAtUtc: DateTimeOffset.UtcNow,
            CourierFee: 2500m,
            PlatformTakeRatePercentage: 0.06m,
            AffiliateCommissionPercentage: 0.08m
        ));
        podResult.Success.Should().BeTrue();
        podResult.SplitBreakdown.MerchantNet.Should().Be(40500m);

        // Check trial balance stage 2
        var tb2 = await _ledgerService.GetTrialBalanceAsync(tenantId, "DZD");
        tb2.IsBalanced.Should().BeTrue();
        tb2.TotalDebitBalances.Should().Be(50000m);

        // Escrow Held (1100) must be 0
        var escrowBal = await _ledgerService.GetAccountBalanceAsync(tenantId, StandardAccountCodes.CustomerEscrowHeld, "DZD");
        escrowBal.Should().Be(0m);

        // 4. Stage 3: Merchant initiates BaridiMob Payout Batch for 40,000 DZD
        var merchantWallet = await _walletService.FindWalletByEntityAsync(tenantId, merchantId, "DZD");
        merchantWallet!.AvailableBalance.Should().Be(40500m);

        var payoutBatch = await _walletService.CreatePayoutBatchAsync(new CreatePayoutBatchRequest(
            TenantId: tenantId,
            IdempotencyKey: "E2E-PAYOUT-IDEMP-01",
            GatewayType: PayoutGatewayType.NorthAfrican,
            Currency: "DZD",
            Items:
            [
                new CreatePayoutItemRequest(
                    WalletId: merchantWallet.WalletId,
                    Beneficiary: new PayoutBeneficiary("Enterprise Founder", PayoutMethod.BaridiMob, "0770001122"),
                    Amount: 40000m,
                    Reference: "E2E-DIVIDEND-01"
                )
            ]
        ));
        payoutBatch.Status.Should().Be(PayoutBatchStatus.Processing);

        // 5. Stage 4: Bank confirms payout clearance
        var settledPayout = await _walletService.ConfirmPayoutBatchProcessedAsync(payoutBatch.BatchId, "POSTAL-CLEARANCE-REF-7711");
        settledPayout.Status.Should().Be(PayoutBatchStatus.Settled);

        // 6. Final Invariant Verifications
        var tbFinal = await _ledgerService.GetTrialBalanceAsync(tenantId, "DZD");
        tbFinal.IsBalanced.Should().BeTrue();

        // Merchant wallet available balance: 40,500 - 40,000 = 500 DZD
        var finalMerchantWallet = await _walletService.GetWalletAsync(merchantWallet.WalletId);
        finalMerchantWallet.AvailableBalance.Should().Be(500m);
        finalMerchantWallet.PendingPayoutBalance.Should().Be(0m);
        finalMerchantWallet.EscrowLockedBalance.Should().Be(0m);

        // Integrity audit chain must pass 100% cryptographic validation
        var chainValid = await _ledgerService.VerifyIntegrityChainAsync(tenantId);
        chainValid.Should().BeTrue();
    }

    #endregion
}
