using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Sales.Proposals;

public interface IB2BProposalEngine
{
    QuoteCalculationResult CalculateQuote(CalculateQuoteRequest request);
    B2BProposal CreateProposal(string tenantId, string customerCompany, string customerEmail, List<QuoteLineItem> items, decimal discountPercent = 0);
    B2BProposal SignProposal(SignProposalRequest request);
    B2BProposal? GetProposal(string tenantId, Guid proposalId);
    void RecordUsage(RecordUsageRequest request);
    UsageInvoiceSummary GenerateUsageInvoice(string tenantId, DateTime periodStartUtc, DateTime periodEndUtc);
}

public class B2BProposalEngine : IB2BProposalEngine
{
    private const decimal MinimumMarginThreshold = 35.0m;
    private const decimal StandardVatRate = 0.10m; // 10% standard VAT / sales tax

    // In-memory thread-safe proposal registry
    private readonly ConcurrentDictionary<(string TenantId, Guid ProposalId), B2BProposal> _proposals = new();

    // In-memory thread-safe usage metric ledger
    private readonly ConcurrentBag<UsageRecord> _usageRecords = new();

    private sealed record UsageRecord(
        string TenantId,
        UsageMetricType MetricType,
        long Quantity,
        DateTime TimestampUtc
    );

    public QuoteCalculationResult CalculateQuote(CalculateQuoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var items = request.Items ?? [];

        decimal subtotal = 0m;
        decimal lineDiscounts = 0m;
        decimal totalCost = 0m;

        foreach (var item in items)
        {
            var gross = item.Quantity * item.UnitPrice;
            subtotal += gross;
            totalCost += item.Quantity * item.CostPrice;

            if (item.DiscountPercent > 0)
            {
                lineDiscounts += Math.Round(gross * (item.DiscountPercent / 100m), 2);
            }
            else if (item.TotalPrice > 0 && item.TotalPrice < gross)
            {
                lineDiscounts += (gross - item.TotalPrice);
            }
        }

        var netAfterLineDiscounts = Math.Max(0m, subtotal - lineDiscounts);

        decimal customDiscount = 0m;
        if (request.CustomDiscountPercent > 0)
        {
            customDiscount = Math.Round(netAfterLineDiscounts * (request.CustomDiscountPercent / 100m), 2);
        }

        var discountTotal = lineDiscounts + customDiscount;
        var netPreTax = Math.Max(0m, subtotal - discountTotal);
        var taxAmount = Math.Round(netPreTax * StandardVatRate, 2);
        var totalAmount = netPreTax + taxAmount;

        // Margin calculation on pre-tax revenue
        decimal marginPercent = 0m;
        if (netPreTax > 0m)
        {
            var grossProfit = netPreTax - totalCost;
            marginPercent = Math.Round((grossProfit / netPreTax) * 100m, 2);
        }

        bool marginApproved = marginPercent >= MinimumMarginThreshold;
        string marginWarning = marginApproved
            ? string.Empty
            : $"Margin {marginPercent:F2}% is below the required floor threshold of {MinimumMarginThreshold:F2}%. Executive approval required.";

        return new QuoteCalculationResult(
            Subtotal: subtotal,
            DiscountTotal: discountTotal,
            TaxAmount: taxAmount,
            TotalAmount: totalAmount,
            TotalCost: totalCost,
            MarginPercent: marginPercent,
            MarginApproved: marginApproved,
            MarginWarning: marginWarning
        );
    }

    public B2BProposal CreateProposal(string tenantId, string customerCompany, string customerEmail, List<QuoteLineItem> items, decimal discountPercent = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerCompany);

        var calculatedItems = (items ?? []).Select(item =>
        {
            var gross = item.Quantity * item.UnitPrice;
            var lineDiscount = item.DiscountPercent > 0 ? Math.Round(gross * (item.DiscountPercent / 100m), 2) : 0m;
            var total = gross - lineDiscount;
            return item with { TotalPrice = total };
        }).ToList();

        var calcResult = CalculateQuote(new CalculateQuoteRequest(tenantId, customerCompany, calculatedItems, discountPercent));

        var proposal = new B2BProposal(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerCompany: customerCompany,
            CustomerEmail: customerEmail,
            LineItems: calculatedItems,
            TotalAmount: calcResult.TotalAmount,
            MarginPercent: calcResult.MarginPercent,
            Status: ProposalStatus.Draft,
            SignedPdfHash: null,
            SignerName: null,
            SignedAtUtc: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        _proposals[(tenantId, proposal.Id)] = proposal;
        return proposal;
    }

    public B2BProposal SignProposal(SignProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SignerName);

        if (!_proposals.TryGetValue((request.TenantId, request.ProposalId), out var proposal))
        {
            throw new KeyNotFoundException($"Proposal '{request.ProposalId}' for tenant '{request.TenantId}' was not found.");
        }

        if (proposal.Status == ProposalStatus.Signed)
        {
            return proposal;
        }

        var signedAt = DateTime.UtcNow;

        // Deterministic cryptographic SHA-256 digital seal
        var sealPayload = $"{proposal.Id:D}|{request.TenantId}|{proposal.CustomerCompany}|{proposal.TotalAmount:F2}|{request.SignerName}|{request.SignerEmail}|{request.SignerIp}|{signedAt:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sealPayload));
        var signedPdfHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var signedProposal = proposal with
        {
            Status = ProposalStatus.Signed,
            SignedPdfHash = signedPdfHash,
            SignerName = request.SignerName,
            SignedAtUtc = signedAt
        };

        _proposals[(request.TenantId, request.ProposalId)] = signedProposal;
        return signedProposal;
    }

    public B2BProposal? GetProposal(string tenantId, Guid proposalId)
    {
        return _proposals.TryGetValue((tenantId, proposalId), out var proposal) ? proposal : null;
    }

    public void RecordUsage(RecordUsageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);

        _usageRecords.Add(new UsageRecord(request.TenantId, request.MetricType, request.Quantity, DateTime.UtcNow));
    }

    public UsageInvoiceSummary GenerateUsageInvoice(string tenantId, DateTime periodStartUtc, DateTime periodEndUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var tenantRecords = _usageRecords
            .Where(r => r.TenantId == tenantId && r.TimestampUtc >= periodStartUtc && r.TimestampUtc <= periodEndUtc)
            .ToList();

        var consumedUnits = new Dictionary<UsageMetricType, long>();
        var overageCharges = new Dictionary<UsageMetricType, decimal>();

        foreach (var metric in Enum.GetValues<UsageMetricType>())
        {
            long consumed = tenantRecords.Where(r => r.MetricType == metric).Sum(r => r.Quantity);
            consumedUnits[metric] = consumed;

            decimal charge = metric switch
            {
                // over 100,000 requests, $0.001 per req
                UsageMetricType.ApiRequests => Math.Max(0, consumed - 100_000L) * 0.001m,

                // over 50,000 events, $0.002 per event
                UsageMetricType.CapiConversions => Math.Max(0, consumed - 50_000L) * 0.002m,

                // over 1,000,000 tokens, $0.005 per 1k tokens
                UsageMetricType.AiTokens => (Math.Max(0, consumed - 1_000_000L) / 1000m) * 0.005m,

                // over 25,000 contacts, $0.01 per contact
                UsageMetricType.ActiveContacts => Math.Max(0, consumed - 25_000L) * 0.01m,

                _ => 0m
            };

            overageCharges[metric] = Math.Round(charge, 2);
        }

        var totalOverageAmount = Math.Round(overageCharges.Values.Sum(), 2);

        return new UsageInvoiceSummary(
            TenantId: tenantId,
            PeriodStartUtc: periodStartUtc,
            PeriodEndUtc: periodEndUtc,
            ConsumedUnits: consumedUnits,
            OverageCharges: overageCharges,
            TotalOverageAmount: totalOverageAmount
        );
    }
}
