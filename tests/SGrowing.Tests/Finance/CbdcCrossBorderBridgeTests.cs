namespace SGrowing.Tests.Finance;

using FluentAssertions;
using SGrowing.Api.Modules.Finance.CbdcCrossBorderBridge;
using Xunit;

public class CbdcCrossBorderBridgeTests
{
    private readonly CbdcCrossBorderSettlementEngine _engine = new();

    [Fact]
    public void SettleCrossBorderBatch_ValidLiquidity_ExecutesAtomicPvpWithCryptographicProof()
    {
        var participantBanks = new List<CentralBankNode>
        {
            new("HKMA", "e-HKD", LiquidityReserve: 50_000_000m, MaximumOverdraftLimit: 10_000_000m, IsActive: true),
            new("PBOC", "e-CNY", LiquidityReserve: 100_000_000m, MaximumOverdraftLimit: 20_000_000m, IsActive: true)
        };

        var instructions = new List<CrossBorderPaymentInstruction>
        {
            new(
                InstructionId: "INST-HK-CN-001",
                DebtorBankCode: "HKMA",
                DebtorCurrency: "e-HKD",
                DebitAmount: 7_800_000m,
                CreditorBankCode: "PBOC",
                CreditorCurrency: "e-CNY",
                ExpectedCreditAmount: 7_200_000m, // FX ~ 0.9231
                PaymentHash: "HASH-SECRET-001",
                SettlementDeadlineUtc: DateTimeOffset.UtcNow.AddMinutes(5)
            )
        };

        var request = new CbdcSettlementRequest(participantBanks, instructions);
        var result = _engine.SettleCrossBorderBatch(request);

        result.TotalInstructionsEvaluated.Should().Be(1);
        result.AtomicSettlementsCompleted.Should().Be(1);
        result.RejectedTransactions.Should().Be(0);
        result.TotalValueSettledUsdEquivalent.Should().Be(7_800_000m);
        result.LiquidityCorridorHealth.Should().Contain("Corridor Healthy");

        var settlement = result.Settlements.Should().ContainSingle().Subject;
        settlement.IsSettledAtomic.Should().BeTrue();
        settlement.Debited.Should().Be(7_800_000m);
        settlement.Credited.Should().Be(7_200_000m);
        settlement.EffectiveFxRate.Should().Be(0.9231m);
        settlement.SettlementProofHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SettleCrossBorderBatch_InsufficientOverdraft_RejectsTransaction()
    {
        var participantBanks = new List<CentralBankNode>
        {
            new("CBUAE", "e-AED", LiquidityReserve: 500_000m, MaximumOverdraftLimit: 200_000m, IsActive: true), // Max available 700k
            new("BOT", "e-THB", LiquidityReserve: 20_000_000m, MaximumOverdraftLimit: 5_000_000m, IsActive: true)
        };

        // Payment requires 1,500,000 AED (> 700k available)
        var instructions = new List<CrossBorderPaymentInstruction>
        {
            new("INST-AE-TH-002", "CBUAE", "e-AED", 1_500_000m, "BOT", "e-THB", 14_000_000m, "HASH-OVERDRAFT", DateTimeOffset.UtcNow.AddMinutes(5))
        };

        var request = new CbdcSettlementRequest(participantBanks, instructions);
        var result = _engine.SettleCrossBorderBatch(request);

        result.AtomicSettlementsCompleted.Should().Be(0);
        result.RejectedTransactions.Should().Be(1);
        result.LiquidityCorridorHealth.Should().Contain("Corridor Constrained");

        var settlement = result.Settlements.Should().ContainSingle().Subject;
        settlement.IsSettledAtomic.Should().BeFalse();
        settlement.Reason.Should().Contain("Insufficient Liquidity");
    }

    [Fact]
    public void SettleCrossBorderBatch_InactiveBank_RejectsWithSuspendedReason()
    {
        var participantBanks = new List<CentralBankNode>
        {
            new("BANK-SUSPENDED", "e-EUR", 10_000_000m, 5_000_000m, IsActive: false),
            new("PBOC", "e-CNY", 10_000_000m, 5_000_000m, IsActive: true)
        };

        var instructions = new List<CrossBorderPaymentInstruction>
        {
            new("INST-SUSPENDED", "BANK-SUSPENDED", "e-EUR", 100_000m, "PBOC", "e-CNY", 750_000m, "HASH-SUSP", DateTimeOffset.UtcNow.AddMinutes(5))
        };

        var request = new CbdcSettlementRequest(participantBanks, instructions);
        var result = _engine.SettleCrossBorderBatch(request);

        result.AtomicSettlementsCompleted.Should().Be(0);
        result.RejectedTransactions.Should().Be(1);
        result.Settlements[0].Reason.Should().Contain("Central Bank Inactive");
    }

    [Fact]
    public void SettleCrossBorderBatch_UnregisteredBankCode_RejectsWithPolicyReason()
    {
        var participantBanks = new List<CentralBankNode>
        {
            new("HKMA", "e-HKD", 10_000_000m, 1_000_000m, true)
        };

        var instructions = new List<CrossBorderPaymentInstruction>
        {
            new("INST-UNKNOWN", "HKMA", "e-HKD", 50_000m, "UNKNOWN_BANK", "e-USD", 6_400m, "HASH-X", DateTimeOffset.UtcNow)
        };

        var request = new CbdcSettlementRequest(participantBanks, instructions);
        var result = _engine.SettleCrossBorderBatch(request);

        result.RejectedTransactions.Should().Be(1);
        result.Settlements[0].Reason.Should().Contain("Bank Node Unregistered");
    }
}
