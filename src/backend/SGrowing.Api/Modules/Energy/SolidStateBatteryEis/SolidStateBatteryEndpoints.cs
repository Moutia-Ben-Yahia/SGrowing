using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SolidStateBatteryEis;

public static class SolidStateBatteryEndpoints
{
    public static IServiceCollection AddSolidStateBatteryEisServices(this IServiceCollection services)
    {
        services.AddSingleton<ISolidStateBatteryEisEngine, SolidStateBatteryEisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSolidStateBatteryEisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/solid-state-battery")
            .WithTags("Energy - Solid-State Battery EIS Dendrite Arbiter");

        group.MapPost("/evaluate-eis", (
            EisEvaluationRequest request,
            ISolidStateBatteryEisEngine engine) =>
        {
            var assessment = engine.EvaluateEisSpectrum(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateSolidStateBatteryEis")
        .WithSummary("Analyzes AC impedance Nyquist spectra to detect lithium dendrite nucleation and prescribe pulsed self-healing charging")
        .Produces<BatteryDendriteAssessment>(StatusCodes.Status200OK);

        return endpoints;
    }
}
