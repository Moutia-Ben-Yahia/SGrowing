using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.CreativeBandit;

public enum HookAngle
{
    PainPointAgitation,
    DirectOutcomeTransformation,
    SocialProofCelebrity,
    ContrarianCuriosity
}

public enum VisualTheme
{
    MinimalistDark,
    HighContrastCyber,
    EnterpriseCleanLight,
    CinematicGraded
}

public record CreativeVariant(
    string VariantId,
    string CampaignId,
    HookAngle Angle,
    string Headline,
    VisualTheme Theme,
    string CallToAction,
    int Impressions,
    int Conversions,
    double CTR,
    double ConversionRate,
    double Alpha, // Thompson Sampling success parameter
    double Beta,  // Thompson Sampling failure parameter
    double AllocatedShare, // % of traffic (0.0 to 100.0)
    bool IsFatigued
);

public record BanditOptimizationReport(
    string CampaignId,
    List<CreativeVariant> Variants,
    string LeadingVariantId,
    double TotalConversions,
    double BlendedConversionRate,
    int MutationsSynthesized
);

public record GenerateVariantsRequest(
    string CampaignId,
    string BaseOfferDescription,
    List<HookAngle>? TargetAngles
);

public record AllocateTrafficRequest(
    string CampaignId,
    int TotalImpressionsToAllocate
);
