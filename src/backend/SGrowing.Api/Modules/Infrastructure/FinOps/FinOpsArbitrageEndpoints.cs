using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.FinOps;

public static class FinOpsArbitrageEndpoints
{
    public static IServiceCollection AddFinOpsArbitrageServices(this IServiceCollection services)
    {
        services.AddSingleton<FinOpsCostAttributionEngine>();
        services.AddSingleton<SpotInstanceEvictionGraceHandler>();
        services.AddSingleton<AutonomousRightSizingRecommender>();
        return services;
    }

    public static IEndpointRouteBuilder MapFinOpsArbitrageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/finops")
            .WithTags("Autonomous Multi-Tenant FinOps & Cloud Resource Cost Arbitrage");

        group.MapPost("/statements", (
            GenerateCostStatementRequest request,
            FinOpsCostAttributionEngine engine) =>
        {
            var statement = engine.GenerateStatement(request);
            return Results.Ok(statement);
        })
        .WithName("GenerateTenantCostStatement")
        .WithSummary("Generates itemized compute, memory, storage, and egress billing statement with gross margin.");

        group.MapPost("/spot/preemption", (
            string nodeId,
            CloudProvider provider,
            string region,
            IReadOnlyList<string> workloads,
            SpotInstanceEvictionGraceHandler handler) =>
        {
            var alert = handler.HandlePreemptionNotice(nodeId, provider, region, workloads);
            return Results.Created($"/api/v1/infrastructure/finops/spot/alerts/{alert.AlertId}", alert);
        })
        .WithName("HandleSpotPreemptionNotice")
        .WithSummary("Registers cloud spot instance 120-second termination notice and begins workload draining.");

        group.MapPost("/spot/evacuate", (
            string alertId,
            SpotInstanceEvictionGraceHandler handler) =>
        {
            var result = handler.ConfirmWorkloadEvacuation(alertId);
            return Results.Ok(result);
        })
        .WithName("ConfirmSpotEvacuation")
        .WithSummary("Confirms completion of zero-downtime container workload migration.");

        group.MapGet("/spot/metrics", (
            SpotInstanceEvictionGraceHandler handler) =>
        {
            var (ratio, total, success) = handler.GetMigrationSuccessMetrics();
            return Results.Ok(new { SuccessRatio = ratio, TotalPreemptions = total, SuccessfulEvacuations = success });
        })
        .WithName("GetSpotMigrationMetrics")
        .WithSummary("Retrieves historical zero-downtime spot migration success rates.");

        group.MapPost("/rightsizing", (
            WorkloadUsageMetrics metrics,
            AutonomousRightSizingRecommender recommender) =>
        {
            var recommendation = recommender.AnalyzeWorkload(metrics);
            return Results.Ok(recommendation);
        })
        .WithName("AnalyzeWorkloadRightSizing")
        .WithSummary("Evaluates historical P99 utilization and generates rightsizing recommendation with cost savings.");

        return app;
    }
}
