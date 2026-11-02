using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.SovereignDebtClimateResilience;

public static class SovereignDebtEndpoints
{
    public static IServiceCollection AddSovereignDebtClimateResilienceServices(this IServiceCollection services)
    {
        services.AddScoped<ISovereignDebtClimateResilienceEngine, SovereignDebtClimateResilienceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSovereignDebtEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/sovereign-debt-climate")
            .WithTags("Finance - Sovereign Debt Sustainability & Climate Resilience");

        group.MapPost("/analyze-sustainability", (
            SovereignDebtTelemetry telemetry,
            ISovereignDebtClimateResilienceEngine engine) =>
        {
            var result = engine.AnalyzeDebtSustainability(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSovereignDebtSustainability")
        .WithSummary("Analyzes sovereign debt dynamics, snowball effect, climate adaptation risk costs, default probability, and fiscal gap.");

        return app;
    }
}
