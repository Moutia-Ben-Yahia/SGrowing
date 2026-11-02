using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.DynamicMarginCollateralVar;

public static class DynamicMarginVarEndpoints
{
    public static IServiceCollection AddDynamicMarginCollateralVarServices(this IServiceCollection services)
    {
        services.AddScoped<IDynamicMarginCollateralVarEngine, DynamicMarginCollateralVarEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDynamicMarginVarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/dynamic-margin-collateral-var")
            .WithTags("Finance Dynamic Margin Collateral VaR");

        group.MapPost("/analyze", (DynamicMarginVarTelemetry telemetry, IDynamicMarginCollateralVarEngine engine) =>
        {
            var result = engine.AnalyzeMarginRisk(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDynamicMarginCollateralVaR")
        .WithSummary("Analyzes heavy-tailed EVT Value-at-Risk, Expected Shortfall, and concentrated liquidity margin collateral requirements.");

        return endpoints;
    }
}
