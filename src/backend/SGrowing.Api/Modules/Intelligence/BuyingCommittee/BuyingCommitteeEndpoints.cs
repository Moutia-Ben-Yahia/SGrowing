using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.BuyingCommittee;

public static class BuyingCommitteeEndpoints
{
    public static IServiceCollection AddBuyingCommitteeServices(this IServiceCollection services)
    {
        services.AddSingleton<IBuyingCommitteeEngine, BuyingCommitteeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBuyingCommitteeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/buying-committee")
            .WithTags("Intelligence - Buying Committee Graph");

        group.MapPost("/map", (MapCommitteeRequest request, IBuyingCommitteeEngine engine) =>
        {
            var result = engine.MapBuyingCommittee(request);
            return Results.Ok(result);
        })
        .WithName("MapBuyingCommittee")
        .WithSummary("Map enterprise account buying committee and calculate consensus readiness");

        group.MapGet("/{companyDomain}", (string companyDomain, IBuyingCommitteeEngine engine) =>
        {
            var committee = engine.GetCommitteeByDomain(companyDomain);
            return committee is not null ? Results.Ok(committee) : Results.NotFound();
        })
        .WithName("GetBuyingCommitteeByDomain")
        .WithSummary("Retrieve buying committee hierarchy and gaps for an account domain");

        group.MapGet("/playbooks/{role}", (PersonaRole role, IBuyingCommitteeEngine engine) =>
        {
            var playbooks = engine.GeneratePlaybooks(role);
            return Results.Ok(playbooks);
        })
        .WithName("GetRolePlaybooks")
        .WithSummary("Generate role-tailored outreach playbooks and collateral recommendations");

        return endpoints;
    }
}
