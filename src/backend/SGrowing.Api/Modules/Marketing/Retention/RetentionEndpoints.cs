using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marketing.Retention;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 30:
/// Autonomous Omnichannel Customer Retention, Predictive Churn Radar &amp; Win-Back Bandits.
/// </summary>
public static class RetentionEndpoints
{
    /// <summary>
    /// Registers all Phase 30 Predictive Retention, Churn Radar, and Thompson Sampling Bandit services.
    /// </summary>
    public static IServiceCollection AddPredictiveRetentionServices(this IServiceCollection services)
    {
        services.AddSingleton<IPredictiveChurnRadarService, PredictiveChurnRadarService>();
        services.AddSingleton<IRetentionPolicyBanditService, RetentionPolicyBanditService>();
        services.AddSingleton<IOmnichannelRetentionOrchestrator, OmnichannelRetentionOrchestrator>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 30 Retention Minimal API route group under /api/v1/marketing/retention.
    /// </summary>
    public static IEndpointRouteBuilder MapPredictiveRetentionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/marketing/retention")
            .WithTags("Autonomous Omnichannel Retention & Churn Radar");

        // 1. POST /api/v1/marketing/retention/radar/assess
        group.MapPost("/radar/assess", (
            [FromBody] AssessCustomerRiskRequest request,
            [FromServices] IPredictiveChurnRadarService radarService) =>
        {
            if (request?.Signals is null)
            {
                return Results.BadRequest(new AssessCustomerRiskResponse(false, null, "Customer signals are required."));
            }

            var assessment = radarService.AssessCustomerRisk(request.Signals);
            return Results.Ok(new AssessCustomerRiskResponse(true, assessment, "Customer churn risk evaluated successfully."));
        })
        .WithName("AssessCustomerChurnRisk")
        .WithSummary("Runs Cox Proportional Hazards survival analysis on customer behavior signals.");

        // 2. POST /api/v1/marketing/retention/radar/batch
        group.MapPost("/radar/batch", async (
            [FromBody] BatchRadarScanRequest request,
            [FromServices] IPredictiveChurnRadarService radarService,
            CancellationToken cancellationToken) =>
        {
            if (request?.Customers is null)
            {
                return Results.BadRequest(new BatchRadarScanResponse(false, 0, 0, [], "Customers cohort list is required."));
            }

            var response = await radarService.ScanBatchAsync(request, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("BatchRadarScan")
        .WithSummary("Executes a high-throughput batch churn radar scan and identifies at-risk cohorts.");

        // 3. POST /api/v1/marketing/retention/bandit/select
        group.MapPost("/bandit/select", (
            [FromBody] SelectIncentiveRequest request,
            [FromServices] IRetentionPolicyBanditService banditService) =>
        {
            if (request?.Signals is null)
            {
                return Results.BadRequest(new SelectIncentiveResponse(false, null, "Customer signals are required."));
            }

            var recommendation = banditService.RecommendIncentive(request.Signals, request.Assessment);
            return Results.Ok(new SelectIncentiveResponse(true, recommendation, "Optimal win-back incentive selected via Thompson Sampling."));
        })
        .WithName("SelectBanditIncentive")
        .WithSummary("Selects optimal retention incentive and delivery channel via Thompson Sampling.");

        // 4. POST /api/v1/marketing/retention/intervene
        group.MapPost("/intervene", async (
            [FromBody] DispatchInterventionRequest request,
            [FromServices] IOmnichannelRetentionOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            if (request?.Signals is null)
            {
                return Results.BadRequest(new DispatchInterventionResponse(false, null, "Customer signals are required."));
            }

            var response = await orchestrator.DispatchInterventionAsync(request, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("DispatchRetentionIntervention")
        .WithSummary("Dispatches cascading omnichannel retention intervention with automated 10% holdout control.");

        // 5. POST /api/v1/marketing/retention/bandit/feedback
        group.MapPost("/bandit/feedback", async (
            [FromBody] RecordBanditFeedbackRequest request,
            [FromServices] IRetentionPolicyBanditService banditService,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new RecordBanditFeedbackResponse(false, null!, "Feedback payload is required."));
            }

            var response = await banditService.RecordFeedbackAsync(request, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("RecordBanditFeedback")
        .WithSummary("Records conversion or defection outcome to update Thompson Sampling Beta posteriors.");

        // 6. GET /api/v1/marketing/retention/bandit/stats
        group.MapGet("/bandit/stats", (
            [FromServices] IRetentionPolicyBanditService banditService) =>
        {
            var stats = banditService.GetBanditStats();
            return Results.Ok(stats);
        })
        .WithName("GetBanditStats")
        .WithSummary("Retrieves all multi-armed bandit arm performance statistics and conversion rates.");

        // 7. GET /api/v1/marketing/retention/health
        group.MapGet("/health", () =>
        {
            var health = new RetentionEngineHealth(
                Status: "Operational",
                Version: "1.0.0-phase30",
                TimestampUtc: DateTime.UtcNow,
                Capabilities: new Dictionary<string, string>
                {
                    ["SurvivalAnalysis"] = "Cox Proportional Hazards h(t|X) = h0 * exp(beta1*Cadence + beta2*(1-Sentiment) + beta3*(1-OpenRate))",
                    ["BanditPolicy"] = "Contextual Multi-Armed Bandit with Thompson Sampling over Beta(alpha, beta) posteriors",
                    ["OmnichannelDispatch"] = "Cascading WhatsApp -> Automated Voice Concierge -> Targeted Email",
                    ["CausalExperimentation"] = "10% Randomized Holdout Control Group for pure incremental causal lift tracking",
                    ["Localization"] = "Optimized for Algerian DZD commercial incentives, express shipping vouchers, and WhatsApp-first flows"
                });

            return Results.Ok(health);
        })
        .WithName("GetRetentionEngineHealth")
        .WithSummary("Returns real-time operational status and model capabilities of the retention engine.");

        // 8. GET /api/v1/marketing/retention/metrics
        group.MapGet("/metrics", (
            [FromServices] IOmnichannelRetentionOrchestrator orchestrator) =>
        {
            var metrics = orchestrator.CalculateCausalLiftMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetRetentionExperimentMetrics")
        .WithSummary("Calculates causal incremental conversion lift and net revenue saved vs holdout control.");

        return app;
    }
}
