using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.InfluencerEscrowV2;

public record InfluencerCampaign(
    string CampaignId,
    string CreatorHandle,
    string Platform,
    decimal TotalEscrowLockedUsd,
    decimal ReleasedPayoutUsd,
    int AttributedConversions,
    decimal AttributedGmvUsd,
    double FraudRiskScore,
    bool IsActive
);

public record ReleaseMilestonePayoutRequest(
    string CampaignId,
    decimal AmountToReleaseUsd
);

public record AttributeConversionRequest(
    string CampaignId,
    decimal OrderGmvUsd,
    bool IsFraudSuspected
);
