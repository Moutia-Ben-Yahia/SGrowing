using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

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

    private readonly ConcurrentDictionary<(string TenantId, Guid ProposalId), B2BProposal> _proposals = new();
    private readonly ConcurrentBag<UsageRecord> _usageRecords = new();
    private readonly SGrowingDbContext? _dbContext;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<B2BProposalEngine>? _logger;
    private readonly object _dbLock = new();

    private sealed record UsageRecord(
        string TenantId,
        UsageMetricType MetricType,
        long Quantity,
        DateTime TimestampUtc
    );

    public B2BProposalEngine()
        : this((IServiceScopeFactory?)null, null)
    {
    }

    public B2BProposalEngine(IServiceScopeFactory? scopeFactory, ILogger<B2BProposalEngine>? logger = null)
        : this(null, scopeFactory, logger)
    {
    }

    public B2BProposalEngine(SGrowingDbContext? dbContext, ILogger<B2BProposalEngine>? logger = null)
        : this(dbContext, null, logger)
    {
    }

    public B2BProposalEngine(
        SGrowingDbContext? dbContext,
        IServiceScopeFactory? scopeFactory,
        ILogger<B2BProposalEngine>? logger = null)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        _logger = logger;

        WarmupFromPersistence();
    }

    private void WarmupFromPersistence()
    {
        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    LoadFromDb(_dbContext);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    LoadFromDb(db);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to warm up B2BProposalEngine cache from database.");
        }
    }

    private void LoadFromDb(SGrowingDbContext db)
    {
        var entities = db.B2BProposalRecords
            .Include(p => p.LineItems)
            .ToList();

        foreach (var entity in entities)
        {
            _proposals[(entity.TenantId, entity.Id)] = entity.ToModel();
        }

        var usageEntities = db.B2BUsageRecords.ToList();
        foreach (var u in usageEntities)
        {
            if (Enum.TryParse<UsageMetricType>(u.MetricType, true, out var metric))
            {
                _usageRecords.Add(new UsageRecord(u.TenantId, metric, u.Quantity, u.TimestampUtc));
            }
        }
    }

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
        SyncProposalToDb(proposal);
        return proposal;
    }

    public B2BProposal SignProposal(SignProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SignerName);

        var proposal = GetProposal(request.TenantId, request.ProposalId);
        if (proposal == null)
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
        SyncProposalToDb(signedProposal);
        return signedProposal;
    }

    public B2BProposal? GetProposal(string tenantId, Guid proposalId)
    {
        if (_proposals.TryGetValue((tenantId, proposalId), out var proposal))
        {
            return proposal;
        }

        var loaded = LoadProposalFromDb(tenantId, proposalId);
        if (loaded != null)
        {
            _proposals[(tenantId, proposalId)] = loaded;
            return loaded;
        }

        return null;
    }

    private B2BProposal? LoadProposalFromDb(string tenantId, Guid proposalId)
    {
        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    var entity = _dbContext.B2BProposalRecords
                        .Include(p => p.LineItems)
                        .FirstOrDefault(p => p.Id == proposalId && p.TenantId == tenantId);
                    return entity?.ToModel();
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    var entity = db.B2BProposalRecords
                        .Include(p => p.LineItems)
                        .FirstOrDefault(p => p.Id == proposalId && p.TenantId == tenantId);
                    return entity?.ToModel();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load proposal {ProposalId} from database", proposalId);
        }

        return null;
    }

    private void SyncProposalToDb(B2BProposal proposal)
    {
        if (_dbContext == null && _scopeFactory == null) return;

        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    SaveProposalToDbContext(_dbContext, proposal);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    SaveProposalToDbContext(db, proposal);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist B2BProposal {ProposalId} to database.", proposal.Id);
        }
    }

    private static void SaveProposalToDbContext(SGrowingDbContext db, B2BProposal proposal)
    {
        var existing = db.B2BProposalRecords
            .Include(p => p.LineItems)
            .FirstOrDefault(p => p.Id == proposal.Id);

        if (existing == null)
        {
            var entity = proposal.ToEntity();
            db.B2BProposalRecords.Add(entity);
        }
        else
        {
            existing.CustomerCompany = proposal.CustomerCompany;
            existing.CustomerEmail = proposal.CustomerEmail;
            existing.TotalAmount = proposal.TotalAmount;
            existing.MarginPercent = proposal.MarginPercent;
            existing.Status = proposal.Status.ToString();
            existing.SignedPdfHash = proposal.SignedPdfHash;
            existing.SignerName = proposal.SignerName;
            existing.SignedAtUtc = proposal.SignedAtUtc;
            existing.UpdatedAtUtc = DateTime.UtcNow;

            if (proposal.LineItems != null && proposal.LineItems.Count > 0)
            {
                existing.LineItems.Clear();
                foreach (var item in proposal.LineItems)
                {
                    existing.LineItems.Add(item.ToEntity(proposal.Id));
                }
            }
        }

        db.SaveChanges();
    }

    public void RecordUsage(RecordUsageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);

        var now = DateTime.UtcNow;
        _usageRecords.Add(new UsageRecord(request.TenantId, request.MetricType, request.Quantity, now));

        if (_dbContext != null || _scopeFactory != null)
        {
            try
            {
                var recordEntity = new B2BUsageRecordEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    MetricType = request.MetricType.ToString(),
                    Quantity = request.Quantity,
                    TimestampUtc = now
                };

                if (_dbContext != null)
                {
                    lock (_dbLock)
                    {
                        _dbContext.B2BUsageRecords.Add(recordEntity);
                        _dbContext.SaveChanges();
                    }
                }
                else if (_scopeFactory != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                    if (db != null)
                    {
                        db.B2BUsageRecords.Add(recordEntity);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to persist B2B usage record to database.");
            }
        }
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
                UsageMetricType.ApiRequests => Math.Max(0, consumed - 100_000L) * 0.001m,
                UsageMetricType.CapiConversions => Math.Max(0, consumed - 50_000L) * 0.002m,
                UsageMetricType.AiTokens => (Math.Max(0, consumed - 1_000_000L) / 1000m) * 0.005m,
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
