using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.CartRescue;

public static class CartRescueEndpoints
{
    public static IServiceCollection AddCartRescueServices(this IServiceCollection services)
    {
        services.AddSingleton<ICartRescueEngine, CartRescueEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCartRescueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/commerce/cart-rescue")
            .WithTags("Commerce - Margin-Preserving Cart Rescue");

        group.MapPost("/evaluate", (EvaluateCartRescueRequest request, ICartRescueEngine engine) =>
        {
            var result = engine.EvaluateCart(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateCartRescue")
        .WithSummary("Calculates recovery elasticity and selects margin-preserving incentive (gift bundle, shipping) over price cuts");

        group.MapGet("/active-rescues", (ICartRescueEngine engine) =>
        {
            var rescues = engine.GetActiveRescues();
            return Results.Ok(rescues);
        })
        .WithName("GetActiveCartRescues")
        .WithSummary("Retrieves staged, dispatched, and recovered cart abandonment pipelines");

        group.MapPost("/dispatch-nudge", (DispatchCartNudgeRequest request, ICartRescueEngine engine) =>
        {
            var result = engine.DispatchNudge(request);
            return Results.Ok(result);
        })
        .WithName("DispatchCartRescueNudge")
        .WithSummary("Dispatches dynamic incentive message via WhatsApp, SMS, or Email");

        return endpoints;
    }
}
