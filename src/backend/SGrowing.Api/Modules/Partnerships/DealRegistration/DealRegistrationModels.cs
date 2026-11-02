using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Partnerships.DealRegistration;

public enum PartnerTier
{
    RegisteredReferral = 10,
    CertifiedSolutionProvider = 15,
    PremierGlobalIntegrator = 25
}

public enum DealStatus
{
    PendingReview,
    ApprovedLocked,
    CollisionRejected,
    ClosedWon,
    Expired
}

public record RegisterPartnerDealRequest(
    string PartnerId,
    string PartnerName,
    PartnerTier Tier,
    string ProspectCompanyName,
    string ProspectDomain,
    string ContactName,
    string ContactEmail,
    decimal EstimatedArr,
    string SolutionScopeDescription
);

public record RegisteredDealRecord(
    string DealId,
    string PartnerId,
    string PartnerName,
    PartnerTier Tier,
    string ProspectCompanyName,
    string ProspectDomain,
    decimal EstimatedArr,
    decimal ProjectedPartnerCommission,
    DealStatus Status,
    DateTime ExclusivityExpirationUtc,
    DateTime RegisteredAtUtc
);

public record ApproveDealRequest(
    string DealId,
    string ApproverEmail
);

public record ApproveDealResult(
    string DealId,
    DealStatus NewStatus,
    string ConfirmationMessage,
    DateTime ExpirationUtc
);
