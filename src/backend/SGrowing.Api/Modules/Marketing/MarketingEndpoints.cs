using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public static class MarketingEndpoints
{
    public static IEndpointRouteBuilder MapMarketingEndpoints(this IEndpointRouteBuilder app)
    {
        // Social Media Publishing & Deflection
        app.MapGet("/api/social/posts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var posts = await db.SocialPosts.Where(p => p.TenantId == tenantId).OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
            return Results.Ok(posts);
        });

        app.MapPost("/api/social/posts", async ([FromQuery] Guid tenantId, [FromBody] SGrowing.Api.CreateSocialPostDto req, SGrowing.Api.Modules.Marketing.SocialPublishingService publishingService) =>
        {
            var post = await publishingService.CreatePostAsync(tenantId, req.Content, req.Platforms, req.ScheduledAtUtc, req.MediaUrls);
            return Results.Ok(post);
        });

        app.MapPost("/api/social/publish-due", async ([FromQuery] Guid tenantId, SGrowing.Api.Modules.Marketing.SocialPublishingService publishingService) =>
        {
            var count = await publishingService.PublishDuePostsAsync(tenantId);
            return Results.Ok(new { publishedCount = count });
        });

        app.MapPost("/api/social/comments/deflect", async ([FromQuery] Guid tenantId, [FromBody] SGrowing.Api.DeflectCommentDto req, SocialCommentDeflectionService deflectionService) =>
        {
            var res = await deflectionService.HandlePublicCommentAsync(tenantId, req.Platform, req.PostExternalId, req.CommenterUsername, req.CommentText);
            return Results.Ok(res);
        });

        // Post-Purchase Retention & Replenishment
        app.MapPost("/api/marketing/retention/schedule", async ([FromBody] ScheduleRetentionDto req, IPostPurchaseRetentionService retentionService) =>
        {
            var schedule = await retentionService.SchedulePostPurchaseRetentionAsync(req.TenantId, req.OrderId);
            return Results.Ok(schedule);
        });

        app.MapPost("/api/marketing/retention/survey-response", async ([FromBody] SubmitRetentionSurveyDto req, IPostPurchaseRetentionService retentionService) =>
        {
            var survey = await retentionService.RecordSurveyResponseAsync(req.TenantId, req.SurveyId, req.Rating, req.Notes);
            return Results.Ok(survey);
        });

        app.MapPost("/api/marketing/retention/process-replenishments", async ([FromBody] ProcessReplenishmentsDto req, IPostPurchaseRetentionService retentionService) =>
        {
            var results = await retentionService.ProcessDueReplenishmentRemindersAsync(req.TenantId, req.CurrentTimeUtc);
            return Results.Ok(results);
        });

        app.MapGet("/api/marketing/retention/win-back", async ([FromQuery] Guid tenantId, [FromQuery] int inactiveDays, IPostPurchaseRetentionService retentionService) =>
        {
            var results = await retentionService.ProcessWinBackCampaignAsync(tenantId, inactiveDays);
            return Results.Ok(results);
        });

        // Creator Affiliate Escrow
        app.MapGet("/api/marketing/creators", async ([FromQuery] Guid tenantId, ICreatorAffiliateService affiliateService) =>
        {
            var creators = await affiliateService.ListCreatorsAsync(tenantId);
            return Results.Ok(creators);
        });

        app.MapPost("/api/marketing/creators", async ([FromBody] CreatorProfile profile, ICreatorAffiliateService affiliateService) =>
        {
            var created = await affiliateService.RegisterCreatorAsync(profile.TenantId, profile);
            return Results.Ok(created);
        });

        app.MapGet("/api/marketing/creators/{creatorId}/summary", async (Guid creatorId, [FromQuery] Guid tenantId, ICreatorAffiliateService affiliateService) =>
        {
            var summary = await affiliateService.GetCreatorSummaryAsync(tenantId, creatorId);
            return Results.Ok(summary);
        });

        app.MapPost("/api/marketing/creators/payouts/batch", async ([FromBody] TenantActionDto req, ICreatorAffiliateService affiliateService) =>
        {
            var payouts = await affiliateService.GeneratePayoutBatchAsync(req.TenantId);
            return Results.Ok(payouts);
        });

        // Closed-Loop Delivered ROAS & Ad Optimizer
        app.MapGet("/api/marketing/ad-optimizer/metrics", async ([FromQuery] Guid tenantId, [FromQuery] AdPlatformType? platform, IAdSpendOptimizerService optimizerService) =>
        {
            var metrics = await optimizerService.ListRoasMetricsAsync(tenantId, platform);
            return Results.Ok(metrics);
        });

        app.MapPost("/api/marketing/ad-optimizer/ingest", async ([FromBody] CampaignAdSpendRecord record, IAdSpendOptimizerService optimizerService) =>
        {
            var result = await optimizerService.IngestAdSpendAsync(record.TenantId, record);
            return Results.Ok(result);
        });

        app.MapPost("/api/marketing/ad-optimizer/calculate-roas", async ([FromBody] CalculateRoasDto req, IAdSpendOptimizerService optimizerService) =>
        {
            var metric = await optimizerService.CalculateDeliveredRoasAsync(req.TenantId, req.Platform, req.AdSetId, req.AdSetName, req.Date, req.AdSpend, req.AttributedOrderIds);
            return Results.Ok(metric);
        });

        app.MapPost("/api/marketing/ad-optimizer/rules", async ([FromBody] AdOptimizationRule rule, IAdSpendOptimizerService optimizerService) =>
        {
            var result = await optimizerService.ConfigureRuleAsync(rule.TenantId, rule);
            return Results.Ok(result);
        });

        app.MapPost("/api/marketing/ad-optimizer/evaluate/{metricId}", async (Guid metricId, [FromQuery] Guid tenantId, [FromQuery] decimal dailyBudget, IAdSpendOptimizerService optimizerService) =>
        {
            var log = await optimizerService.EvaluateAndApplyRulesAsync(tenantId, metricId, dailyBudget);
            return Results.Ok(log);
        });

        app.MapGet("/api/marketing/ad-optimizer/logs", async ([FromQuery] Guid tenantId, IAdSpendOptimizerService optimizerService) =>
        {
            var logs = await optimizerService.ListExecutionLogsAsync(tenantId);
            return Results.Ok(logs);
        });

        // Autonomous UGC Creative Script Engine
        app.MapPost("/api/marketing/ugc/generate-scripts", async ([FromBody] GenerateUgcScriptsDto req, IUgcCreativeGeneratorService ugcService) =>
        {
            var scripts = await ugcService.GenerateScriptsForProductAsync(req.TenantId, req.Sku, req.ProductName, req.Language ?? "Darija", req.TargetAudience ?? "Shoppers 18-35");
            return Results.Ok(scripts);
        });

        app.MapGet("/api/marketing/ugc/scripts", async ([FromQuery] Guid tenantId, [FromQuery] string sku, IUgcCreativeGeneratorService ugcService) =>
        {
            var scripts = await ugcService.GetScriptsByProductAsync(tenantId, sku);
            return Results.Ok(scripts);
        });

        // Competitor Ad & Price Intelligence Radar
        app.MapPost("/api/marketing/competitor/ad", async ([FromBody] IngestCompetitorAdDto req, ICompetitorIntelligenceService compService) =>
        {
            var ad = await compService.IngestCompetitorAdAsync(req.TenantId, req.CompetitorName, req.Platform, req.HookSnippet, req.OfferDetails, req.EstimatedDailySpend, req.ThreatLevel ?? "Medium");
            return Results.Ok(ad);
        });

        app.MapPost("/api/marketing/competitor/price", async ([FromBody] TrackCompetitorPriceDto req, ICompetitorIntelligenceService compService) =>
        {
            var tracker = await compService.TrackPriceDisparityAsync(req.TenantId, req.Sku, req.CompetitorName, req.Url, req.CompetitorPrice, req.OurPrice);
            return Results.Ok(tracker);
        });

        app.MapGet("/api/marketing/competitor/radar", async ([FromQuery] Guid tenantId, ICompetitorIntelligenceService compService) =>
        {
            var summary = await compService.GetRadarSummaryAsync(tenantId);
            return Results.Ok(summary);
        });

        return app;
    }
}
