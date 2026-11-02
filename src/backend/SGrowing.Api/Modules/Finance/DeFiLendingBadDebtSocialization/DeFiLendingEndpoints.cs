using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.DeFiLendingBadDebtSocialization;

public static class DeFiLendingEndpoints
{
    public static IServiceCollection AddDeFiLendingServices(this IServiceCollection services)
    {
        services.AddScoped<IDeFiLendingBadDebtSocializationEngine, DeFiLendingBadDebtSocializationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDeFiLendingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/defi-lending-resolution")
            .WithTags("Finance - DeFi Lending Bad Debt Socialization");

        group.MapPost("/analyze-deficit", (
            LendingPoolRiskTelemetry telemetry,
            IDeFiLendingBadDebtSocializationEngine engine) =>
        {
            var result = engine.AnalyzeBadDebtResolution(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDeFiBadDebtResolution")
        .WithSummary("Analyzes DeFi lending bad debt deficits, waterfall insurance absorption, and socialization haircuts.");

        return app;
    }
}
