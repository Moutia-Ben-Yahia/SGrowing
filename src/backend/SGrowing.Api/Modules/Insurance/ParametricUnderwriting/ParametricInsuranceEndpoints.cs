using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Insurance.ParametricUnderwriting;

public static class ParametricInsuranceEndpoints
{
    public static IServiceCollection AddParametricInsuranceServices(this IServiceCollection services)
    {
        services.AddSingleton<ParametricCatastropheOracleEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapParametricInsuranceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/insurance/parametric-underwriting")
            .WithTags("Autonomous Real-Time Dynamic Insurance Underwriting & Parametric Catastrophe Oracle");

        group.MapPost("/adjudicate-event", (
            AdjudicateEventRequest request,
            ParametricCatastropheOracleEngine engine) =>
        {
            var settlements = engine.AdjudicateEvent(request);
            return Results.Ok(settlements);
        });

        group.MapPost("/audit-solvency", (
            AuditSolvencyRequest request,
            ParametricCatastropheOracleEngine engine) =>
        {
            var result = engine.AuditSolvencyAdequacy(request);
            return Results.Ok(result);
        });

        return app;
    }
}
