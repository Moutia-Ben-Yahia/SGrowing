using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Commerce.CpqApproval;

public interface ICpqApprovalEngine
{
    CpqApprovalOverview GetOverview();
    QuoteApprovalRecord SubmitQuote(QuoteApprovalRequest request);
    QuoteApprovalRecord ApproveQuote(ApproveQuoteCommand command);
    QuoteApprovalRecord RejectQuote(RejectQuoteCommand command);
    QuoteApprovalRecord EscalateQuote(string quoteId, string reason);
}

public class CpqApprovalEngine : ICpqApprovalEngine
{
    private readonly ConcurrentDictionary<string, QuoteApprovalRecord> _quotes = new();
    private readonly object _lock = new();

    public CpqApprovalEngine()
    {
        SeedInitialQuotes();
    }

    private void SeedInitialQuotes()
    {
        var now = DateTime.UtcNow;

        var q1 = new QuoteApprovalRecord(
            QuoteId: "QUOTE-2026-881",
            AccountName: "Vertex Global Enterprises",
            TotalListPrice: 180000m,
            DiscountPercentage: 18.5m,
            NetPrice: 146700m,
            PaymentTerms: "Net 30",
            RequiredTier: ApprovalTier.SalesManager,
            Status: QuoteApprovalStatus.PendingApproval,
            RequesterName: "Sarah Connor (Enterprise AE)",
            RequestedAt: now.AddHours(-2),
            ApprovedBy: null,
            ApprovedAt: null,
            AuditToken: null,
            SlaMinutesRemaining: 120,
            IsSlaBreached: false,
            Justification: "Competitive bakeoff against legacy vendor; 3-year commitment"
        );

        var q2 = new QuoteApprovalRecord(
            QuoteId: "QUOTE-2026-882",
            AccountName: "Apex FinTech Holdings",
            TotalListPrice: 420000m,
            DiscountPercentage: 28.0m,
            NetPrice: 302400m,
            PaymentTerms: "Net 60",
            RequiredTier: ApprovalTier.SalesDirector,
            Status: QuoteApprovalStatus.PendingApproval,
            RequesterName: "David Kim (Strategic Accounts)",
            RequestedAt: now.AddHours(-7),
            ApprovedBy: null,
            ApprovedAt: null,
            AuditToken: null,
            SlaMinutesRemaining: 60,
            IsSlaBreached: false,
            Justification: "Global rollout across 12 subsidiaries; requested standard Net 60 billing"
        );

        var q3 = new QuoteApprovalRecord(
            QuoteId: "QUOTE-2026-883",
            AccountName: "Nordic Health Telematics",
            TotalListPrice: 850000m,
            DiscountPercentage: 42.0m,
            NetPrice: 493000m,
            PaymentTerms: "Net 90",
            RequiredTier: ApprovalTier.CroBoardGate,
            Status: QuoteApprovalStatus.PendingApproval,
            RequesterName: "Elena Rostova (Global VP)",
            RequestedAt: now.AddHours(-26),
            ApprovedBy: null,
            ApprovedAt: null,
            AuditToken: null,
            SlaMinutesRemaining: 0,
            IsSlaBreached: true,
            Justification: "Market penetration marquee anchor customer; 5-year multi-million TCV"
        );

        var q4 = new QuoteApprovalRecord(
            QuoteId: "QUOTE-2026-880",
            AccountName: "Helios Energy Systems",
            TotalListPrice: 95000m,
            DiscountPercentage: 8.0m,
            NetPrice: 87400m,
            PaymentTerms: "Net 30",
            RequiredTier: ApprovalTier.RepAutonomous,
            Status: QuoteApprovalStatus.Approved,
            RequesterName: "Marcus Vance (Commercial AE)",
            RequestedAt: now.AddDays(-1),
            ApprovedBy: "Automated Governance Gate",
            ApprovedAt: now.AddDays(-1).AddMinutes(1),
            AuditToken: "CPQ-TOKEN-AUTONOMOUS-880-VERIFIED",
            SlaMinutesRemaining: 0,
            IsSlaBreached: false,
            Justification: "Standard standard rep volume discount"
        );

        _quotes[q1.QuoteId] = q1;
        _quotes[q2.QuoteId] = q2;
        _quotes[q3.QuoteId] = q3;
        _quotes[q4.QuoteId] = q4;
    }

    public CpqApprovalOverview GetOverview()
    {
        var list = _quotes.Values.OrderByDescending(q => q.RequestedAt).ToList();
        var pending = list.Where(q => q.Status == QuoteApprovalStatus.PendingApproval || q.Status == QuoteApprovalStatus.Escalated).ToList();
        var approved = list.Where(q => q.Status == QuoteApprovalStatus.Approved).ToList();

        var pendingCount = pending.Count;
        var pendingValue = pending.Sum(q => q.NetPrice);
        var breaches = pending.Count(q => q.IsSlaBreached);
        var avgDiscount = approved.Count > 0 ? approved.Average(q => q.DiscountPercentage) : 0m;

        return new CpqApprovalOverview(
            PendingApprovalsCount: pendingCount,
            TotalPendingValue: pendingValue,
            SlaBreachesCount: breaches,
            AverageApprovedDiscount: Math.Round(avgDiscount, 1),
            Quotes: list
        );
    }

    public QuoteApprovalRecord SubmitQuote(QuoteApprovalRequest request)
    {
        var tier = DetermineRequiredTier(request.DiscountPercentage, request.PaymentTerms);
        var netPrice = request.TotalListPrice * (1.0m - (request.DiscountPercentage / 100.0m));
        var now = DateTime.UtcNow;

        bool isAutonomous = tier == ApprovalTier.RepAutonomous;
        var status = isAutonomous ? QuoteApprovalStatus.Approved : QuoteApprovalStatus.PendingApproval;
        string? token = isAutonomous ? GenerateAuditToken(request.QuoteId, "AutonomousGate", request.DiscountPercentage) : null;
        string? approver = isAutonomous ? "Automated Governance Matrix" : null;
        DateTime? approvedAt = isAutonomous ? now : null;

        var record = new QuoteApprovalRecord(
            QuoteId: request.QuoteId,
            AccountName: request.AccountName,
            TotalListPrice: request.TotalListPrice,
            DiscountPercentage: request.DiscountPercentage,
            NetPrice: Math.Round(netPrice, 2),
            PaymentTerms: request.PaymentTerms,
            RequiredTier: tier,
            Status: status,
            RequesterName: request.RequesterName,
            RequestedAt: now,
            ApprovedBy: approver,
            ApprovedAt: approvedAt,
            AuditToken: token,
            SlaMinutesRemaining: isAutonomous ? 0 : GetInitialSlaMinutes(tier),
            IsSlaBreached: false,
            Justification: request.Justification
        );

        _quotes[record.QuoteId] = record;
        return record;
    }

    public QuoteApprovalRecord ApproveQuote(ApproveQuoteCommand command)
    {
        if (!_quotes.TryGetValue(command.QuoteId, out var existing))
        {
            throw new KeyNotFoundException($"Quote {command.QuoteId} not found.");
        }

        var token = GenerateAuditToken(command.QuoteId, command.ApproverId, existing.DiscountPercentage);
        var updated = existing with
        {
            Status = QuoteApprovalStatus.Approved,
            ApprovedBy = command.ApproverName,
            ApprovedAt = DateTime.UtcNow,
            AuditToken = token,
            SlaMinutesRemaining = 0,
            IsSlaBreached = false
        };

        _quotes[command.QuoteId] = updated;
        return updated;
    }

    public QuoteApprovalRecord RejectQuote(RejectQuoteCommand command)
    {
        if (!_quotes.TryGetValue(command.QuoteId, out var existing))
        {
            throw new KeyNotFoundException($"Quote {command.QuoteId} not found.");
        }

        var updated = existing with
        {
            Status = QuoteApprovalStatus.Rejected,
            ApprovedBy = $"{command.ApproverName} (Rejected: {command.Reason})",
            ApprovedAt = DateTime.UtcNow,
            SlaMinutesRemaining = 0
        };

        _quotes[command.QuoteId] = updated;
        return updated;
    }

    public QuoteApprovalRecord EscalateQuote(string quoteId, string reason)
    {
        if (!_quotes.TryGetValue(quoteId, out var existing))
        {
            throw new KeyNotFoundException($"Quote {quoteId} not found.");
        }

        var nextTier = existing.RequiredTier switch
        {
            ApprovalTier.RepAutonomous => ApprovalTier.SalesManager,
            ApprovalTier.SalesManager => ApprovalTier.SalesDirector,
            _ => ApprovalTier.CroBoardGate
        };

        var updated = existing with
        {
            RequiredTier = nextTier,
            Status = QuoteApprovalStatus.Escalated,
            SlaMinutesRemaining = 180,
            IsSlaBreached = false,
            Justification = $"{existing.Justification} | ESCALATED: {reason}"
        };

        _quotes[quoteId] = updated;
        return updated;
    }

    private static ApprovalTier DetermineRequiredTier(decimal discountPct, string paymentTerms)
    {
        if (discountPct > 35.0m || paymentTerms == "Net 90")
            return ApprovalTier.CroBoardGate;
        if (discountPct > 20.0m || paymentTerms == "Net 60")
            return ApprovalTier.SalesDirector;
        if (discountPct > 10.0m)
            return ApprovalTier.SalesManager;
        return ApprovalTier.RepAutonomous;
    }

    private static int GetInitialSlaMinutes(ApprovalTier tier) => tier switch
    {
        ApprovalTier.RepAutonomous => 0,
        ApprovalTier.SalesManager => 240,   // 4 hours
        ApprovalTier.SalesDirector => 480,  // 8 hours
        ApprovalTier.CroBoardGate => 1440,  // 24 hours
        _ => 240
    };

    private static string GenerateAuditToken(string quoteId, string approverId, decimal discount)
    {
        var raw = $"{quoteId}:{approverId}:{discount}:{DateTime.UtcNow:yyyyMMddHHmmss}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("SGrowing-CPQ-Governance-Salt-2026"));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return $"CPQ-AUDIT-{Convert.ToHexString(hash)[..16]}";
    }
}
