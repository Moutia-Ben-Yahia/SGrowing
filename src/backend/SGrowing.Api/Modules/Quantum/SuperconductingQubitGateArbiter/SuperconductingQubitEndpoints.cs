using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SuperconductingQubitGateArbiter;

public static class SuperconductingQubitEndpoints
{
    public static IServiceCollection AddSuperconductingQubitGateArbiterServices(this IServiceCollection services)
    {
        services.AddSingleton<ISuperconductingQubitGateArbiterEngine, SuperconductingQubitGateArbiterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSuperconductingQubitGateArbiterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/superconducting")
            .WithTags("Quantum - Superconducting Qubit Gate Arbiter");

        group.MapPost("/synthesize-gate", (
            GateSynthesisRequest request,
            ISuperconductingQubitGateArbiterEngine engine) =>
        {
            var result = engine.SynthesizeGate(request);
            return Results.Ok(result);
        })
        .WithName("SynthesizeSuperconductingQubitGate")
        .WithSummary("Synthesizes DRAG pulse envelopes to suppress leakage into non-computational states and arbitrates ZZ cross-talk")
        .Produces<QuantumGateArbiterResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
