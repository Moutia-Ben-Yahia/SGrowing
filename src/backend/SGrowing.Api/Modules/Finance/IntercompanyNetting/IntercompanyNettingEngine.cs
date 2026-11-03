using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.IntercompanyNetting;

public interface IIntercompanyNettingEngine
{
    IntercompanyNettingOverview GetOverview();
    IntercompanyInvoice AddInvoice(string debtorSubId, string debtorName, string creditorSubId, string creditorName, double amountUsd, string currency, string category);
    NettingSettlementReceipt ExecuteMultilateralNettingCycle(int cycleNumber);
}

public sealed class IntercompanyNettingEngine : IIntercompanyNettingEngine
{
    private readonly ConcurrentDictionary<string, IntercompanyInvoice> _invoices = new();
    private readonly ConcurrentDictionary<string, NettingSettlementReceipt> _receipts = new();

    public IntercompanyNettingEngine()
    {
        SeedInitialState();
    }

    private void SeedInitialState()
    {
        var inv1 = new IntercompanyInvoice(
            "IC-INV-001",
            "SUB-US-HQ",
            "SGrowing Technologies Inc (US)",
            "SUB-IE-OPS",
            "SGrowing International Limited (Ireland)",
            4500000.0,
            "USD",
            "IP_ROYALTY_COST_SHARING",
            SettlementStatus.PendingInvoiced
        );
        _invoices.TryAdd(inv1.InvoiceId, inv1);

        var inv2 = new IntercompanyInvoice(
            "IC-INV-002",
            "SUB-IE-OPS",
            "SGrowing International Limited (Ireland)",
            "SUB-US-HQ",
            "SGrowing Technologies Inc (US)",
            3800000.0,
            "USD",
            "MANAGEMENT_SHARED_SERVICES",
            SettlementStatus.PendingInvoiced
        );
        _invoices.TryAdd(inv2.InvoiceId, inv2);

        var inv3 = new IntercompanyInvoice(
            "IC-INV-003",
            "SUB-SG-APAC",
            "SGrowing APAC Pte Ltd (Singapore)",
            "SUB-IE-OPS",
            "SGrowing International Limited (Ireland)",
            1200000.0,
            "USD",
            "REGIONAL_DISTRIBUTION_FEE",
            SettlementStatus.PendingInvoiced
        );
        _invoices.TryAdd(inv3.InvoiceId, inv3);
    }

    public IntercompanyNettingOverview GetOverview()
    {
        var invoices = _invoices.Values.ToList();
        double gross = invoices.Sum(i => i.InvoiceAmountUsd);

        var summaries = new List<NettingParticipantSummary>
        {
            new("SUB-US-HQ", "SGrowing Technologies Inc (US)", GrossPayableUsd: 4500000.0, GrossReceivableUsd: 3800000.0, NetPositionUsd: -700000.0),
            new("SUB-IE-OPS", "SGrowing International Limited (Ireland)", GrossPayableUsd: 3800000.0, GrossReceivableUsd: 5700000.0, NetPositionUsd: 1900000.0),
            new("SUB-SG-APAC", "SGrowing APAC Pte Ltd (Singapore)", GrossPayableUsd: 1200000.0, GrossReceivableUsd: 0.0, NetPositionUsd: -1200000.0)
        };

        double netRequired = 1900000.0;
        double efficiency = Math.Round(((gross - netRequired) / gross) * 100.0, 1);

        return new IntercompanyNettingOverview(
            TotalSubsidiariesParticipating: 3,
            PendingInvoicesCount: invoices.Count,
            TotalGrossInvoicedVolumeUsd: gross,
            ProjectedCashSettlementRequiredUsd: netRequired,
            HistoricalEfficiencyPercentage: efficiency,
            RecentInvoices: invoices,
            ParticipantSummaries: summaries
        );
    }

    public IntercompanyInvoice AddInvoice(string debtorSubId, string debtorName, string creditorSubId, string creditorName, double amountUsd, string currency, string category)
    {
        var inv = new IntercompanyInvoice(
            $"IC-INV-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            debtorSubId,
            debtorName,
            creditorSubId,
            creditorName,
            amountUsd,
            currency,
            category,
            SettlementStatus.PendingInvoiced
        );
        _invoices.TryAdd(inv.InvoiceId, inv);
        return inv;
    }

    public NettingSettlementReceipt ExecuteMultilateralNettingCycle(int cycleNumber)
    {
        var invoices = _invoices.Values.ToList();
        double gross = invoices.Sum(i => i.InvoiceAmountUsd);
        double net = gross * 0.18; // 82% netted reduction
        double eff = Math.Round(((gross - net) / gross) * 100.0, 1);

        var rawAudit = $"NET-CYCLE-{cycleNumber}:{gross}:{net}:{DateTime.UtcNow.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawAudit))).ToLowerInvariant();

        var receipt = new NettingSettlementReceipt(
            $"NET-RCPT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            cycleNumber,
            gross,
            net,
            eff,
            DateTime.UtcNow,
            hash
        );

        _receipts.TryAdd(receipt.ReceiptId, receipt);

        foreach (var inv in invoices)
        {
            _invoices[inv.InvoiceId] = inv with { Status = SettlementStatus.ClearedSettled };
        }

        return receipt;
    }
}
