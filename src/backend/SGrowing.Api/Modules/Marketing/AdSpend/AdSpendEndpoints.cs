using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marketing.AdSpend;

#region Request & Response Contracts

public record ChannelSpendSimulationInput(
    string ChannelId,
    string ChannelName,
    double ProposedSpend,
    HillParameters HillParams);

public record SimulateRoasRequest(
    IReadOnlyList<ChannelSpendSimulationInput> Channels,
    IReadOnlyList<CustomerJourneyPath>? CustomerJourneys = null,
    double TargetMarginalRoas = 1.0);

public record SimulateRoasResponse(
    bool Success,
    double TotalSpend,
    double TotalPredictedRevenue,
    double BlendedAverageRoas,
    IReadOnlyList<ChannelEfficiencyRanking> ChannelRankings,
    IReadOnlyList<MarkovAttributionResult>? AttributionResults,
    string? Message = null);

public record ReallocateBudgetRequest(
    IReadOnlyList<ChannelBudgetInput> Channels,
    double? TotalBudget = null,
    double? GlobalMaxShiftFraction = null,
    int? RandomSeed = null);

public record ReallocateBudgetResponse(
    bool Success,
    ReallocationPlanResult? Plan,
    string? Message = null);

public record AdChannelProfile(
    string ChannelId,
    string ChannelName,
    string Platform,
    HillParameters DefaultHillParams,
    double BaselineConversionRate,
    double SaturationHalfSpendK,
    string Notes);

public record AdChannelsResponse(
    bool Success,
    IReadOnlyList<AdChannelProfile> Channels);

public record CreativeFatigueRadarResponse(
    bool Success,
    CreativeFatigueEvaluationResult? Evaluation,
    string? Message = null);

#endregion

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Horizon 49:
/// Autonomous Cross-Channel Predictive ROAS &amp; Real-Time Ad Spend Allocator.
/// </summary>
public static class AdSpendEndpoints
{
    /// <summary>
    /// Registers all Horizon 49 Ad Spend optimization, Hill predictive ROAS, Thompson Sampling reallocation,
    /// and creative fatigue radar services into the DI container.
    /// </summary>
    public static IServiceCollection AddAdSpendServices(this IServiceCollection services)
    {
        services.AddSingleton<IPredictiveRoasEngine, PredictiveRoasEngine>();
        services.AddSingleton<IDynamicBudgetReallocationService, DynamicBudgetReallocationService>();
        services.AddSingleton<ICreativeFatigueRadarService, CreativeFatigueRadarService>();

        return services;
    }

    /// <summary>
    /// Maps Horizon 49 Ad Spend Minimal API routes under /api/marketing/adspend.
    /// </summary>
    public static IEndpointRouteBuilder MapAdSpendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/marketing/adspend")
            .WithTags("Autonomous Predictive ROAS & Ad Spend Allocator");

        // 1. POST /api/marketing/adspend/simulate-roas
        group.MapPost("/simulate-roas", (
            [FromBody] SimulateRoasRequest request,
            [FromServices] IPredictiveRoasEngine engine) =>
        {
            if (request?.Channels == null || request.Channels.Count == 0)
            {
                return Results.BadRequest(new SimulateRoasResponse(
                    Success: false,
                    TotalSpend: 0.0,
                    TotalPredictedRevenue: 0.0,
                    BlendedAverageRoas: 0.0,
                    ChannelRankings: [],
                    AttributionResults: null,
                    Message: "At least one channel is required for ROAS simulation."));
            }

            var channelTuples = request.Channels.Select(c =>
                (c.ChannelId, c.ChannelName, Math.Max(0.0, c.ProposedSpend), c.HillParams)).ToList();

            var rankings = engine.RankChannelsByEfficiency(channelTuples);

            double totalSpend = request.Channels.Sum(c => Math.Max(0.0, c.ProposedSpend));
            double totalPredictedRevenue = rankings.Sum(r => r.PredictedRevenue);
            double blendedRoas = totalSpend > 0.0 ? totalPredictedRevenue / totalSpend : 0.0;

            IReadOnlyList<MarkovAttributionResult>? attribution = null;
            if (request.CustomerJourneys != null && request.CustomerJourneys.Count > 0)
            {
                attribution = engine.CalculateMarkovBayesianAttribution(request.CustomerJourneys);
            }

            return Results.Ok(new SimulateRoasResponse(
                Success: true,
                TotalSpend: Math.Round(totalSpend, 2),
                TotalPredictedRevenue: Math.Round(totalPredictedRevenue, 2),
                BlendedAverageRoas: Math.Round(blendedRoas, 4),
                ChannelRankings: rankings,
                AttributionResults: attribution));
        })
        .WithName("SimulatePredictiveRoas")
        .WithSummary("Simulates non-linear Hill diminishing returns curve and Markov-Bayesian attribution across channels.");

        // 2. POST /api/marketing/adspend/reallocate-budget
        group.MapPost("/reallocate-budget", (
            [FromBody] ReallocateBudgetRequest request,
            [FromServices] IDynamicBudgetReallocationService reallocationService) =>
        {
            if (request?.Channels == null || request.Channels.Count == 0)
            {
                return Results.BadRequest(new ReallocateBudgetResponse(
                    Success: false,
                    Plan: null,
                    Message: "Channels collection cannot be empty."));
            }

            try
            {
                var plan = reallocationService.ReallocateBudgets(
                    channels: request.Channels,
                    customTotalBudget: request.TotalBudget,
                    globalMaxShiftFraction: request.GlobalMaxShiftFraction,
                    randomSeed: request.RandomSeed);

                return Results.Ok(new ReallocateBudgetResponse(
                    Success: true,
                    Plan: plan));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ReallocateBudgetResponse(
                    Success: false,
                    Plan: null,
                    Message: ex.Message));
            }
        })
        .WithName("ReallocateAdSpendBudget")
        .WithSummary("Executes Thompson Sampling continuous bandit reallocating spend while strictly preserving budget conservation.");

        // 3. GET /api/marketing/adspend/creative-fatigue-radar/{campaignId}
        group.MapGet("/creative-fatigue-radar/{campaignId}", (
            string campaignId,
            [FromQuery] string? adSetId,
            [FromQuery] double? customFrequency,
            [FromServices] ICreativeFatigueRadarService radarService) =>
        {
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                return Results.BadRequest(new CreativeFatigueRadarResponse(
                    Success: false,
                    Evaluation: null,
                    Message: "Campaign ID is required."));
            }

            // Generate synthetic baseline history for live diagnostic preview when invoked over HTTP
            var now = DateTime.UtcNow.Date;
            List<DailyAdPerformance> sampleHistory = [];
            for (int i = 14; i >= 0; i--)
            {
                var date = now.AddDays(-i);
                int impressions = 50_000 + (i * 1_000);
                // Simulated decay: CTR drops from 2.5% to 1.1% over two weeks
                double decayFactor = Math.Max(0.4, 1.0 - (0.04 * (14 - i)));
                int clicks = (int)(impressions * 0.025 * decayFactor);
                decimal spend = 500m;
                int conversions = Math.Max(5, (int)(clicks * 0.05));
                sampleHistory.Add(new DailyAdPerformance(date, impressions, clicks, spend, conversions, spend * 3.2m, UniqueReach: 15_000));
            }

            var eval = radarService.EvaluateCreativeFatigue(
                campaignId: campaignId,
                history: sampleHistory,
                adSetId: adSetId,
                customFrequency: customFrequency ?? 4.2);

            return Results.Ok(new CreativeFatigueRadarResponse(
                Success: true,
                Evaluation: eval));
        })
        .WithName("GetCreativeFatigueRadar")
        .WithSummary("Evaluates rolling CTR decay slopes, frequency saturation, and CPA inflection triggers for a campaign.");

        // 4. GET /api/marketing/adspend/channels
        group.MapGet("/channels", () =>
        {
            List<AdChannelProfile> profiles =
            [
                new(
                    ChannelId: "meta-advantage-plus",
                    ChannelName: "Meta Advantage+ Shopping Campaigns (ASC)",
                    Platform: "Meta",
                    DefaultHillParams: HillParameters.DefaultMeta,
                    BaselineConversionRate: 0.038,
                    SaturationHalfSpendK: HillParameters.DefaultMeta.K,
                    Notes: "High top-of-funnel reach with broad dynamic creative optimization; Hill exponent 1.8."),
                new(
                    ChannelId: "google-performance-max",
                    ChannelName: "Google Performance Max (PMax)",
                    Platform: "Google",
                    DefaultHillParams: HillParameters.DefaultGoogle,
                    BaselineConversionRate: 0.045,
                    SaturationHalfSpendK: HillParameters.DefaultGoogle.K,
                    Notes: "Intent-driven multi-surface search and shopping; Hill exponent 1.5."),
                new(
                    ChannelId: "tiktok-spark-ads",
                    ChannelName: "TikTok Spark Ads & UGC Creators",
                    Platform: "TikTok",
                    DefaultHillParams: HillParameters.DefaultTikTok,
                    BaselineConversionRate: 0.028,
                    SaturationHalfSpendK: HillParameters.DefaultTikTok.K,
                    Notes: "High viral coefficient with faster creative decay; steep S-curve Hill exponent 2.0."),
                new(
                    ChannelId: "snapchat-catalog-sales",
                    ChannelName: "Snapchat Dynamic Catalog Ads",
                    Platform: "Snapchat",
                    DefaultHillParams: HillParameters.DefaultSnapchat,
                    BaselineConversionRate: 0.022,
                    SaturationHalfSpendK: HillParameters.DefaultSnapchat.K,
                    Notes: "Gen-Z demographic targeting with moderate saturation capacity; Hill exponent 1.6.")
            ];

            return Results.Ok(new AdChannelsResponse(
                Success: true,
                Channels: profiles));
        })
        .WithName("GetSupportedAdChannels")
        .WithSummary("Lists registered ad channels, default Hill curve parameters, and baseline conversion performance.");

        return group;
    }
}
