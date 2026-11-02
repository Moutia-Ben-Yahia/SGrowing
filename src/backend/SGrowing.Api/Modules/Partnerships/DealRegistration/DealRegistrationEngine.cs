using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Partnerships.DealRegistration;

public interface IDealRegistrationEngine
{
    RegisteredDealRecord RegisterDeal(RegisterPartnerDealRequest request);
    IReadOnlyList<RegisteredDealRecord> GetAllDeals();
    ApproveDealResult ApproveDeal(ApproveDealRequest request);
}

public class DealRegistrationEngine : IDealRegistrationEngine
{
    private readonly ConcurrentDictionary<string, RegisteredDealRecord> _deals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _domainLocks = new(StringComparer.OrdinalIgnoreCase); // domain -> dealId

    public DealRegistrationEngine()
    {
        var d1 = new RegisteredDealRecord(
            DealId: "deal_partner_701",
            PartnerId: "ptn_accenture_cloud",
            PartnerName: "Accenture Cloud First",
            Tier: PartnerTier.PremierGlobalIntegrator,
            ProspectCompanyName: "Global Telecommunications AG",
            ProspectDomain: "globaltelecom.de",
            EstimatedArr: 350000m,
            ProjectedPartnerCommission: 350000m * 0.25m,
            Status: DealStatus.ApprovedLocked,
            ExclusivityExpirationUtc: DateTime.UtcNow.AddDays(75),
            RegisteredAtUtc: DateTime.UtcNow.AddDays(-15)
        );

        var d2 = new RegisteredDealRecord(
            DealId: "deal_partner_702",
            PartnerId: "ptn_growth_consulting",
            PartnerName: "ScaleForward Agency",
            Tier: PartnerTier.CertifiedSolutionProvider,
            ProspectCompanyName: "Nordic Logistics Nordic AB",
            ProspectDomain: "nordiclogistics.se",
            EstimatedArr: 120000m,
            ProjectedPartnerCommission: 120000m * 0.15m,
            Status: DealStatus.PendingReview,
            ExclusivityExpirationUtc: DateTime.UtcNow.AddDays(90),
            RegisteredAtUtc: DateTime.UtcNow.AddHours(-4)
        );

        _deals[d1.DealId] = d1;
        _domainLocks[d1.ProspectDomain] = d1.DealId;

        _deals[d2.DealId] = d2;
        _domainLocks[d2.ProspectDomain] = d2.DealId;
    }

    public RegisteredDealRecord RegisterDeal(RegisterPartnerDealRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domain = request.ProspectDomain.Trim().ToLowerInvariant();
        var dealId = $"deal_{Guid.NewGuid():N}"[..12];

        // Collision check
        if (_domainLocks.TryGetValue(domain, out var lockedDealId))
        {
            var collision = new RegisteredDealRecord(
                DealId: dealId,
                PartnerId: request.PartnerId,
                PartnerName: request.PartnerName,
                Tier: request.Tier,
                ProspectCompanyName: request.ProspectCompanyName,
                ProspectDomain: domain,
                EstimatedArr: request.EstimatedArr,
                ProjectedPartnerCommission: 0m,
                Status: DealStatus.CollisionRejected,
                ExclusivityExpirationUtc: DateTime.UtcNow,
                RegisteredAtUtc: DateTime.UtcNow
            );
            _deals[dealId] = collision;
            return collision;
        }

        var commissionRate = ((decimal)(int)request.Tier) / 100m;
        var commission = Math.Round(request.EstimatedArr * commissionRate, 2);

        var record = new RegisteredDealRecord(
            DealId: dealId,
            PartnerId: request.PartnerId,
            PartnerName: request.PartnerName,
            Tier: request.Tier,
            ProspectCompanyName: request.ProspectCompanyName,
            ProspectDomain: domain,
            EstimatedArr: request.EstimatedArr,
            ProjectedPartnerCommission: commission,
            Status: DealStatus.ApprovedLocked,
            ExclusivityExpirationUtc: DateTime.UtcNow.AddDays(90),
            RegisteredAtUtc: DateTime.UtcNow
        );

        _deals[dealId] = record;
        _domainLocks[domain] = dealId;

        return record;
    }

    public IReadOnlyList<RegisteredDealRecord> GetAllDeals()
    {
        return _deals.Values.OrderByDescending(d => d.RegisteredAtUtc).ToList();
    }

    public ApproveDealResult ApproveDeal(ApproveDealRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_deals.TryGetValue(request.DealId, out var existing))
        {
            var approved = existing with
            {
                Status = DealStatus.ApprovedLocked,
                ExclusivityExpirationUtc = DateTime.UtcNow.AddDays(90)
            };
            _deals[request.DealId] = approved;

            return new ApproveDealResult(
                DealId: request.DealId,
                NewStatus: DealStatus.ApprovedLocked,
                ConfirmationMessage: $"Deal exclusivity approved for {approved.ProspectCompanyName} by {request.ApproverEmail}. 90-day lock secured.",
                ExpirationUtc: approved.ExclusivityExpirationUtc
            );
        }

        throw new KeyNotFoundException($"Deal registration '{request.DealId}' not found.");
    }
}
