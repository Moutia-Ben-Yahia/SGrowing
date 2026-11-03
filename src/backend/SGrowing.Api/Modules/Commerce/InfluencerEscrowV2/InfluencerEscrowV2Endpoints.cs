using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.InfluencerEscrowV2;

public static class InfluencerEscrowV2Endpoints
{
    public static IServiceCollection AddInfluencerEscrowV2Services(this IServiceCollection services)
    {
        services.AddSingleton<IInfluencerEscrowV2Engine, InfluencerEscrowV2Engine>();
        return services;
    }

    public static IEndpointRouteBuilder MapInfluencerEscrowV2Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/commerce/influencer-escrow")
            .WithTags("Commerce - Influencer Affiliate Escrow V2");

        group.MapGet("/campaigns", (IInfluencerEscrowV2Engine engine) =>
        {
            var list = engine.GetCampaigns();
            return Results.Ok(list);
        })
        .WithName("GetInfluencerCampaigns")
        .WithSummary("Retrieves active creator campaigns, locked escrow, and attributed GMV");

        group.MapPost("/release-payout", (ReleaseMilestonePayoutRequest request, IInfluencerEscrowV2Engine engine) =>
        {
            try
            {
                var updated = engine.ReleasePayout(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("ReleaseInfluencerPayout")
        .WithSummary("Releases milestone payment from campaign escrow to creator wallet");

        group.MapPost("/attribute-conversion", (AttributeConversionRequest request, IInfluencerEscrowV2Engine engine) =>
        {
            try
            {
                var updated = engine.AttributeConversion(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("AttributeInfluencerConversion")
        .WithSummary("Attributes verified order GMV to influencer tag with fraud score filtering");

        return endpoints;
    }
}
