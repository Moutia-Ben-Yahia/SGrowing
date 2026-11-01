using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Finance.SettlementBridge;
using Xunit;

namespace SGrowing.Tests.Finance.SettlementBridge;

/// <summary>
/// Comprehensive test suite for Horizon 53:
/// Decentralized Multi-Currency Settlement Bridges &amp; Atomic Stablecoin Liquidity Aggregator.
/// </summary>
public sealed class MultiCurrencySettlementBridgeAndFxHedgingTests
{
    private readonly AtomicCrossRailSettlementEngine _settlementEngine = new();
    private readonly RealTimeFxHedgingOracle _hedgingOracle = new();
    private readonly HerstattRiskEliminatorService _herstattService = new();

    #region 1. HTLC Contract Creation & Cryptographic Validation

    [Fact]
    public void ComputeHashLock_Utf8String_ComputesAccurateSha256Hex()
    {
        const string secret = "horizon53-secret-preimage-key";
        byte[] expectedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        string expectedHex = Convert.ToHexStringLower(expectedHashBytes);

        var computedHex = _settlementEngine.ComputeHashLock(secret);

        computedHex.Should().Be(expectedHex);
        computedHex.Should().HaveLength(64);
    }

    [Fact]
    public void ComputeHashLock_HexPreimage_ComputesAccurateSha256Hex()
    {
        // 32-byte hex string (64 chars)
        const string hexSecret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        byte[] rawBytes = Convert.FromHexString(hexSecret);
        string expectedHex = Convert.ToHexStringLower(SHA256.HashData(rawBytes));

        var computedHex = _settlementEngine.ComputeHashLock(hexSecret);

        computedHex.Should().Be(expectedHex);
    }

    [Fact]
    public void VerifyPreimage_ValidUtf8AndHex_ReturnsTrue()
    {
        const string secret = "cross-border-settlement-token";
        string hashLock = _settlementEngine.ComputeHashLock(secret);

        bool isValid = _settlementEngine.VerifyPreimage(secret, hashLock);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPreimage_MismatchedSecret_ReturnsFalse()
    {
        const string realSecret = "real-secret-preimage";
        const string wrongSecret = "wrong-attacker-secret";
        string hashLock = _settlementEngine.ComputeHashLock(realSecret);

        bool isValid = _settlementEngine.VerifyPreimage(wrongSecret, hashLock);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void CreateContract_ValidParameters_InstantiatesActiveLockedContract()
    {
        const string secret = "alpha-secret-1";
        string hashLock = _settlementEngine.ComputeHashLock(secret);
        var ttl = TimeSpan.FromMinutes(30);

        var contract = _settlementEngine.CreateContract(
            sender: "tenant_eur_merchant",
            recipient: "tenant_usdc_liquidity_provider",
            amount: 50_000m,
            currency: CurrencyCode.EUR,
            rail: PaymentRail.SepaInstant,
            hashLockHex: hashLock,
            timeToLive: ttl
        );

        contract.Should().NotBeNull();
        contract.ContractId.Should().StartWith("htlc_");
        contract.Sender.Should().Be("tenant_eur_merchant");
        contract.Recipient.Should().Be("tenant_usdc_liquidity_provider");
        contract.Amount.Should().Be(50_000m);
        contract.Currency.Should().Be(CurrencyCode.EUR);
        contract.Rail.Should().Be(PaymentRail.SepaInstant);
        contract.HashLockHex.Should().Be(hashLock.ToLowerInvariant());
        contract.Status.Should().Be(HtlcStatus.ActiveLocked);
        contract.ClaimSecretHex.Should().BeNull();
        contract.ExpiryTimestampUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(29));
    }

    [Fact]
    public void CreateContract_InvalidAmountOrZeroTtl_ThrowsArgumentOutOfRangeException()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret");

        var actZeroAmount = () => _settlementEngine.CreateContract(
            "sender", "recipient", 0m, CurrencyCode.USD, PaymentRail.FedNow, hashLock, TimeSpan.FromMinutes(10));
        actZeroAmount.Should().Throw<ArgumentOutOfRangeException>();

        var actNegativeTtl = () => _settlementEngine.CreateContract(
            "sender", "recipient", 100m, CurrencyCode.USD, PaymentRail.FedNow, hashLock, TimeSpan.FromSeconds(-5));
        actNegativeTtl.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateContract_MissingPartiesOrHashLock_ThrowsArgumentException()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret");

        var actSender = () => _settlementEngine.CreateContract(
            "", "recipient", 100m, CurrencyCode.USD, PaymentRail.FedNow, hashLock, TimeSpan.FromMinutes(10));
        actSender.Should().Throw<ArgumentException>();

        var actRecipient = () => _settlementEngine.CreateContract(
            "sender", "  ", 100m, CurrencyCode.USD, PaymentRail.FedNow, hashLock, TimeSpan.FromMinutes(10));
        actRecipient.Should().Throw<ArgumentException>();

        var actHash = () => _settlementEngine.CreateContract(
            "sender", "recipient", 100m, CurrencyCode.USD, PaymentRail.FedNow, "", TimeSpan.FromMinutes(10));
        actHash.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClaimContract_ValidPreimageBeforeExpiry_SuccessfullyClaimsAndStoresSecret()
    {
        const string secret = "valid-preimage-secret-42";
        string hashLock = _settlementEngine.ComputeHashLock(secret);

        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 25_000m, CurrencyCode.EUR, PaymentRail.EurcStablecoin, hashLock, TimeSpan.FromHours(1));

        var claimed = _settlementEngine.ClaimContract(contract.ContractId, secret);

        claimed.Status.Should().Be(HtlcStatus.Claimed);
        claimed.ClaimSecretHex.Should().Be(secret);
        claimed.SettledAtUtc.Should().NotBeNull();
        claimed.SettledAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region 2. HTLC State Machine, Replay Protection & Expiration

    [Fact]
    public void ClaimContract_InvalidPreimage_ThrowsArgumentException()
    {
        string hashLock = _settlementEngine.ComputeHashLock("correct-secret");
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 10_000m, CurrencyCode.USD, PaymentRail.UsdcStablecoin, hashLock, TimeSpan.FromHours(1));

        var act = () => _settlementEngine.ClaimContract(contract.ContractId, "fraudulent-secret");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid preimage*");
    }

    [Fact]
    public void ClaimContract_ReplayAttempt_ThrowsInvalidOperationException()
    {
        const string secret = "secret-for-replay-test";
        string hashLock = _settlementEngine.ComputeHashLock(secret);
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 10_000m, CurrencyCode.USD, PaymentRail.UsdcStablecoin, hashLock, TimeSpan.FromHours(1));

        _settlementEngine.ClaimContract(contract.ContractId, secret);

        var actReplay = () => _settlementEngine.ClaimContract(contract.ContractId, secret);

        actReplay.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been claimed*");
    }

    [Fact]
    public void ClaimContract_ExpiredContract_ThrowsInvalidOperationException()
    {
        const string secret = "secret-expire";
        string hashLock = _settlementEngine.ComputeHashLock(secret);

        // Very small TTL
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 5_000m, CurrencyCode.DZD, PaymentRail.DzInstantCpi, hashLock, TimeSpan.FromMilliseconds(50));

        // Sleep to ensure expiration
        Thread.Sleep(80);

        var act = () => _settlementEngine.ClaimContract(contract.ContractId, secret);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public void ClaimContract_NonExistentContract_ThrowsKeyNotFoundException()
    {
        var act = () => _settlementEngine.ClaimContract("htlc_nonexistent_999", "any_secret");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void RefundExpiredContract_BeforeExpiry_ThrowsInvalidOperationException()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret");
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 10_000m, CurrencyCode.GBP, PaymentRail.SepaInstant, hashLock, TimeSpan.FromHours(2));

        var act = () => _settlementEngine.RefundExpiredContract(contract.ContractId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Lock time T has not expired yet*");
    }

    [Fact]
    public void RefundExpiredContract_AfterExpiry_TransitionsToRefundedExpired()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret");
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 10_000m, CurrencyCode.GBP, PaymentRail.SepaInstant, hashLock, TimeSpan.FromMilliseconds(50));

        Thread.Sleep(80);

        var refunded = _settlementEngine.RefundExpiredContract(contract.ContractId);

        refunded.Status.Should().Be(HtlcStatus.RefundedExpired);
        refunded.SettledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RefundExpiredContract_ReplayAttempt_ThrowsInvalidOperationException()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret");
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 10_000m, CurrencyCode.GBP, PaymentRail.SepaInstant, hashLock, TimeSpan.FromMilliseconds(50));

        Thread.Sleep(80);
        _settlementEngine.RefundExpiredContract(contract.ContractId);

        var actReplay = () => _settlementEngine.RefundExpiredContract(contract.ContractId);

        actReplay.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been refunded*");
    }

    [Fact]
    public void RefundExpiredContract_AlreadyClaimed_ThrowsInvalidOperationException()
    {
        const string secret = "secret-claimed-no-refund";
        string hashLock = _settlementEngine.ComputeHashLock(secret);
        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 10_000m, CurrencyCode.EUR, PaymentRail.SepaInstant, hashLock, TimeSpan.FromHours(1));

        _settlementEngine.ClaimContract(contract.ContractId, secret);

        var act = () => _settlementEngine.RefundExpiredContract(contract.ContractId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been claimed*");
    }

    #endregion

    #region 3. Dual-Rail Atomic Cross-Currency Swaps

    [Fact]
    public void InitiateSwap_CreatesPairedContractsOnSourceAndTargetRails_WithSameHashLock()
    {
        const string secret = "swap-dual-rail-secret";
        string hashLock = _settlementEngine.ComputeHashLock(secret);

        var swap = _settlementEngine.InitiateSwap(
            initiator: "buyer_paris",
            counterparty: "supplier_algiers",
            sourceRail: PaymentRail.SepaInstant,
            targetRail: PaymentRail.DzInstantCpi,
            sourceCurrency: CurrencyCode.EUR,
            targetCurrency: CurrencyCode.DZD,
            sourceAmount: 10_000m,
            targetAmount: 1_452_000m,
            hashLockHex: hashLock,
            timeToLive: TimeSpan.FromHours(1)
        );

        swap.Should().NotBeNull();
        swap.SwapId.Should().StartWith("swap_");
        swap.SourceContractId.Should().NotBeNullOrWhiteSpace();
        swap.TargetContractId.Should().NotBeNullOrWhiteSpace();
        swap.HashLockHex.Should().Be(hashLock.ToLowerInvariant());
        swap.Status.Should().Be(SwapStatus.BothLocked);

        var sourceContract = _settlementEngine.GetContract(swap.SourceContractId);
        var targetContract = _settlementEngine.GetContract(swap.TargetContractId);

        sourceContract.HashLockHex.Should().Be(hashLock.ToLowerInvariant());
        targetContract.HashLockHex.Should().Be(hashLock.ToLowerInvariant());
        sourceContract.Amount.Should().Be(10_000m);
        targetContract.Amount.Should().Be(1_452_000m);
    }

    [Fact]
    public void InitiateSwap_CalculatesCorrectExchangeRate_AndSetsBothLockedStatus()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret-rate");

        var swap = _settlementEngine.InitiateSwap(
            initiator: "trader_ny",
            counterparty: "pool_usdc",
            sourceRail: PaymentRail.FedNow,
            targetRail: PaymentRail.UsdcStablecoin,
            sourceCurrency: CurrencyCode.USD,
            targetCurrency: CurrencyCode.USD,
            sourceAmount: 100_000m,
            targetAmount: 100_000m,
            hashLockHex: hashLock,
            timeToLive: TimeSpan.FromHours(2)
        );

        swap.ExchangeRate.Should().Be(1.000000m);
        swap.Status.Should().Be(SwapStatus.BothLocked);
    }

    [Fact]
    public void InitiateSwap_InvalidAmountsOrTtl_ThrowsArgumentOutOfRangeException()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret");

        var actZeroSource = () => _settlementEngine.InitiateSwap(
            "init", "cp", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 0m, 100m, hashLock, TimeSpan.FromMinutes(10));
        actZeroSource.Should().Throw<ArgumentOutOfRangeException>();

        var actZeroTarget = () => _settlementEngine.InitiateSwap(
            "init", "cp", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 100m, 0m, hashLock, TimeSpan.FromMinutes(10));
        actZeroTarget.Should().Throw<ArgumentOutOfRangeException>();

        var actNegativeTtl = () => _settlementEngine.InitiateSwap(
            "init", "cp", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 100m, 100m, hashLock, TimeSpan.Zero);
        actNegativeTtl.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ConfirmCounterpartyLeg_InitiatedSwap_TransitionsToBothLocked()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret-confirm");
        var swap = _settlementEngine.InitiateSwap(
            "init", "cp", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 1000m, 1085m, hashLock, TimeSpan.FromMinutes(30));

        var confirmed = _settlementEngine.ConfirmCounterpartyLeg(swap.SwapId);

        confirmed.Status.Should().Be(SwapStatus.BothLocked);
    }

    [Fact]
    public void ClaimSwap_ValidPreimage_AtomicallyClaimsBothLegsAndCompletes()
    {
        const string secret = "cross-rail-atomic-success-secret";
        string hashLock = _settlementEngine.ComputeHashLock(secret);

        var swap = _settlementEngine.InitiateSwap(
            initiator: "alice",
            counterparty: "bob",
            sourceRail: PaymentRail.SepaInstant,
            targetRail: PaymentRail.UsdcStablecoin,
            sourceCurrency: CurrencyCode.EUR,
            targetCurrency: CurrencyCode.USD,
            sourceAmount: 10_000m,
            targetAmount: 10_850m,
            hashLockHex: hashLock,
            timeToLive: TimeSpan.FromHours(1)
        );

        var completedSwap = _settlementEngine.ClaimSwap(swap.SwapId, secret);

        completedSwap.Status.Should().Be(SwapStatus.Completed);
        completedSwap.SecretHex.Should().Be(secret);
        completedSwap.CompletedAtUtc.Should().NotBeNull();

        var sourceContract = _settlementEngine.GetContract(swap.SourceContractId);
        var targetContract = _settlementEngine.GetContract(swap.TargetContractId);

        sourceContract.Status.Should().Be(HtlcStatus.Claimed);
        targetContract.Status.Should().Be(HtlcStatus.Claimed);
        sourceContract.ClaimSecretHex.Should().Be(secret);
        targetContract.ClaimSecretHex.Should().Be(secret);
    }

    [Fact]
    public void ClaimSwap_InvalidPreimage_ThrowsArgumentExceptionAndLeavesContractsActive()
    {
        string hashLock = _settlementEngine.ComputeHashLock("real-secret");
        var swap = _settlementEngine.InitiateSwap(
            "alice", "bob", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 5_000m, 5_425m, hashLock, TimeSpan.FromHours(1));

        var act = () => _settlementEngine.ClaimSwap(swap.SwapId, "fake-secret");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid preimage*");

        var sourceContract = _settlementEngine.GetContract(swap.SourceContractId);
        var targetContract = _settlementEngine.GetContract(swap.TargetContractId);
        sourceContract.Status.Should().Be(HtlcStatus.ActiveLocked);
        targetContract.Status.Should().Be(HtlcStatus.ActiveLocked);
    }

    [Fact]
    public void ClaimSwap_ReplayClaim_ThrowsInvalidOperationException()
    {
        const string secret = "secret-claim-replay";
        string hashLock = _settlementEngine.ComputeHashLock(secret);
        var swap = _settlementEngine.InitiateSwap(
            "alice", "bob", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 2_000m, 2_170m, hashLock, TimeSpan.FromHours(1));

        _settlementEngine.ClaimSwap(swap.SwapId, secret);

        var actReplay = () => _settlementEngine.ClaimSwap(swap.SwapId, secret);

        actReplay.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been claimed*");
    }

    [Fact]
    public void RefundSwap_AfterExpiry_AtomicallyRefundsBothLegs()
    {
        string hashLock = _settlementEngine.ComputeHashLock("secret-refund-dual");
        var swap = _settlementEngine.InitiateSwap(
            "alice", "bob", PaymentRail.SepaInstant, PaymentRail.FedNow,
            CurrencyCode.EUR, CurrencyCode.USD, 5_000m, 5_425m, hashLock, TimeSpan.FromMilliseconds(50));

        Thread.Sleep(80);

        var refundedSwap = _settlementEngine.RefundSwap(swap.SwapId);

        refundedSwap.Status.Should().Be(SwapStatus.AbortedRefunded);

        var sourceContract = _settlementEngine.GetContract(swap.SourceContractId);
        var targetContract = _settlementEngine.GetContract(swap.TargetContractId);
        sourceContract.Status.Should().Be(HtlcStatus.RefundedExpired);
        targetContract.Status.Should().Be(HtlcStatus.RefundedExpired);
    }

    #endregion

    #region 4. Real-Time FX Hedging Oracle & Interest Rate Parity

    [Fact]
    public void CalculateForwardRate_PositiveInterestRateDifferential_ForwardRateExceedsSpot()
    {
        // EUR/USD: Base = EUR (foreign rate = 3.50%), Quote = USD (domestic rate = 5.25%)
        decimal spot = 1.085000m;
        decimal domesticRate = 0.0525m;
        decimal foreignRate = 0.0350m;
        int tenureDays = 90;

        decimal forwardRate = _hedgingOracle.CalculateForwardRate(spot, domesticRate, foreignRate, tenureDays);

        // Since domestic rate > foreign rate, F must be strictly greater than S
        forwardRate.Should().BeGreaterThan(spot);

        // Verification of IRP formula:
        // F = S * (1 + 0.0525 * (90/360)) / (1 + 0.0350 * (90/360))
        // 1 + 0.0525 * 0.25 = 1.013125
        // 1 + 0.0350 * 0.25 = 1.008750
        // Ratio = 1.013125 / 1.008750 = 1.00433705
        // F = 1.085000 * 1.00433705 = 1.089706
        forwardRate.Should().Be(1.089706m);
    }

    [Fact]
    public void CalculateForwardRate_UsdDzd_MatchesCoveredInterestParityFormula()
    {
        // USD/DZD: Domestic (DZD) = 6.00%, Foreign (USD) = 5.25%, tenure = 30 days
        decimal spot = 134.500000m;
        decimal domesticRate = 0.0600m;
        decimal foreignRate = 0.0525m;
        int tenureDays = 30;

        decimal forwardRate = _hedgingOracle.CalculateForwardRate(spot, domesticRate, foreignRate, tenureDays);

        forwardRate.Should().BeGreaterThan(spot);
        // Manual calculate:
        // Num = 1 + 0.06 * (30/360) = 1 + 0.0050 = 1.005000
        // Den = 1 + 0.0525 * (30/360) = 1 + 0.004375 = 1.004375
        // F = 134.5 * (1.005000 / 1.004375) = 134.583696
        forwardRate.Should().Be(134.583696m);
    }

    [Fact]
    public void CalculateForwardRate_ZeroTenureDays_ReturnsSpotRateExact()
    {
        decimal spot = 1.250000m;
        decimal forwardRate = _hedgingOracle.CalculateForwardRate(spot, 0.05m, 0.02m, tenureDays: 0);

        forwardRate.Should().Be(spot);
    }

    [Fact]
    public void CalculateForwardRate_IdenticalInterestRates_ReturnsSpotRateExact()
    {
        decimal spot = 1.150000m;
        decimal forwardRate = _hedgingOracle.CalculateForwardRate(spot, 0.04m, 0.04m, tenureDays: 60);

        forwardRate.Should().Be(spot);
    }

    [Fact]
    public void CalculateForwardRate_NegativeSpotOrTenure_ThrowsArgumentOutOfRangeException()
    {
        var actNegativeSpot = () => _hedgingOracle.CalculateForwardRate(-1.0m, 0.05m, 0.03m, 30);
        actNegativeSpot.Should().Throw<ArgumentOutOfRangeException>();

        var actNegativeTenure = () => _hedgingOracle.CalculateForwardRate(1.08m, 0.05m, 0.03m, -10);
        actNegativeTenure.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateForwardPoints_ComputesPipsAccurately()
    {
        decimal spot = 1.085000m;
        decimal forward = 1.086578m;

        decimal points = _hedgingOracle.CalculateForwardPoints(spot, forward);

        // (1.086578 - 1.085000) * 10,000 = 15.78 pips
        points.Should().Be(15.78m);
    }

    [Fact]
    public void CalculateMarginSpreadPoints_ExpandsWithTenure()
    {
        decimal spread30 = _hedgingOracle.CalculateMarginSpreadPoints(30);
        decimal spread90 = _hedgingOracle.CalculateMarginSpreadPoints(90);

        spread30.Should().Be(14.00m);
        spread90.Should().Be(18.00m);
        spread90.Should().BeGreaterThan(spread30);
    }

    [Fact]
    public void GetHedgingQuote_GeneratesSymmetricalBidAskQuotes_AndSetsValidTenureExpiry()
    {
        var quote = _hedgingOracle.GetHedgingQuote(
            CurrencyCode.EUR,
            CurrencyCode.USD,
            notionalAmount: 500_000m,
            tenureDays: 30
        );

        quote.Should().NotBeNull();
        quote.Pair.Should().Be("EUR/USD");
        quote.SpotRate.Should().Be(1.085000m);
        quote.ForwardRate.Should().BeGreaterThan(quote.SpotRate);
        quote.BidForwardRate.Should().BeLessThan(quote.ForwardRate);
        quote.AskForwardRate.Should().BeGreaterThan(quote.ForwardRate);
        quote.MarginSpreadPoints.Should().Be(14.00m);
        quote.ExpiryUtc.Should().BeAfter(DateTime.UtcNow.AddDays(29));
    }

    #endregion

    #region 5. Liquidity Aggregator, Slippage & Pool Depth

    [Fact]
    public void EstimateSlippage_SmallOrder_YieldsMinimalPriceImpact()
    {
        // 10,000 EUR order in 15M USD FedNow pool
        var slippage = _hedgingOracle.EstimateSlippage(CurrencyCode.EUR, CurrencyCode.USD, requestedAmount: 10_000m);

        slippage.Should().NotBeNull();
        slippage.ExceedsMaxLiquidity.Should().BeFalse();
        slippage.PriceImpactPercentage.Should().BeLessThan(0.1m);
    }

    [Fact]
    public void EstimateSlippage_MediumOrder_YieldsProportionalSlippage()
    {
        // 500,000 EUR in USD pool
        var slippage = _hedgingOracle.EstimateSlippage(CurrencyCode.EUR, CurrencyCode.USD, requestedAmount: 500_000m);

        slippage.Should().NotBeNull();
        slippage.ExceedsMaxLiquidity.Should().BeFalse();
        slippage.PriceImpactPercentage.Should().BeGreaterThan(0.01m);
        slippage.SlippageAmount.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void EstimateSlippage_OrderExceedingPoolDepth_FlagsExceedsMaxLiquidity()
    {
        // 100,000,000 EUR order far exceeds available pool liquidity
        var slippage = _hedgingOracle.EstimateSlippage(CurrencyCode.EUR, CurrencyCode.USD, requestedAmount: 100_000_000m);

        slippage.ExceedsMaxLiquidity.Should().BeTrue();
        slippage.PriceImpactPercentage.Should().Be(50.0m);
    }

    [Fact]
    public void ExecuteHedgingOrder_ValidQuote_LocksInMerchantProfitMargin()
    {
        var quote = _hedgingOracle.GetHedgingQuote(CurrencyCode.EUR, CurrencyCode.USD, 100_000m, 30);

        var order = _hedgingOracle.ExecuteHedgingOrder(quote.QuoteId, notionalAmount: 100_000m, merchantTargetMarginPct: 2.5m);

        order.Should().NotBeNull();
        order.OrderId.Should().StartWith("hord_");
        order.QuoteId.Should().Be(quote.QuoteId);
        order.LockedForwardRate.Should().Be(quote.ForwardRate);
        order.GuaranteedMerchantProfitMargin.Should().Be(2_500.00m); // 2.5% of 100,000
        order.Status.Should().Be("Executed");
    }

    [Fact]
    public void ExecuteHedgingOrder_ExpiredQuote_ThrowsInvalidOperationException()
    {
        var expiredQuote = new FxHedgingQuote(
            QuoteId: "quote_expired_test",
            Pair: "EUR/USD",
            SpotRate: 1.08m,
            ForwardRate: 1.09m,
            MarginSpreadPoints: 10m,
            ExpiryUtc: DateTime.UtcNow.AddMinutes(-5) // Expired in past
        );

        var act = () => _hedgingOracle.ExecuteHedgingOrder(expiredQuote.QuoteId, 50_000m);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void UpdatePoolBalance_ValidDeltas_UpdatesAvailableAndUtilizationAccurately()
    {
        var pool = _hedgingOracle.GetPool("pool_sepa_eur");
        decimal initialAvailable = pool.AvailableBalance;

        // Lock 1,000,000 EUR in HTLC
        _hedgingOracle.UpdatePoolBalance("pool_sepa_eur", deltaBalance: 0m, deltaLocked: 1_000_000m);

        var updatedPool = _hedgingOracle.GetPool("pool_sepa_eur");
        updatedPool.LockedInHtlc.Should().Be(1_000_000m);
        updatedPool.AvailableBalance.Should().Be(initialAvailable - 1_000_000m);
        updatedPool.UtilizationRate.Should().Be(0.1000m); // 1M / 10M = 10%
    }

    #endregion

    #region 6. Herstatt Risk Eliminator & PvP Settlement

    [Fact]
    public async Task ExecutePvpSettlementAsync_SuccessfulSettlement_GuaranteesZeroPrincipalRisk()
    {
        var request = new PvpSettlementRequest(
            SettlementId: "pvp_settle_001",
            SenderTenantId: "tenant_corp_france",
            ReceiverTenantId: "tenant_supplier_algeria",
            Leg1Rail: PaymentRail.SepaInstant,
            Leg2Rail: PaymentRail.DzInstantCpi,
            Leg1Currency: CurrencyCode.EUR,
            Leg2Currency: CurrencyCode.DZD,
            Leg1Amount: 100_000m,
            Leg2Amount: 14_520_000m,
            SimulateLeg2Failure: false
        );

        var result = await _herstattService.ExecutePvpSettlementAsync(request);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Leg1Status.Should().Be("Settled");
        result.Leg2Status.Should().Be("Settled");
        result.PrincipalRiskExposed.Should().Be(0.0m);
        result.Message.Should().Contain("zero principal risk");
    }

    [Fact]
    public async Task ExecutePvpSettlementAsync_SimulatedLeg2Failure_AtomicallyRollsBackLeg1WithZeroPrincipalLoss()
    {
        var request = new PvpSettlementRequest(
            SettlementId: "pvp_settle_fail_002",
            SenderTenantId: "tenant_corp_usa",
            ReceiverTenantId: "tenant_bank_europe",
            Leg1Rail: PaymentRail.FedNow,
            Leg2Rail: PaymentRail.EurcStablecoin,
            Leg1Currency: CurrencyCode.USD,
            Leg2Currency: CurrencyCode.EUR,
            Leg1Amount: 500_000m,
            Leg2Amount: 460_829m,
            SimulateLeg2Failure: true
        );

        var result = await _herstattService.ExecutePvpSettlementAsync(request);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Leg1Status.Should().Be("AbortedAndRefunded");
        result.Leg2Status.Should().Be("Failed");
        result.PrincipalRiskExposed.Should().Be(0.0m); // Critical invariant: Zero Principal Loss!
        result.Message.Should().Contain("zero principal loss");
    }

    [Fact]
    public async Task ExecutePvpSettlementAsync_SimulatedFailure_IncrementsFailureRatioOnLeg2()
    {
        _herstattService.ResetRailCircuitBreaker(PaymentRail.UsdcStablecoin);

        var request = new PvpSettlementRequest(
            SettlementId: "pvp_settle_ratio_003",
            SenderTenantId: "tenant_a",
            ReceiverTenantId: "tenant_b",
            Leg1Rail: PaymentRail.SepaInstant,
            Leg2Rail: PaymentRail.UsdcStablecoin,
            Leg1Currency: CurrencyCode.EUR,
            Leg2Currency: CurrencyCode.USD,
            Leg1Amount: 10_000m,
            Leg2Amount: 10_850m,
            SimulateLeg2Failure: true
        );

        await _herstattService.ExecutePvpSettlementAsync(request);

        var health = _herstattService.GetRailHealth(PaymentRail.UsdcStablecoin);
        health.TotalSettlementAttempts.Should().Be(1);
        health.FailedSettlementAttempts.Should().Be(1);
        health.FailureRatio.Should().Be(1.0);
    }

    [Fact]
    public void CircuitBreaker_TripsWhenFailureRatioExceedsThreshold_AfterMinimumAttempts()
    {
        var rail = PaymentRail.DzInstantCpi;
        _herstattService.ResetRailCircuitBreaker(rail);

        // Record 1 success, then 4 failures (total 5 attempts, 4 failures = 80% failure ratio > 15% threshold)
        _herstattService.RecordSettlementOutcome(rail, success: true);
        _herstattService.RecordSettlementOutcome(rail, success: false, "Timeout on CPI switch");
        _herstattService.RecordSettlementOutcome(rail, success: false, "Gateway 504");
        _herstattService.RecordSettlementOutcome(rail, success: false, "Connectivity loss");
        _herstattService.RecordSettlementOutcome(rail, success: false, "Switch rejected");

        var health = _herstattService.GetRailHealth(rail);

        health.IsActive.Should().BeFalse();
        health.TotalSettlementAttempts.Should().Be(5);
        health.FailedSettlementAttempts.Should().Be(4);
        health.FailureRatio.Should().Be(0.8);
        health.DisableReason.Should().Contain("Circuit breaker tripped");
    }

    [Fact]
    public async Task ExecutePvpSettlementAsync_DisabledRail_ThrowsRailDisabledException()
    {
        var rail = PaymentRail.FedNow;
        _herstattService.DisableRailManually(rail, "Scheduled maintenance downtime");

        var request = new PvpSettlementRequest(
            SettlementId: "pvp_settle_disabled_test",
            SenderTenantId: "tenant_a",
            ReceiverTenantId: "tenant_b",
            Leg1Rail: PaymentRail.FedNow,
            Leg2Rail: PaymentRail.SepaInstant,
            Leg1Currency: CurrencyCode.USD,
            Leg2Currency: CurrencyCode.EUR,
            Leg1Amount: 1_000m,
            Leg2Amount: 920m
        );

        var act = async () => await _herstattService.ExecutePvpSettlementAsync(request);

        await act.Should().ThrowAsync<RailDisabledException>()
            .WithMessage("*disabled by circuit breaker*");

        // Clean up
        _herstattService.ResetRailCircuitBreaker(rail);
    }

    [Fact]
    public void ResetRailCircuitBreaker_RestoresDisabledRailToActive()
    {
        var rail = PaymentRail.EurcStablecoin;
        _herstattService.DisableRailManually(rail, "Manual test disable");
        _herstattService.GetRailHealth(rail).IsActive.Should().BeFalse();

        _herstattService.ResetRailCircuitBreaker(rail);

        var health = _herstattService.GetRailHealth(rail);
        health.IsActive.Should().BeTrue();
        health.TotalSettlementAttempts.Should().Be(0);
        health.FailedSettlementAttempts.Should().Be(0);
        health.DisableReason.Should().BeNull();
    }

    [Fact]
    public void DisableRailManually_ImmediatelyDeactivatesRail()
    {
        var rail = PaymentRail.SepaInstant;
        _herstattService.DisableRailManually(rail, "Regulatory audit hold");

        var health = _herstattService.GetRailHealth(rail);
        health.IsActive.Should().BeFalse();
        health.DisableReason.Should().Contain("Regulatory audit hold");

        // Clean up
        _herstattService.ResetRailCircuitBreaker(rail);
    }

    [Fact]
    public void GetAllRailHealthStatuses_ReturnsAllSupportedRails()
    {
        var statuses = _herstattService.GetAllRailHealthStatuses();

        statuses.Should().HaveCount(5);
        statuses.Select(s => s.Rail).Should().Contain([
            PaymentRail.SepaInstant,
            PaymentRail.FedNow,
            PaymentRail.UsdcStablecoin,
            PaymentRail.EurcStablecoin,
            PaymentRail.DzInstantCpi
        ]);
    }

    #endregion

    #region 7. High-Concurrency Multi-Threaded Execution using System.Threading.Lock

    [Fact]
    public void Concurrent_ContractCreation_ThreadSafeAndZeroCollisions()
    {
        const int threadCount = 40;
        string hashLock = _settlementEngine.ComputeHashLock("concurrent-secret");
        var contractIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, threadCount, i =>
        {
            var contract = _settlementEngine.CreateContract(
                sender: $"sender_{i}",
                recipient: $"recipient_{i}",
                amount: 100m + i,
                currency: CurrencyCode.USD,
                rail: PaymentRail.UsdcStablecoin,
                hashLockHex: hashLock,
                timeToLive: TimeSpan.FromHours(1)
            );
            contractIds.Add(contract.ContractId);
        });

        contractIds.Should().HaveCount(threadCount);
        contractIds.Distinct().Should().HaveCount(threadCount);
    }

    [Fact]
    public void Concurrent_SimultaneousClaimsOnSameContract_OnlyOneSucceeds()
    {
        const string secret = "concurrent-claim-secret";
        string hashLock = _settlementEngine.ComputeHashLock(secret);

        var contract = _settlementEngine.CreateContract(
            "alice", "bob", 50_000m, CurrencyCode.EUR, PaymentRail.SepaInstant, hashLock, TimeSpan.FromHours(1));

        const int attempts = 30;
        int successCount = 0;
        int failureCount = 0;

        Parallel.For(0, attempts, _ =>
        {
            try
            {
                _settlementEngine.ClaimContract(contract.ContractId, secret);
                Interlocked.Increment(ref successCount);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        // Exactly one thread wins the race condition; all other attempts are rejected
        successCount.Should().Be(1);
        failureCount.Should().Be(attempts - 1);
    }

    [Fact]
    public void Concurrent_PoolBalanceUpdates_MaintainsExactArithmeticBalance()
    {
        const string poolId = "pool_fednow_usd";
        var initial = _hedgingOracle.GetPool(poolId);
        decimal initialTotal = initial.TotalBalance;

        const int iterations = 50;
        decimal deltaPerIter = 10_000m;

        Parallel.For(0, iterations, _ =>
        {
            _hedgingOracle.UpdatePoolBalance(poolId, deltaBalance: deltaPerIter, deltaLocked: 0m);
        });

        var finalPool = _hedgingOracle.GetPool(poolId);
        finalPool.TotalBalance.Should().Be(initialTotal + (iterations * deltaPerIter));
    }

    #endregion

    #region 8. Dependency Injection & Service Registration (No BuildServiceProvider!)

    [Fact]
    public void AddSettlementBridgeServices_RegistersAtomicCrossRailSettlementEngine_AsSingleton()
    {
        // HARD CONSTRAINT: Assert directly on ServiceCollection descriptors without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSettlementBridgeServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAtomicCrossRailSettlementEngine) &&
            d.ImplementationType == typeof(AtomicCrossRailSettlementEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSettlementBridgeServices_RegistersRealTimeFxHedgingOracle_AsSingleton()
    {
        // HARD CONSTRAINT: Assert directly on ServiceCollection descriptors without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSettlementBridgeServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRealTimeFxHedgingOracle) &&
            d.ImplementationType == typeof(RealTimeFxHedgingOracle) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSettlementBridgeServices_RegistersHerstattRiskEliminatorService_AsSingleton()
    {
        // HARD CONSTRAINT: Assert directly on ServiceCollection descriptors without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSettlementBridgeServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHerstattRiskEliminatorService) &&
            d.ImplementationType == typeof(HerstattRiskEliminatorService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
