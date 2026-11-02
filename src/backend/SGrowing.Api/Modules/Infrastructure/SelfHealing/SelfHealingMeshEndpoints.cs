using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.SelfHealing;

public static class SelfHealingMeshEndpoints
{
    public static IServiceCollection AddSelfHealingMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<SelfHealingMeshEngine>();
        return services;
    }

    public static RouteGroupBuilder MapSelfHealingMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/self-healing")
            .WithTags("InfrastructureSelfHealingMesh");

        group.MapGet("/probes", async Task<Ok<IReadOnlyList<HealthProbeTelemetry>>> (
            [FromServices] SelfHealingMeshEngine engine,
            CancellationToken ct) =>
        {
            var probes = await engine.GetHealthProbesAsync(ct);
            return TypedResults.Ok(probes);
        })
        .WithName("GetInfrastructureHealthProbes")
        .WithSummary("Retrieve distributed latency and saturation health probe telemetry");

        group.MapPost("/remediate", async Task<Results<Ok<RemediationActionLog>, BadRequest<string>>> (
            [FromBody] TriggerRemediationRequest request,
            [FromServices] SelfHealingMeshEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.TriggerRemediationAsync(request.ResourceName, ct);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("TriggerInfrastructureRemediation")
        .WithSummary("Trigger automated circuit-breaker reset, connection recycling, or node failover");

        group.MapPost("/chaos/execute", async Task<Results<Ok<ChaosExperimentResult>, BadRequest<string>>> (
            [FromBody] ChaosExperimentRequest request,
            [FromServices] SelfHealingMeshEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.ExecuteChaosExperimentAsync(request, ct);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("ExecuteChaosExperiment")
        .WithSummary("Execute latency injection, replica partition, or connection flood chaos tests");

        group.MapGet("/scorecard", async Task<Ok<MeshResilienceScorecard>> (
            [FromServices] SelfHealingMeshEngine engine,
            CancellationToken ct) =>
        {
            var scorecard = await engine.GetResilienceScorecardAsync(ct);
            return TypedResults.Ok(scorecard);
        })
        .WithName("GetMeshResilienceScorecard")
        .WithSummary("Retrieve platform availability percentage, MTTR, and health grade");

        return group;
    }
}

public record TriggerRemediationRequest(
    string ResourceName
);
