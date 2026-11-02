using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.CbdcCrossBorderBridge;

public class CbdcCrossBorderSettlementEngine
{
    public CbdcBridgeSettlementResult SettleCrossBorderBatch(CbdcSettlementRequest request)
    {
        var bankMap = request.ParticipantBanks.ToDictionary(b => b.BankCode);
        var currentReserves = request.ParticipantBanks.ToDictionary(b => b.BankCode, b => b.LiquidityReserve);

        var settlements = new List<SettledPvpTransaction>();
        int settledCount = 0;
        int rejectedCount = 0;
        decimal totalUsdVolume = 0m;

        foreach (var inst in request.Instructions)
        {
            if (!bankMap.TryGetValue(inst.DebtorBankCode, out var debtorBank) ||
                !bankMap.TryGetValue(inst.CreditorBankCode, out var creditorBank))
            {
                rejectedCount++;
                settlements.Add(new SettledPvpTransaction(
                    InstructionId: inst.InstructionId,
                    IsSettledAtomic: false,
                    DebtorCurrency: inst.DebtorCurrency,
                    Debited: 0m,
                    CreditorCurrency: inst.CreditorCurrency,
                    Credited: 0m,
                    EffectiveFxRate: 0m,
                    SettlementProofHash: string.Empty,
                    Reason: "Bank Node Unregistered: Debtor or creditor central bank not active on mBridge ledger."
                ));
                continue;
            }

            if (!debtorBank.IsActive || !creditorBank.IsActive)
            {
                rejectedCount++;
                settlements.Add(new SettledPvpTransaction(
                    InstructionId: inst.InstructionId,
                    IsSettledAtomic: false,
                    DebtorCurrency: inst.DebtorCurrency,
                    Debited: 0m,
                    CreditorCurrency: inst.CreditorCurrency,
                    Credited: 0m,
                    EffectiveFxRate: 0m,
                    SettlementProofHash: string.Empty,
                    Reason: "Central Bank Inactive: Corridor participant is administratively suspended."
                ));
                continue;
            }

            // Check debtor liquidity + overdraft headroom
            decimal debtorAvailable = currentReserves[inst.DebtorBankCode] + debtorBank.MaximumOverdraftLimit;
            if (debtorAvailable < inst.DebitAmount)
            {
                rejectedCount++;
                settlements.Add(new SettledPvpTransaction(
                    InstructionId: inst.InstructionId,
                    IsSettledAtomic: false,
                    DebtorCurrency: inst.DebtorCurrency,
                    Debited: 0m,
                    CreditorCurrency: inst.CreditorCurrency,
                    Credited: 0m,
                    EffectiveFxRate: 0m,
                    SettlementProofHash: string.Empty,
                    Reason: $"Insufficient Liquidity: Debtor reserve ({debtorAvailable:N2}) below debit requirement ({inst.DebitAmount:N2})."
                ));
                continue;
            }

            // Execute atomic PvP transfer
            currentReserves[inst.DebtorBankCode] -= inst.DebitAmount;
            currentReserves[inst.CreditorBankCode] += inst.ExpectedCreditAmount;
            settledCount++;

            decimal fxRate = inst.DebitAmount > 0 ? Math.Round(inst.ExpectedCreditAmount / inst.DebitAmount, 4) : 1m;
            totalUsdVolume += inst.DebitAmount; // Simplified USD-equivalent estimation

            // Cryptographic proof hash
            string proofRaw = $"{inst.InstructionId}:{inst.DebtorBankCode}:{inst.CreditorBankCode}:{inst.DebitAmount}:{inst.ExpectedCreditAmount}";
            string proofHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proofRaw)));

            settlements.Add(new SettledPvpTransaction(
                InstructionId: inst.InstructionId,
                IsSettledAtomic: true,
                DebtorCurrency: inst.DebtorCurrency,
                Debited: inst.DebitAmount,
                CreditorCurrency: inst.CreditorCurrency,
                Credited: inst.ExpectedCreditAmount,
                EffectiveFxRate: fxRate,
                SettlementProofHash: proofHash,
                Reason: "Atomic PvP Settled: Debited and credited simultaneously across distributed mBridge ledger."
            ));
        }

        string health = rejectedCount == 0
            ? "Liquidity Corridor Healthy: 100% atomic PvP settlements completed with zero settlement risk."
            : $"Corridor Constrained: {rejectedCount} transaction(s) rejected due to participant overdraft limits.";

        return new CbdcBridgeSettlementResult(
            TotalInstructionsEvaluated: request.Instructions.Count,
            AtomicSettlementsCompleted: settledCount,
            RejectedTransactions: rejectedCount,
            TotalValueSettledUsdEquivalent: totalUsdVolume,
            Settlements: settlements,
            LiquidityCorridorHealth: health
        );
    }
}
