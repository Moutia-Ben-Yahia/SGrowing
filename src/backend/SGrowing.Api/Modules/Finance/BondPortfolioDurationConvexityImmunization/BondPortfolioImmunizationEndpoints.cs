using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.BondPortfolioDurationConvexityImmunization;

public static class BondPortfolioImmunizationEndpoints
{
    public static IServiceCollection AddBondPortfolioImmunizationServices(this IServiceCollection services)
    {
        services.AddScoped<IBondPortfolioImmunizationEngine, BondPortfolioImmunizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBondPortfolioImmunizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/bond-portfolio-immunization")
            .WithTags("Finance Bond Portfolio Duration Convexity Immunization");

        group.MapPost("/analyze", (BondPortfolioImmunizationTelemetry telemetry, IBondPortfolioImmunizationEngine engine) =>
        {
            var result = engine.AnalyzeImmunization(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeBondPortfolioImmunization")
        .WithSummary("Analyzes fixed income bond portfolio Redington immunization, Macaulay duration matching, convexity dominance, and liability cash-flow hedges.");

        return endpoints;
    }
}
