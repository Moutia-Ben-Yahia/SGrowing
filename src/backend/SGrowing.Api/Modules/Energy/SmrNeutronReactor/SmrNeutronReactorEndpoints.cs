using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SmrNeutronReactor;

public static class SmrNeutronReactorEndpoints
{
    public static IServiceCollection AddSmrNeutronReactorServices(this IServiceCollection services)
    {
        services.AddSingleton<ISmrNeutronReactorEngine, SmrNeutronReactorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSmrNeutronReactorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/smr")
            .WithTags("Energy - SMR Neutron Reactor Regulator");

        group.MapPost("/evaluate-transient", (
            XenonTransientEvaluationRequest request,
            ISmrNeutronReactorEngine engine) =>
        {
            var result = engine.EvaluateTransient(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateSmrXenonTransient")
        .WithSummary("Evaluates core xenon-135 transient trajectory and reactivity adjustments for small modular reactors")
        .Produces<ReactivityAdjustmentDecision>(StatusCodes.Status200OK);

        return endpoints;
    }
}
