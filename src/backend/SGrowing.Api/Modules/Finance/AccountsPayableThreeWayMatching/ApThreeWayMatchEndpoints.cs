using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.AccountsPayableThreeWayMatching;

public static class ApThreeWayMatchEndpoints
{
    public static IServiceCollection AddApThreeWayMatchServices(this IServiceCollection services)
    {
        services.AddSingleton<IApThreeWayMatchEngine, ApThreeWayMatchEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapApThreeWayMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/ap/three-way-match").WithTags("Accounts Payable 3-Way Match");

        group.MapPost("/reconcile", (ThreeWayMatchRequest request, IApThreeWayMatchEngine engine) =>
        {
            var result = engine.ReconcileInvoice(request);
            return Results.Ok(result);
        }).WithName("ReconcileThreeWayMatch");

        return endpoints;
    }
}
