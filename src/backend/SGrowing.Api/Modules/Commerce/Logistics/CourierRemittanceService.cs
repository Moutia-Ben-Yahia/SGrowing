using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.Logistics;

public enum RemittanceLedgerStage
{
    PendingCollection = 1,
    CollectedByCourier = 2,
    InCourierFloat = 3,
    RemittedToBank = 4,
    SettledWithMerchant = 5
}

public enum RemittanceDisputeType
{
    None = 0,
    CollectedCashShortfall = 1,
    FeeDiscrepancy = 2,
    MissingPackageOrUnknownWaybill = 3,
    UncollectedDelivery = 4
}

public class CodCashLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string WaybillId { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public decimal ExpectedCashAmount { get; set; }
    public decimal CollectedCashAmount { get; set; }
    public decimal ExpectedDeliveryFee { get; set; }
    public decimal ActualCourierFee { get; set; }
    public RemittanceLedgerStage CurrentStage { get; set; } = RemittanceLedgerStage.PendingCollection;
    public RemittanceDisputeType DisputeType { get; set; } = RemittanceDisputeType.None;
    public decimal VarianceAmount { get; set; }
    public bool IsDisputed { get; set; }
    public string? DisputeReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastStageUpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

public record CourierRemittanceBatchStatementRow(
    string WaybillId,
    decimal CollectedCash,
    decimal CourierFee,
    DateTime? RemittedDateUtc = null);

public record CourierRemittanceBatchRequest(
    Guid TenantId,
    string StatementReference,
    string CourierName,
    DateTime StatementDateUtc,
    List<CourierRemittanceBatchStatementRow> Rows);

public record RemittanceDisputeRecord(
    string WaybillId,
    RemittanceDisputeType DisputeType,
    decimal ExpectedAmount,
    decimal ReportedAmount,
    decimal Variance,
    string Description);

public record CourierRemittanceReconciliationReport(
    Guid TenantId,
    string StatementReference,
    string CourierName,
    int TotalWaybills,
    int MatchedWaybills,
    int DisputedWaybills,
    decimal TotalExpectedCash,
    decimal TotalCollectedCash,
    decimal TotalCourierFees,
    decimal NetRemittedCash,
    decimal TotalVarianceAmount,
    IReadOnlyList<RemittanceDisputeRecord> Disputes,
    DateTime ReconciledAtUtc);

public record CourierRemittanceSummary(
    Guid TenantId,
    decimal TotalPendingCollection,
    decimal TotalCollectedByCourier,
    decimal TotalInCourierFloat,
    decimal TotalRemittedToBank,
    decimal TotalSettledWithMerchant,
    int ActiveDisputesCount,
    decimal TotalDisputedAmount,
    Dictionary<string, decimal> CashByStage,
    DateTime GeneratedAtUtc);

public interface ICourierRemittanceService
{
    Task<CodCashLedgerEntry> RecordShipmentForCollectionAsync(
        Guid tenantId,
        string waybillId,
        Guid orderId,
        string courierName,
        decimal expectedCashAmount,
        decimal expectedDeliveryFee = 7.0m,
        CancellationToken ct = default);

    Task<CodCashLedgerEntry> TransitionStageAsync(
        Guid tenantId,
        string waybillId,
        RemittanceLedgerStage targetStage,
        string? note = null,
        CancellationToken ct = default);

    Task<CourierRemittanceReconciliationReport> ReconcileBatchStatementAsync(
        Guid tenantId,
        CourierRemittanceBatchRequest batch,
        CancellationToken ct = default);

    Task<CourierRemittanceSummary> GetRemittanceSummaryAsync(
        Guid tenantId,
        string? courierName = null,
        CancellationToken ct = default);

    Task<CodCashLedgerEntry?> GetLedgerEntryAsync(
        Guid tenantId,
        string waybillId,
        CancellationToken ct = default);
}

public class CourierRemittanceService : ICourierRemittanceService
{
    private readonly ConcurrentDictionary<(Guid TenantId, string WaybillId), CodCashLedgerEntry> _ledger = new();

    public Task<CodCashLedgerEntry> RecordShipmentForCollectionAsync(
        Guid tenantId,
        string waybillId,
        Guid orderId,
        string courierName,
        decimal expectedCashAmount,
        decimal expectedDeliveryFee = 7.0m,
        CancellationToken ct = default)
    {
        var cleanWaybill = (waybillId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanWaybill))
        {
            throw new ArgumentException("Waybill ID cannot be null or empty.", nameof(waybillId));
        }

        var entry = new CodCashLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WaybillId = cleanWaybill,
            OrderId = orderId,
            CourierName = courierName.Trim(),
            ExpectedCashAmount = expectedCashAmount,
            CollectedCashAmount = 0m,
            ExpectedDeliveryFee = expectedDeliveryFee,
            ActualCourierFee = 0m,
            CurrentStage = RemittanceLedgerStage.PendingCollection,
            DisputeType = RemittanceDisputeType.None,
            VarianceAmount = 0m,
            IsDisputed = false,
            CreatedAtUtc = DateTime.UtcNow,
            LastStageUpdatedUtc = DateTime.UtcNow
        };

        _ledger[(tenantId, cleanWaybill)] = entry;
        return Task.FromResult(entry);
    }

    public Task<CodCashLedgerEntry> TransitionStageAsync(
        Guid tenantId,
        string waybillId,
        RemittanceLedgerStage targetStage,
        string? note = null,
        CancellationToken ct = default)
    {
        var cleanWaybill = (waybillId ?? string.Empty).Trim();
        if (!_ledger.TryGetValue((tenantId, cleanWaybill), out var entry))
        {
            throw new KeyNotFoundException($"Ledger entry for waybill '{cleanWaybill}' not found for tenant {tenantId}.");
        }

        if (entry.CurrentStage == targetStage)
        {
            return Task.FromResult(entry);
        }

        // Validate multi-stage sequence: PendingCollection -> CollectedByCourier -> InCourierFloat -> RemittedToBank -> SettledWithMerchant
        bool isValidTransition = (entry.CurrentStage, targetStage) switch
        {
            (RemittanceLedgerStage.PendingCollection, RemittanceLedgerStage.CollectedByCourier) => true,
            (RemittanceLedgerStage.CollectedByCourier, RemittanceLedgerStage.InCourierFloat) => true,
            (RemittanceLedgerStage.InCourierFloat, RemittanceLedgerStage.RemittedToBank) => true,
            (RemittanceLedgerStage.RemittedToBank, RemittanceLedgerStage.SettledWithMerchant) => true,
            _ => false
        };

        if (!isValidTransition)
        {
            throw new InvalidOperationException(
                $"Invalid stage transition from {entry.CurrentStage} to {targetStage}. Stages must follow sequential multi-stage progression: PendingCollection -> CollectedByCourier -> InCourierFloat -> RemittedToBank -> SettledWithMerchant.");
        }

        entry.CurrentStage = targetStage;
        entry.LastStageUpdatedUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            entry.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? note.Trim() : $"{entry.Notes} | {note.Trim()}";
        }

        return Task.FromResult(entry);
    }

    public Task<CourierRemittanceReconciliationReport> ReconcileBatchStatementAsync(
        Guid tenantId,
        CourierRemittanceBatchRequest batch,
        CancellationToken ct = default)
    {
        var disputes = new List<RemittanceDisputeRecord>();
        int matchedCount = 0;
        int disputedCount = 0;
        decimal totalExpected = 0m;
        decimal totalCollected = 0m;
        decimal totalFees = 0m;
        decimal totalVariance = 0m;

        foreach (var row in batch.Rows)
        {
            var cleanWaybill = (row.WaybillId ?? string.Empty).Trim();
            totalCollected += row.CollectedCash;
            totalFees += row.CourierFee;

            if (!_ledger.TryGetValue((tenantId, cleanWaybill), out var entry))
            {
                // Missing package / unknown waybill
                disputedCount++;
                decimal missingVariance = row.CollectedCash;
                totalVariance += missingVariance;

                disputes.Add(new RemittanceDisputeRecord(
                    cleanWaybill,
                    RemittanceDisputeType.MissingPackageOrUnknownWaybill,
                    ExpectedAmount: 0m,
                    ReportedAmount: row.CollectedCash,
                    Variance: missingVariance,
                    Description: $"Waybill '{cleanWaybill}' present on {batch.CourierName} remittance statement was not found in system dispatched shipments."));
                continue;
            }

            matchedCount++;
            totalExpected += entry.ExpectedCashAmount;
            entry.CollectedCashAmount = row.CollectedCash;
            entry.ActualCourierFee = row.CourierFee;

            bool rowHasDispute = false;
            decimal rowVariance = 0m;
            var reasons = new List<string>();

            // 1. Collected cash shortfall
            if (row.CollectedCash < entry.ExpectedCashAmount)
            {
                decimal shortfall = entry.ExpectedCashAmount - row.CollectedCash;
                rowVariance += shortfall;
                rowHasDispute = true;
                reasons.Add($"Collected cash shortfall: Expected ${entry.ExpectedCashAmount:F2}, received ${row.CollectedCash:F2} (Variance: -${shortfall:F2})");

                disputes.Add(new RemittanceDisputeRecord(
                    cleanWaybill,
                    RemittanceDisputeType.CollectedCashShortfall,
                    ExpectedAmount: entry.ExpectedCashAmount,
                    ReportedAmount: row.CollectedCash,
                    Variance: shortfall,
                    Description: $"Cash shortfall for waybill '{cleanWaybill}': Expected ${entry.ExpectedCashAmount:F2}, courier remitted ${row.CollectedCash:F2}."));
            }

            // 2. Fee discrepancy
            if (row.CourierFee > entry.ExpectedDeliveryFee)
            {
                decimal feeExcess = row.CourierFee - entry.ExpectedDeliveryFee;
                rowVariance += feeExcess;
                rowHasDispute = true;
                reasons.Add($"Delivery tariff fee discrepancy: Expected ${entry.ExpectedDeliveryFee:F2}, charged ${row.CourierFee:F2} (Excess: +${feeExcess:F2})");

                disputes.Add(new RemittanceDisputeRecord(
                    cleanWaybill,
                    RemittanceDisputeType.FeeDiscrepancy,
                    ExpectedAmount: entry.ExpectedDeliveryFee,
                    ReportedAmount: row.CourierFee,
                    Variance: feeExcess,
                    Description: $"Courier fee overcharge for waybill '{cleanWaybill}': Expected ${entry.ExpectedDeliveryFee:F2}, courier invoiced ${row.CourierFee:F2}."));
            }

            if (rowHasDispute)
            {
                disputedCount++;
                totalVariance += rowVariance;
                entry.IsDisputed = true;
                entry.DisputeType = row.CollectedCash < entry.ExpectedCashAmount
                    ? RemittanceDisputeType.CollectedCashShortfall
                    : RemittanceDisputeType.FeeDiscrepancy;
                entry.VarianceAmount = rowVariance;
                entry.DisputeReason = string.Join("; ", reasons);
                entry.CurrentStage = RemittanceLedgerStage.RemittedToBank;
                entry.LastStageUpdatedUtc = DateTime.UtcNow;
            }
            else
            {
                entry.IsDisputed = false;
                entry.DisputeType = RemittanceDisputeType.None;
                entry.VarianceAmount = 0m;
                entry.CurrentStage = RemittanceLedgerStage.RemittedToBank;
                entry.LastStageUpdatedUtc = DateTime.UtcNow;
            }
        }

        decimal netRemitted = totalCollected - totalFees;

        var report = new CourierRemittanceReconciliationReport(
            tenantId,
            batch.StatementReference.Trim(),
            batch.CourierName.Trim(),
            TotalWaybills: batch.Rows.Count,
            MatchedWaybills: matchedCount,
            DisputedWaybills: disputedCount,
            TotalExpectedCash: totalExpected,
            TotalCollectedCash: totalCollected,
            TotalCourierFees: totalFees,
            NetRemittedCash: netRemitted,
            TotalVarianceAmount: totalVariance,
            Disputes: disputes,
            ReconciledAtUtc: DateTime.UtcNow);

        return Task.FromResult(report);
    }

    public Task<CourierRemittanceSummary> GetRemittanceSummaryAsync(
        Guid tenantId,
        string? courierName = null,
        CancellationToken ct = default)
    {
        var entries = _ledger.Values.Where(e => e.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(courierName))
        {
            entries = entries.Where(e => string.Equals(e.CourierName, courierName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var entryList = entries.ToList();

        decimal pending = entryList.Where(e => e.CurrentStage == RemittanceLedgerStage.PendingCollection).Sum(e => e.ExpectedCashAmount);
        decimal collected = entryList.Where(e => e.CurrentStage == RemittanceLedgerStage.CollectedByCourier).Sum(e => e.ExpectedCashAmount);
        decimal inFloat = entryList.Where(e => e.CurrentStage == RemittanceLedgerStage.InCourierFloat).Sum(e => e.ExpectedCashAmount);
        decimal remitted = entryList.Where(e => e.CurrentStage == RemittanceLedgerStage.RemittedToBank).Sum(e => e.CollectedCashAmount);
        decimal settled = entryList.Where(e => e.CurrentStage == RemittanceLedgerStage.SettledWithMerchant).Sum(e => e.CollectedCashAmount - e.ActualCourierFee);

        var disputed = entryList.Where(e => e.IsDisputed).ToList();

        var stageMap = new Dictionary<string, decimal>
        {
            ["PendingCollection"] = pending,
            ["CollectedByCourier"] = collected,
            ["InCourierFloat"] = inFloat,
            ["RemittedToBank"] = remitted,
            ["SettledWithMerchant"] = settled
        };

        var summary = new CourierRemittanceSummary(
            tenantId,
            pending,
            collected,
            inFloat,
            remitted,
            settled,
            ActiveDisputesCount: disputed.Count,
            TotalDisputedAmount: disputed.Sum(d => d.VarianceAmount),
            CashByStage: stageMap,
            GeneratedAtUtc: DateTime.UtcNow);

        return Task.FromResult(summary);
    }

    public Task<CodCashLedgerEntry?> GetLedgerEntryAsync(
        Guid tenantId,
        string waybillId,
        CancellationToken ct = default)
    {
        var clean = (waybillId ?? string.Empty).Trim();
        _ledger.TryGetValue((tenantId, clean), out var entry);
        return Task.FromResult(entry);
    }
}
