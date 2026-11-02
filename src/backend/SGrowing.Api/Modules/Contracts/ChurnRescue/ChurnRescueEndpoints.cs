using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Contracts.ChurnRescue;

public static class ChurnRescueEndpoints
{
    public static IServiceCollection AddChurnRescueServices(this IServiceCollection services)
    {
        services.AddSingleton<ChurnRescueEngine>();
        return services;
    }

    public static RouteGroupBuilder MapChurnRescueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contracts/churn-rescue")
            .WithTags("ContractsChurnRescue");

        group.MapGet("/radar", async Task<Ok<IReadOnlyList<ChurnRiskAssessment>>> (
            [FromServices] ChurnRescueEngine engine,
            CancellationToken ct) =>
        {
            var radar = await engine.GetRiskRadarAsync(ct);
            return TypedResults.Ok(radar);
        })
        .WithName("GetChurnRiskRadar")
        .WithSummary("Retrieve at-risk customer accounts with churn propensity scores");

        group.MapGet("/accounts/{id}/assess", async Task<Results<Ok<ChurnRiskAssessment>, NotFound<string>>> (
            [FromRoute] string id,
            [FromServices] ChurnRescueEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var assessment = await engine.AssessAccountRiskAsync(id, ct);
                return TypedResults.Ok(assessment);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Account '{id}' not found.");
            }
        })
        .WithName("AssessAccountChurnRisk")
        .WithSummary("Calculate churn risk drivers and recommended retention playbook");

        group.MapPost("/playbooks/execute", async Task<Results<Ok<PlaybookExecutionResult>, NotFound<string>, BadRequest<string>>> (
            [FromBody] TriggerPlaybookRequest request,
            [FromServices] ChurnRescueEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.ExecutePlaybookAsync(request, ct);
                return TypedResults.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("ExecuteRetentionPlaybook")
        .WithSummary("Trigger automated rescue playbook, price lock, or executive outreach");

        group.MapPost("/agreements/generate", async Task<Results<Ok<RenewalAgreement>, NotFound<string>, BadRequest<string>>> (
            [FromBody] GenerateRenewalAgreementRequest request,
            [FromServices] ChurnRescueEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var agreement = await engine.GenerateRenewalAgreementAsync(request, ct);
                return TypedResults.Ok(agreement);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("GenerateRenewalAgreement")
        .WithSummary("Generate automated contract renewal agreement with discount escrow");

        group.MapGet("/metrics", async Task<Ok<RescueMetricsSummary>> (
            [FromServices] ChurnRescueEngine engine,
            CancellationToken ct) =>
        {
            var metrics = await engine.GetRescueMetricsAsync(ct);
            return TypedResults.Ok(metrics);
        })
        .WithName("GetRescueMetrics")
        .WithSummary("Retrieve ARR at risk, rescued ARR, and NRR telemetry");

        return group;
    }
}
