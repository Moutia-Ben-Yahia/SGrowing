using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.RetargetingMatrix;

public interface IRetargetingMatrixEngine
{
    ImpressionDecision EvaluateImpression(EvaluateImpressionRequest request);
    BuyerRetargetingProfile? GetProfile(string accountDomain);
    BuyerRetargetingProfile UpdateStage(UpdateBuyerStageRequest request);
}

public class RetargetingMatrixEngine : IRetargetingMatrixEngine
{
    private readonly ConcurrentDictionary<string, BuyerRetargetingProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public RetargetingMatrixEngine()
    {
        // Seed initial buyer profiles
        _profiles["nexustech.io"] = new BuyerRetargetingProfile(
            "nexustech.io",
            "NexusTech Systems",
            RetargetingStage.PricingPageEvaluator,
            ImpressionsLast24h: 1,
            MaxDailyCap: 3,
            DateTime.UtcNow.AddHours(-3),
            IsSuppressed: false
        );

        _profiles["capprofit.com"] = new BuyerRetargetingProfile(
            "capprofit.com",
            "Capital Profit Group",
            RetargetingStage.DemoAttendee,
            ImpressionsLast24h: 3, // At frequency cap!
            MaxDailyCap: 3,
            DateTime.UtcNow.AddMinutes(-30),
            IsSuppressed: false
        );

        _profiles["churnedlead.org"] = new BuyerRetargetingProfile(
            "churnedlead.org",
            "Churned Lead Org",
            RetargetingStage.SuppressedNegativeIntent,
            ImpressionsLast24h: 0,
            MaxDailyCap: 3,
            DateTime.UtcNow.AddDays(-10),
            IsSuppressed: true
        );
    }

    public ImpressionDecision EvaluateImpression(EvaluateImpressionRequest request)
    {
        if (!_profiles.TryGetValue(request.AccountDomain, out var profile))
        {
            profile = new BuyerRetargetingProfile(
                request.AccountDomain,
                request.AccountDomain,
                RetargetingStage.PricingPageEvaluator,
                0,
                3,
                DateTime.UtcNow.AddDays(-1),
                false
            );
            _profiles[request.AccountDomain] = profile;
        }

        if (profile.IsSuppressed || profile.Stage == RetargetingStage.SuppressedNegativeIntent || profile.Stage == RetargetingStage.ClosedWonCustomer)
        {
            return new ImpressionDecision(
                profile.AccountDomain,
                ShouldServeImpression: false,
                SelectedCreativeTheme: "None",
                RecommendedChannel: request.Channel,
                profile.ImpressionsLast24h,
                profile.MaxDailyCap,
                "Account is suppressed (Closed Customer or Negative Intent Opt-Out)."
            );
        }

        if (profile.ImpressionsLast24h >= profile.MaxDailyCap)
        {
            return new ImpressionDecision(
                profile.AccountDomain,
                ShouldServeImpression: false,
                SelectedCreativeTheme: "None",
                RecommendedChannel: request.Channel,
                profile.ImpressionsLast24h,
                profile.MaxDailyCap,
                $"Daily frequency cap reached ({profile.ImpressionsLast24h}/{profile.MaxDailyCap}). Ad fatigue prevented."
            );
        }

        string creativeTheme = profile.Stage switch
        {
            RetargetingStage.PricingPageEvaluator => "3-Year TCO & 4.2 Month Payback ROI Deck",
            RetargetingStage.DemoAttendee => "SOC2 Type II & Zero-Trust Architecture Brief",
            RetargetingStage.StalledProposal => "Verified Enterprise Video Testimonials Carousel",
            _ => "Intelligent Growth OS Overview"
        };

        // Increment served impressions
        var updatedProfile = profile with
        {
            ImpressionsLast24h = profile.ImpressionsLast24h + 1,
            LastImpressionUtc = DateTime.UtcNow
        };
        _profiles[profile.AccountDomain] = updatedProfile;

        return new ImpressionDecision(
            profile.AccountDomain,
            ShouldServeImpression: true,
            creativeTheme,
            request.Channel,
            updatedProfile.ImpressionsLast24h,
            profile.MaxDailyCap,
            "Impression approved within frequency cap limits."
        );
    }

    public BuyerRetargetingProfile? GetProfile(string accountDomain)
    {
        _profiles.TryGetValue(accountDomain, out var profile);
        return profile;
    }

    public BuyerRetargetingProfile UpdateStage(UpdateBuyerStageRequest request)
    {
        var isSuppressed = request.NewStage == RetargetingStage.SuppressedNegativeIntent || request.NewStage == RetargetingStage.ClosedWonCustomer;

        if (_profiles.TryGetValue(request.AccountDomain, out var existing))
        {
            var updated = existing with { Stage = request.NewStage, IsSuppressed = isSuppressed };
            _profiles[request.AccountDomain] = updated;
            return updated;
        }

        var newProfile = new BuyerRetargetingProfile(
            request.AccountDomain,
            request.AccountDomain,
            request.NewStage,
            0,
            3,
            DateTime.UtcNow,
            isSuppressed
        );

        _profiles[request.AccountDomain] = newProfile;
        return newProfile;
    }
}
