using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.MajoranaZeroModeBraiding;

public static class MajoranaBraidingEndpoints
{
    public static IServiceCollection AddMajoranaBraidingServices(this IServiceCollection services)
    {
        services.AddScoped<IMajoranaBraidingEngine, MajoranaBraidingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMajoranaBraidingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/majorana-zero-mode-braiding")
            .WithTags("Quantum Majorana Zero Modes (MZM) & Topological Anyon Braiding");

        group.MapPost("/analyze", (MajoranaBraidingTelemetry telemetry, IMajoranaBraidingEngine engine) =>
        {
            var evaluation = engine.AnalyzeBraiding(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeMajoranaBraiding")
        .WithSummary("Analyzes semiconductor-superconductor nanowires, zero-bias peak conductance quantization, topological gap, and non-Abelian braiding");

        return endpoints;
    }
}
