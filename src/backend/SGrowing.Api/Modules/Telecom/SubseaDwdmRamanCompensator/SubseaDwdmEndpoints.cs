using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Telecom.SubseaDwdmRamanCompensator;

public static class SubseaDwdmEndpoints
{
    public static IServiceCollection AddSubseaDwdmRamanCompensatorServices(this IServiceCollection services)
    {
        services.AddSingleton<ISubseaDwdmRamanCompensatorEngine, SubseaDwdmRamanCompensatorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSubseaDwdmRamanCompensatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/telecom/subsea-dwdm")
            .WithTags("Telecom - Subsea Optical DWDM Raman Compensator");

        group.MapPost("/compensate-nonlinearity", (
            RamanCompensationRequest request,
            ISubseaDwdmRamanCompensatorEngine engine) =>
        {
            var plan = engine.EvaluateAndCompensate(request);
            return Results.Ok(plan);
        })
        .WithName("CompensateSubseaDwdmNonlinearity")
        .WithSummary("Models Stimulated Raman Scattering (SRS) spectral tilt, Kerr phase shifts, and coordinates counter-propagating Raman pump powers")
        .Produces<KerrNonlinearCompensationPlan>(StatusCodes.Status200OK);

        return endpoints;
    }
}
