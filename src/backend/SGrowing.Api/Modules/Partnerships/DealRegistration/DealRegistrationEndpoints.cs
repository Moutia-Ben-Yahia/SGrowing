using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Partnerships.DealRegistration;

public static class DealRegistrationEndpoints
{
    public static IServiceCollection AddDealRegistrationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDealRegistrationEngine, DealRegistrationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDealRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/partnerships/deal-registration")
            .WithTags("Partnerships - Co-Selling & Deal Registration");

        group.MapPost("/register", (RegisterPartnerDealRequest request, IDealRegistrationEngine engine) =>
        {
            var result = engine.RegisterDeal(request);
            return Results.Ok(result);
        })
        .WithName("RegisterPartnerDeal")
        .WithSummary("Registers a partner deal with collision protection and 90-day exclusivity locking");

        group.MapGet("/deals", (IDealRegistrationEngine engine) =>
        {
            var deals = engine.GetAllDeals();
            return Results.Ok(deals);
        })
        .WithName("GetAllRegisteredPartnerDeals")
        .WithSummary("Lists all registered partner deals, commission projections, and exclusivity statuses");

        group.MapPost("/approve", (ApproveDealRequest request, IDealRegistrationEngine engine) =>
        {
            var result = engine.ApproveDeal(request);
            return Results.Ok(result);
        })
        .WithName("ApprovePartnerDealRegistration")
        .WithSummary("Approves deal registration exclusivity for a certified co-selling partner");

        return endpoints;
    }
}
