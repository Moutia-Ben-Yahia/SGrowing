using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.RestakingCryptoeconomicSlashing;

public static class RestakingSlashingEndpoints
{
    public static IServiceCollection AddRestakingSlashingServices(this IServiceCollection services)
    {
        services.AddScoped<IRestakingCryptoeconomicSlashingEngine, RestakingCryptoeconomicSlashingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRestakingSlashingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/restaking-slashing")
            .WithTags("Finance - Re-Staking Cryptoeconomic Security Slashing");

        group.MapPost("/analyze-slashing", (
            RestakingPoolTelemetry telemetry,
            IRestakingCryptoeconomicSlashingEngine engine) =>
        {
            var result = engine.AnalyzeRestakingSecurity(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeRestakingSlashing")
        .WithSummary("Analyzes shared restaking economic security, correlated quadratic slashing, and net yield propagation.");

        return app;
    }
}
