using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.HypersonicScramjetIsolatorShockTrain;

public static class HypersonicScramjetEndpoints
{
    public static IServiceCollection AddHypersonicScramjetIsolatorServices(this IServiceCollection services)
    {
        services.AddScoped<IHypersonicScramjetIsolatorShockTrainEngine, HypersonicScramjetIsolatorShockTrainEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHypersonicScramjetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/hypersonic-scramjet-isolator-shock-train")
            .WithTags("Aerospace Hypersonic Scramjet Isolator Shock-Train");

        group.MapPost("/analyze", (HypersonicScramjetTelemetry telemetry, IHypersonicScramjetIsolatorShockTrainEngine engine) =>
        {
            var result = engine.AnalyzeIsolatorShockTrain(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHypersonicScramjetIsolator")
        .WithSummary("Analyzes scramjet isolator shock-train boundary layer interaction, Waltrup-Billig shock length, and catastrophic inlet unstart margins.");

        return endpoints;
    }
}
