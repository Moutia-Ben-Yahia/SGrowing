using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Sales.DirectMailMesh;

public sealed record DirectMailOverview(
    int ActiveCampaignsCount,
    int PackagesDispatchedCount,
    double DeliverySuccessRatePct,
    double PipelineInfluenceRatePct,
    IReadOnlyList<DirectMailCampaign> ActiveCampaigns,
    IReadOnlyList<DispatchedPackageRecord> RecentDispatches
);

public sealed record DirectMailCampaign(
    string CampaignId,
    string CampaignName,
    string TargetTier,
    string GiftType,
    decimal UnitCostUsd,
    string TriggerStage,
    string Status
);

public sealed record DispatchedPackageRecord(
    string TrackingNumber,
    string CampaignId,
    string RecipientExecutiveName,
    string CompanyName,
    string Carrier,
    string DeliveryStatus,
    DateTime DispatchedAt
);

public sealed record DispatchCampaignRequest(
    string CampaignId,
    string RecipientExecutiveName,
    string CompanyName,
    string RecipientAddress,
    string Carrier,
    string PersonalizedNote
);

public sealed record DispatchReceipt(
    string DispatchId,
    string TrackingNumber,
    string Carrier,
    string EstimatedDeliveryDate,
    string ComplianceVerificationCode,
    DateTime Timestamp
);

public sealed record VerifyGiftComplianceRequest(
    string RecipientCompany,
    string RecipientTitle,
    decimal GiftValueUsd,
    string Jurisdiction
);

public sealed record GiftComplianceVerdict(
    string RecipientCompany,
    bool IsCompliant,
    decimal MaxAllowableValueUsd,
    string ApplicablePolicyRule,
    string AuditReceiptId
);
