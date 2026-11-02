using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.MicrostructureKylesLambdaAdverseSelection;

public static class MicrostructureKylesLambdaEndpoints
{
    public static IServiceCollection AddMicrostructureKylesLambdaServices(this IServiceCollection services)
    {
        services.AddScoped<IMicrostructureKylesLambdaEngine, MicrostructureKylesLambdaEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicrostructureKylesLambdaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/microstructure-kyles-lambda")
            .WithTags("Finance Microstructure Kyle's Lambda Adverse Selection");

        group.MapPost("/analyze", (MicrostructureKylesLambdaTelemetry telemetry, IMicrostructureKylesLambdaEngine engine) =>
        {
            var result = engine.AnalyzeMicrostructure(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMicrostructureKylesLambda")
        .WithSummary("Analyzes high-frequency market microstructure Kyle's Lambda price impact, Hasbrouck permanent information fraction, and VPIN toxicity cascades.");

        return endpoints;
    }
}
