namespace SGrowing.Tests.Finance;

using SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumTree;
using Xunit;

public class ZkSolvencyMerkleSumTreeTests
{
    private readonly ZkSolvencyMerkleSumTreeEngine _engine = new();

    [Fact]
    public void AuditExchangeSolvency_FullReserveSolvent_CertifiesSolvent()
    {
        var request = new SolvencyAuditRequest(
            ExchangeId: "SOLVENT-EXCHANGE-GLOBAL",
            AuditEpochTimestamp: DateTimeOffset.UtcNow,
            UserAccounts:
            [
                new UserAccountBalance("USR-101", 50000.00m, "salt1"),
                new UserAccountBalance("USR-102", 75000.00m, "salt2"),
                new UserAccountBalance("USR-103", 25000.00m, "salt3")
            ], // Total liabilities = $150,000.00
            OnChainReserves:
            [
                new OnChainReserveWallet("0xColdVault1", "BTC", 100000.00m, "sig1"),
                new OnChainReserveWallet("0xColdVault2", "ETH", 60000.00m, "sig2")
            ], // Total reserves = $160,000.00 (106.67%)
            ZkProofHex: "0xabcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789");

        var result = _engine.AuditExchangeSolvency(request);

        Assert.Equal("SOLVENT-EXCHANGE-GLOBAL", result.ExchangeId);
        Assert.Equal(150000.00m, result.TotalUserLiabilitiesUsd);
        Assert.Equal(160000.00m, result.TotalOnChainReservesUsd);
        Assert.Equal(106.67m, result.SolvencyRatioPercent);
        Assert.True(result.IsFullySolvent);
        Assert.False(result.HasNegativeBalanceFraud);
        Assert.True(result.IsZkProofVerified);
        Assert.Equal("CERTIFIED_100_PERCENT_RESERVE_SOLVENT", result.CertificationStatus);
        Assert.StartsWith("0x", result.MerkleSumTreeRootHash);
    }

    [Fact]
    public void AuditExchangeSolvency_FractionalInsolvency_FlagsCriticalInsolvency()
    {
        var request = new SolvencyAuditRequest(
            ExchangeId: "FRACTIONAL-EXCHANGE-RISK",
            AuditEpochTimestamp: DateTimeOffset.UtcNow,
            UserAccounts:
            [
                new UserAccountBalance("USR-201", 1000000.00m, "saltA"),
                new UserAccountBalance("USR-202", 500000.00m, "saltB")
            ], // Total liabilities = $1,500,000.00
            OnChainReserves:
            [
                new OnChainReserveWallet("0xColdReserve", "USDT", 900000.00m, "sigX")
            ], // Total reserves = $900,000.00 (60.00%)
            ZkProofHex: "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");

        var result = _engine.AuditExchangeSolvency(request);

        Assert.False(result.IsFullySolvent);
        Assert.Equal(60.00m, result.SolvencyRatioPercent);
        Assert.Equal("CRITICAL_FRACTIONAL_INSOLVENCY", result.CertificationStatus);
        Assert.Contains("Insolvency Alert!", result.AuditSummary);
    }

    [Fact]
    public void AuditExchangeSolvency_NegativeBalanceInjection_DetectsFraud()
    {
        var request = new SolvencyAuditRequest(
            ExchangeId: "FRAUDULENT-EXCHANGE-CORP",
            AuditEpochTimestamp: DateTimeOffset.UtcNow,
            UserAccounts:
            [
                new UserAccountBalance("USR-GENUINE", 500000.00m, "saltG"),
                new UserAccountBalance("USR-FAKE-SINK", -300000.00m, "saltFake") // Negative balance to suppress liabilities
            ],
            OnChainReserves:
            [
                new OnChainReserveWallet("0xWallet", "USDC", 250000.00m, "sig")
            ],
            ZkProofHex: "0x9999999999999999999999999999999999999999999999999999999999999999");

        var result = _engine.AuditExchangeSolvency(request);

        Assert.True(result.HasNegativeBalanceFraud);
        Assert.False(result.IsFullySolvent);
        Assert.Equal("FRAUD_NEGATIVE_BALANCE_DETECTED", result.CertificationStatus);
        Assert.Contains("Detected negative user account balance injection", result.AuditSummary);
    }

    [Fact]
    public void AuditExchangeSolvency_InvalidZkProof_FailsProofVerification()
    {
        var request = new SolvencyAuditRequest(
            ExchangeId: "CORRUPTED-PROOF-EXCHANGE",
            AuditEpochTimestamp: DateTimeOffset.UtcNow,
            UserAccounts:
            [
                new UserAccountBalance("USR-1", 1000.00m, "salt1")
            ],
            OnChainReserves:
            [
                new OnChainReserveWallet("0xW1", "ETH", 2000.00m, "sig")
            ],
            ZkProofHex: "INVALID_NOT_HEX"); // Invalid ZK proof format

        var result = _engine.AuditExchangeSolvency(request);

        Assert.False(result.IsZkProofVerified);
        Assert.Equal("PROOF_VERIFICATION_FAILED", result.CertificationStatus);
    }
}
