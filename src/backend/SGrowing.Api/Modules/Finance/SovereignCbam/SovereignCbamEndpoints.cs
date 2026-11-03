using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.SovereignCbam;

public static class SovereignCbamEndpoints
{
    public static IServiceCollection AddSovereignCbamServices(this IServiceCollection services)
    {
        services.AddSingleton<SovereignCbamEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSovereignCbamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/sovereign-cbam")
            .WithTags("Finance - Sovereign CBAM & Scope 1/2/3 Supply Chain Emissions");

        group.MapGet("/overview", (SovereignCbamEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetSovereignCbamOverview")
        .WithSummary("Retrieve statutory CBAM declarations, embedded emissions factors, and carbon liabilities");

        group.MapPost("/declare", (GenerateCbamDeclarationRequest request, SovereignCbamEngine engine) =>
        {
            var record = engine.GenerateDeclaration(request);
            return Results.Ok(record);
        })
        .WithName("GenerateCbamDeclaration")
        .WithSummary("Calculates direct and indirect embedded tCO2e and generates cryptographic EU CBAM filing");

        group.MapPost("/offset", (OffsetEmissionsRequest request, SovereignCbamEngine engine) =>
        {
            var record = engine.OffsetEmissions(request);
            return Results.Ok(record);
        })
        .WithName("OffsetCbamEmissions")
        .WithSummary("Surrenders certified carbon certificates to settle statutory import liabilities");

        return endpoints;
    }
}
