using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marketing.Copywriting;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 33:
/// Autonomous Multi-Channel Spintax &amp; Generative Copywriting AI Matrix.
/// </summary>
public static class CopywritingEndpoints
{
    /// <summary>
    /// Registers all Phase 33 Spintax, Brand Voice Guardrails, and Thompson Sampling Bandit services.
    /// </summary>
    public static IServiceCollection AddCopywritingServices(this IServiceCollection services)
    {
        services.AddSingleton<ISpintaxMatrixEngine, SpintaxMatrixEngine>();
        services.AddSingleton<IBrandVoiceGuardrailEngine, BrandVoiceGuardrailEngine>();
        services.AddSingleton<IMultiVariantCopyBanditService, MultiVariantCopyBanditService>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 33 Copywriting Minimal API route group under /api/v1/marketing/copywriting.
    /// </summary>
    public static IEndpointRouteBuilder MapCopywritingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/marketing/copywriting")
            .WithTags("Autonomous Spintax & Copywriting Bandit Matrix");

        // 1. POST /api/v1/marketing/copywriting/spintax/generate
        group.MapPost("/spintax/generate", (
            [FromBody] SpintaxGenerateRequest request,
            [FromServices] ISpintaxMatrixEngine spintaxEngine) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Template))
            {
                return Results.BadRequest(new SpintaxGenerateResponse(false, null, 0, "Template is required."));
            }

            string message = spintaxEngine.GenerateMessage(
                request.Template,
                request.Seed,
                request.Variables,
                request.TemplateKey);

            long permutationSpace = spintaxEngine.CalculatePermutationSpace(request.Template);

            return Results.Ok(new SpintaxGenerateResponse(true, message, permutationSpace));
        })
        .WithName("GenerateSpintaxMessage")
        .WithSummary("Generates an anti-fingerprinting copy variation with sliding window deduplication.");

        // 2. POST /api/v1/marketing/copywriting/spintax/analyze
        group.MapPost("/spintax/analyze", (
            [FromBody] SpintaxAnalyzeRequest request,
            [FromServices] ISpintaxMatrixEngine spintaxEngine) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Template))
            {
                return Results.BadRequest(new SpintaxAnalyzeResponse(false, null, "Template is required."));
            }

            long totalPermutations = spintaxEngine.CalculatePermutationSpace(request.Template);
            double entropy = spintaxEngine.CalculateEntropy(request.Template, request.SampleSize);

            int sampleCount = Math.Min(5, (int)Math.Min(totalPermutations, 5));
            List<string> sampleOutputs = [];
            for (int i = 0; i < sampleCount; i++)
            {
                sampleOutputs.Add(spintaxEngine.GenerateMessage(request.Template, seed: i * 7 + 1));
            }

            var analysis = new SpintaxAnalysisResult(
                Template: request.Template,
                TotalPermutations: totalPermutations,
                ShannonEntropy: entropy,
                SampleSize: request.SampleSize,
                SampleOutputs: sampleOutputs);

            return Results.Ok(new SpintaxAnalyzeResponse(true, analysis));
        })
        .WithName("AnalyzeSpintaxTemplate")
        .WithSummary("Calculates exact combinatorial permutation space and Shannon entropy for a spintax template.");

        // 3. POST /api/v1/marketing/copywriting/guardrails/evaluate
        group.MapPost("/guardrails/evaluate", (
            [FromBody] CopyAuditRequest request,
            [FromServices] IBrandVoiceGuardrailEngine guardrailEngine) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Text))
            {
                return Results.BadRequest(new CopyAuditResponse(false, null, "Text to audit is required."));
            }

            var audit = guardrailEngine.EvaluateCopy(request);
            return Results.Ok(new CopyAuditResponse(true, audit));
        })
        .WithName("EvaluateCopyGuardrails")
        .WithSummary("Audits copywriting text against brand voice dimensions, Maghreb dialect nuances, spam filters, and telecom compliance.");

        // 4. POST /api/v1/marketing/copywriting/bandit/select
        group.MapPost("/bandit/select", (
            [FromBody] BanditSelectRequest request,
            [FromServices] IMultiVariantCopyBanditService banditService) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.CampaignId) || request.VariantIds is null || request.VariantIds.Count == 0)
            {
                return Results.BadRequest(new BanditSelectResponse(false, null, "CampaignId and at least one VariantId are required."));
            }

            var selection = banditService.SelectBestVariant(request.CampaignId, request.VariantIds);
            return Results.Ok(new BanditSelectResponse(true, selection));
        })
        .WithName("SelectBanditVariant")
        .WithSummary("Selects optimal copy variant in real-time via Bayesian Thompson Sampling.");

        // 5. POST /api/v1/marketing/copywriting/bandit/feedback
        group.MapPost("/bandit/feedback", (
            [FromBody] BanditFeedbackRequest request,
            [FromServices] IMultiVariantCopyBanditService banditService) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.CampaignId) || string.IsNullOrWhiteSpace(request.VariantId) || string.IsNullOrWhiteSpace(request.EventType))
            {
                return Results.BadRequest(new BanditFeedbackResponse(false, null, "CampaignId, VariantId, and EventType are required."));
            }

            var interaction = new CopyInteractionEvent(
                EventType: request.EventType,
                TimestampUtc: DateTime.UtcNow,
                Weight: request.Weight ?? 1.0);

            var feedback = banditService.RecordFeedback(request.CampaignId, request.VariantId, interaction);
            return Results.Ok(new BanditFeedbackResponse(true, feedback));
        })
        .WithName("RecordBanditFeedback")
        .WithSummary("Updates Bayesian Beta distribution parameters based on customer interactions (Delivered, Opened, Clicked, Converted, Unsubscribed).");

        // 6. GET /api/v1/marketing/copywriting/bandit/stats/{campaignId}
        group.MapGet("/bandit/stats/{campaignId}", (
            string campaignId,
            [FromServices] IMultiVariantCopyBanditService banditService) =>
        {
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                return Results.BadRequest(new BanditStatsResponse(false, null, "CampaignId is required."));
            }

            var stats = banditService.GetVariantPerformance(campaignId);
            return Results.Ok(new BanditStatsResponse(true, stats));
        })
        .WithName("GetBanditCampaignStats")
        .WithSummary("Returns variant-level conversion rates, expected values, variances, and empirical regret.");

        return app;
    }
}
