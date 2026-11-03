using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Commerce.CommissionSettlement;

public interface ICommissionSettlementEngine
{
    CommissionOverview GetOverview();
    CommissionRecord CalculateCommission(CalculateCommissionRequest request);
    ReleaseEscrowResult ReleaseEscrow(ReleaseEscrowCommand command);
}

public class CommissionSettlementEngine : ICommissionSettlementEngine
{
    private readonly ConcurrentDictionary<string, CommissionRecord> _records = new();

    public CommissionSettlementEngine()
    {
        SeedRecords();
    }

    private void SeedRecords()
    {
        var now = DateTime.UtcNow;

        var r1 = new CommissionRecord(
            CommissionId: "COMM-2026-101",
            DealId: "DEAL-ENT-901",
            AccountName: "Global FinCorp Americas",
            RepId: "REP-401",
            RepName: "Victoria Sterling (Enterprise Director)",
            DealArr: 350000m,
            QuotaAttainmentPct: 128.5,
            Tier: RepTier.SuperAccelerator,
            TotalCommission: 70000m, // 20% on 350k
            ImmediateDisbursedAmount: 56000m, // 80%
            ClawbackEscrowAmount: 14000m,     // 20%
            Status: PayoutStatus.HeldInClawbackEscrow,
            ClosedAt: now.AddDays(-14),
            EscrowReleaseDate: now.AddDays(76),
            PayoutHash: "COMM-HASH-901-A48F"
        );

        var r2 = new CommissionRecord(
            CommissionId: "COMM-2026-102",
            DealId: "DEAL-ENT-902",
            AccountName: "Hyperion Cloud Systems",
            RepId: "REP-402",
            RepName: "Marcus Vance (Strategic AE)",
            DealArr: 180000m,
            QuotaAttainmentPct: 104.2,
            Tier: RepTier.Accelerator,
            TotalCommission: 27000m, // 15% on 180k
            ImmediateDisbursedAmount: 21600m,
            ClawbackEscrowAmount: 5400m,
            Status: PayoutStatus.HeldInClawbackEscrow,
            ClosedAt: now.AddDays(-28),
            EscrowReleaseDate: now.AddDays(62),
            PayoutHash: "COMM-HASH-902-B12C"
        );

        var r3 = new CommissionRecord(
            CommissionId: "COMM-2026-103",
            DealId: "DEAL-ENT-903",
            AccountName: "Nordic Health Telematics",
            RepId: "REP-403",
            RepName: "Elena Rostova (Commercial AE)",
            DealArr: 95000m,
            QuotaAttainmentPct: 92.0,
            Tier: RepTier.Standard,
            TotalCommission: 9500m, // 10% on 95k
            ImmediateDisbursedAmount: 7600m,
            ClawbackEscrowAmount: 1900m,
            Status: PayoutStatus.FullySettled,
            ClosedAt: now.AddDays(-95),
            EscrowReleaseDate: now.AddDays(-5),
            PayoutHash: "COMM-HASH-903-E88D"
        );

        _records[r1.CommissionId] = r1;
        _records[r2.CommissionId] = r2;
        _records[r3.CommissionId] = r3;
    }

    public CommissionOverview GetOverview()
    {
        var list = _records.Values.OrderByDescending(r => r.ClosedAt).ToList();
        var totalPaid = list.Sum(r => r.ImmediateDisbursedAmount + (r.Status == PayoutStatus.FullySettled ? r.ClawbackEscrowAmount : 0m));
        var inEscrow = list.Where(r => r.Status == PayoutStatus.HeldInClawbackEscrow).Sum(r => r.ClawbackEscrowAmount);
        var avgAttainment = list.Count > 0 ? list.Average(r => r.QuotaAttainmentPct) : 100.0;

        return new CommissionOverview(
            TotalDealsCount: list.Count,
            TotalCommissionsPaid: totalPaid,
            TotalInEscrow: inEscrow,
            AverageAttainmentPct: Math.Round(avgAttainment, 1),
            Commissions: list
        );
    }

    public CommissionRecord CalculateCommission(CalculateCommissionRequest request)
    {
        var (tier, rate) = DetermineTierAndRate(request.QuotaAttainmentPct);
        var totalComm = request.DealArr * rate;
        var immediate = Math.Round(totalComm * 0.80m, 2);
        var escrow = Math.Round(totalComm * 0.20m, 2);

        var commId = $"COMM-2026-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var rawHash = $"{commId}:{request.DealId}:{request.RepId}:{totalComm}:{DateTime.UtcNow:yyyyMMdd}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawHash));
        var hashStr = $"COMM-HASH-{Convert.ToHexString(hashBytes)[..12]}";

        var record = new CommissionRecord(
            CommissionId: commId,
            DealId: request.DealId,
            AccountName: request.AccountName,
            RepId: request.RepId,
            RepName: request.RepName,
            DealArr: request.DealArr,
            QuotaAttainmentPct: request.QuotaAttainmentPct,
            Tier: tier,
            TotalCommission: totalComm,
            ImmediateDisbursedAmount: immediate,
            ClawbackEscrowAmount: escrow,
            Status: PayoutStatus.HeldInClawbackEscrow,
            ClosedAt: DateTime.UtcNow,
            EscrowReleaseDate: DateTime.UtcNow.AddDays(90),
            PayoutHash: hashStr
        );

        _records[commId] = record;
        return record;
    }

    public ReleaseEscrowResult ReleaseEscrow(ReleaseEscrowCommand command)
    {
        if (!_records.TryGetValue(command.CommissionId, out var existing))
        {
            throw new KeyNotFoundException($"Commission record {command.CommissionId} not found.");
        }

        var updated = existing with
        {
            Status = PayoutStatus.FullySettled
        };

        _records[command.CommissionId] = updated;

        var receipt = $"ESCROW-RELEASE-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}: Disbursed {existing.ClawbackEscrowAmount:C0} to {existing.RepName} approved by {command.ApproverId}.";

        return new ReleaseEscrowResult(
            CommissionId: command.CommissionId,
            ReleasedAmount: existing.ClawbackEscrowAmount,
            Status: PayoutStatus.FullySettled,
            ReleaseReceipt: receipt
        );
    }

    private static (RepTier Tier, decimal Rate) DetermineTierAndRate(double quotaPct)
    {
        if (quotaPct > 120.0) return (RepTier.SuperAccelerator, 0.20m);
        if (quotaPct > 100.0) return (RepTier.Accelerator, 0.15m);
        if (quotaPct > 80.0) return (RepTier.Standard, 0.10m);
        return (RepTier.Baseline, 0.08m);
    }
}
