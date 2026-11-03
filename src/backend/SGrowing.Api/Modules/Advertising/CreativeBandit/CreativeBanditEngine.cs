using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.CreativeBandit;

public interface ICreativeBanditEngine
{
    BanditOptimizationReport GenerateVariants(GenerateVariantsRequest request);
    BanditOptimizationReport? GetCampaignReport(string campaignId);
    BanditOptimizationReport AllocateTraffic(AllocateTrafficRequest request);
    CreativeVariant SynthesizeMutant(string campaignId, string parentVariantId);
}

public class CreativeBanditEngine : ICreativeBanditEngine
{
    private readonly ConcurrentDictionary<string, List<CreativeVariant>> _campaignVariants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new(42);

    public CreativeBanditEngine()
    {
        // Seed default campaign
        var defaultVariants = new List<CreativeVariant>
        {
            new("var_101", "camp_growth_q1", HookAngle.PainPointAgitation, "Stop Burning Ad Spend on Unqualified Traffic", VisualTheme.HighContrastCyber, "Deploy Free Autopilot", 12500, 312, 0.038, 0.025, 312.0, 12188.0, 45.0, false),
            new("var_102", "camp_growth_q1", HookAngle.DirectOutcomeTransformation, "Scale Your ARR 3.2x in 90 Days Without New SDRs", VisualTheme.MinimalistDark, "Claim Live Demo", 9800, 265, 0.042, 0.027, 265.0, 9535.0, 35.0, false),
            new("var_103", "camp_growth_q1", HookAngle.SocialProofCelebrity, "Why Top YC & Series B Founders Switched to SGrowing", VisualTheme.EnterpriseCleanLight, "Read Customer Case Studies", 5200, 78, 0.021, 0.015, 78.0, 5122.0, 20.0, true) // Fatigued
        };

        _campaignVariants["camp_growth_q1"] = defaultVariants;
    }

    public BanditOptimizationReport GenerateVariants(GenerateVariantsRequest request)
    {
        var variants = new List<CreativeVariant>();
        int index = 1;

        var angles = (request.TargetAngles != null && request.TargetAngles.Count > 0)
            ? request.TargetAngles
            : new List<HookAngle> { HookAngle.PainPointAgitation, HookAngle.DirectOutcomeTransformation, HookAngle.ContrarianCuriosity };

        foreach (var angle in angles)
        {
            var headline = angle switch
            {
                HookAngle.PainPointAgitation => $"Are Manual Funnels Leaking Your Pipeline in {DateTime.UtcNow.Year}?",
                HookAngle.DirectOutcomeTransformation => $"Automate Inbound Lead Conversion with Autonomous Swarms",
                HookAngle.ContrarianCuriosity => $"The Death of Traditional SDR Cold Email: What Works Today",
                _ => $"Experience Intelligent Growth Orchestration at Scale"
            };

            var theme = (VisualTheme)(index % 4);
            var cta = index % 2 == 0 ? "Book Growth Audit" : "Start 14-Day Pilot";

            variants.Add(new CreativeVariant(
                $"var_{request.CampaignId}_{index}",
                request.CampaignId,
                angle,
                headline,
                theme,
                cta,
                1000,
                25,
                0.035,
                0.025,
                25.0,
                975.0,
                Math.Round(100.0 / angles.Count, 1),
                false
            ));
            index++;
        }

        _campaignVariants[request.CampaignId] = variants;
        return BuildReport(request.CampaignId, variants, 0);
    }

    public BanditOptimizationReport? GetCampaignReport(string campaignId)
    {
        if (!_campaignVariants.TryGetValue(campaignId, out var variants))
        {
            return null;
        }

        return BuildReport(campaignId, variants, 0);
    }

    public BanditOptimizationReport AllocateTraffic(AllocateTrafficRequest request)
    {
        if (!_campaignVariants.TryGetValue(request.CampaignId, out var variants) || variants.Count == 0)
        {
            variants = GenerateVariants(new GenerateVariantsRequest(request.CampaignId, "Default SaaS", null)).Variants;
        }

        // Thompson Sampling simulation via sampling from Beta approximations
        var scores = new Dictionary<string, double>();
        foreach (var v in variants)
        {
            if (v.IsFatigued)
            {
                scores[v.VariantId] = 0.05; // Exploration remnant
                continue;
            }

            // Mean estimate with slight exploration noise
            double mean = v.Alpha / (v.Alpha + v.Beta);
            double sampledValue = mean + ((_random.NextDouble() - 0.5) * 0.005);
            scores[v.VariantId] = Math.Max(0.001, sampledValue);
        }

        double totalScore = scores.Values.Sum();
        var updated = variants.Select(v =>
        {
            var rawShare = (scores[v.VariantId] / totalScore) * 100.0;
            var allocatedShare = Math.Round(rawShare, 1);
            var newImpressions = v.Impressions + (int)(request.TotalImpressionsToAllocate * (allocatedShare / 100.0));
            var newConversions = v.Conversions + (int)(newImpressions * v.ConversionRate * 0.1);

            return v with
            {
                AllocatedShare = allocatedShare,
                Impressions = newImpressions,
                Conversions = newConversions,
                Alpha = newConversions,
                Beta = Math.Max(1.0, newImpressions - newConversions)
            };
        }).ToList();

        _campaignVariants[request.CampaignId] = updated;
        return BuildReport(request.CampaignId, updated, 0);
    }

    public CreativeVariant SynthesizeMutant(string campaignId, string parentVariantId)
    {
        if (!_campaignVariants.TryGetValue(campaignId, out var variants))
        {
            variants = new List<CreativeVariant>();
        }

        var parent = variants.FirstOrDefault(v => v.VariantId == parentVariantId) ?? variants.FirstOrDefault()
            ?? new CreativeVariant("var_base", campaignId, HookAngle.ContrarianCuriosity, "Autonomous Inbound Growth Engine", VisualTheme.HighContrastCyber, "Explore Now", 500, 15, 0.03, 0.03, 15, 485, 20.0, false);

        var mutant = new CreativeVariant(
            $"mutant_{DateTime.UtcNow.Ticks % 10000}",
            campaignId,
            parent.Angle,
            $"[AI-Evolved] {parent.Headline}",
            (VisualTheme)(((int)parent.Theme + 1) % 4),
            parent.CallToAction,
            100,
            4,
            0.045,
            0.040,
            4.0,
            96.0,
            15.0,
            false
        );

        variants.Add(mutant);
        _campaignVariants[campaignId] = variants;
        return mutant;
    }

    private static BanditOptimizationReport BuildReport(string campaignId, List<CreativeVariant> variants, int mutations)
    {
        var leading = variants.OrderByDescending(v => v.ConversionRate).FirstOrDefault()?.VariantId ?? "none";
        var totalConv = variants.Sum(v => v.Conversions);
        var totalImp = variants.Sum(v => v.Impressions);
        var blendedRate = totalImp > 0 ? Math.Round((double)totalConv / totalImp, 4) : 0.0;

        return new BanditOptimizationReport(
            campaignId,
            variants,
            leading,
            totalConv,
            blendedRate,
            mutations
        );
    }
}
