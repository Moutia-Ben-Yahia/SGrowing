using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.StrontiumOpticalLatticeClock;

public static class StrontiumOpticalClockEndpoints
{
    public static IServiceCollection AddStrontiumOpticalLatticeClockServices(this IServiceCollection services)
    {
        services.AddScoped<IStrontiumOpticalLatticeClockEngine, StrontiumOpticalLatticeClockEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapStrontiumOpticalClockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quantum/strontium-optical-clock")
            .WithTags("Quantum - Strontium-87 Optical Lattice Clock Frequency Comb");

        group.MapPost("/analyze-clock", (
            StrontiumClockTelemetry telemetry,
            IStrontiumOpticalLatticeClockEngine engine) =>
        {
            var result = engine.AnalyzeOpticalAtomicClock(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeStrontiumOpticalClock")
        .WithSummary("Analyzes Strontium-87 optical lattice atomic clock, frequency comb f_ceo lock, Allan deviation instability, and geodetic redshift.");

        return app;
    }
}
