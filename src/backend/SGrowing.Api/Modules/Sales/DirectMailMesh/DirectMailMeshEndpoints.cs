using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.DirectMailMesh;

public static class DirectMailMeshEndpoints
{
    public static IServiceCollection AddDirectMailMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<DirectMailMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDirectMailMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/direct-mail-mesh")
            .WithTags("Sales - ABM Direct Mail & Executive Gifting Mesh");

        group.MapGet("/overview", (DirectMailMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetDirectMailMeshOverview")
        .WithSummary("Retrieve active gifting campaigns, delivery metrics, and pipeline influence rate");

        group.MapPost("/dispatch-campaign", (DispatchCampaignRequest request, DirectMailMeshEngine engine) =>
        {
            var receipt = engine.DispatchCampaign(request);
            return Results.Ok(receipt);
        })
        .WithName("DispatchDirectMailCampaign")
        .WithSummary("Trigger automated physical direct mail package dispatch with courier tracking");

        group.MapPost("/verify-compliance", (VerifyGiftComplianceRequest request, DirectMailMeshEngine engine) =>
        {
            var verdict = engine.VerifyGiftCompliance(request);
            return Results.Ok(verdict);
        })
        .WithName("VerifyGiftCompliance")
        .WithSummary("Validate recipient corporate gifting limits and anti-bribery threshold rules");

        return endpoints;
    }
}
