using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Growth.Influencers;

public static class InfluencerEscrowEndpoints
{
    public static IServiceCollection AddInfluencerEscrowServices(this IServiceCollection services)
    {
        services.AddSingleton<InfluencerEscrowEngine>();
        return services;
    }

    public static RouteGroupBuilder MapInfluencerEscrowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/growth/influencers")
            .WithTags("GrowthInfluencersEscrow");

        group.MapGet("/creators", async Task<Ok<IReadOnlyList<CreatorProfile>>> (
            [FromServices] InfluencerEscrowEngine engine,
            CancellationToken ct) =>
        {
            var creators = await engine.GetCreatorsAsync(ct);
            return TypedResults.Ok(creators);
        })
        .WithName("GetInfluencerCreators")
        .WithSummary("Retrieve verified creator profiles and engagement authenticity metrics");

        group.MapGet("/contracts", async Task<Ok<IReadOnlyList<EscrowContract>>> (
            [FromServices] InfluencerEscrowEngine engine,
            CancellationToken ct) =>
        {
            var contracts = await engine.GetContractsAsync(ct);
            return TypedResults.Ok(contracts);
        })
        .WithName("GetInfluencerEscrowContracts")
        .WithSummary("List active and completed creator escrow agreements");

        group.MapPost("/contracts", async Task<Results<Ok<EscrowContract>, BadRequest<string>, NotFound<string>>> (
            [FromBody] CreateEscrowContractRequest request,
            [FromServices] InfluencerEscrowEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var contract = await engine.CreateContractAsync(request, ct);
                return TypedResults.Ok(contract);
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
        .WithName("CreateInfluencerEscrowContract")
        .WithSummary("Initialize milestone-gated escrow contract for creator campaign");

        group.MapPost("/milestones/release", async Task<Results<Ok<EscrowContract>, BadRequest<string>, NotFound<string>>> (
            [FromBody] ReleaseMilestoneRequest request,
            [FromServices] InfluencerEscrowEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await engine.VerifyAndReleaseMilestoneAsync(request, ct);
                return TypedResults.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("ReleaseInfluencerEscrowMilestone")
        .WithSummary("Verify deliverable views and release milestone payout to creator");

        group.MapGet("/summary", async Task<Ok<CreatorCampaignSummary>> (
            [FromServices] InfluencerEscrowEngine engine,
            CancellationToken ct) =>
        {
            var summary = await engine.GetCampaignSummaryAsync(ct);
            return TypedResults.Ok(summary);
        })
        .WithName("GetInfluencerCampaignSummary")
        .WithSummary("Retrieve aggregate escrow funds, attributed GMV, and ROAS");

        return group;
    }
}
