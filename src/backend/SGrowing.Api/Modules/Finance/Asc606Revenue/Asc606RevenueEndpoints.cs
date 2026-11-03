using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.Asc606Revenue;

public static class Asc606RevenueEndpoints
{
    public static IServiceCollection AddAsc606RevenueServices(this IServiceCollection services)
    {
        services.AddSingleton<IAsc606RevenueEngine, Asc606RevenueEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAsc606RevenueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/asc606")
            .WithTags("Finance - ASC 606 & IFRS 15 Revenue Ledger");

        group.MapGet("/schedules", (IAsc606RevenueEngine engine) =>
        {
            var list = engine.GetSchedules();
            return Results.Ok(list);
        })
        .WithName("GetAsc606RevenueSchedules")
        .WithSummary("Retrieves multi-period contract amortization schedules and deferred revenue balances");

        group.MapPost("/recognize-month", (RecognizeNextMonthRequest request, IAsc606RevenueEngine engine) =>
        {
            try
            {
                var updated = engine.RecognizeNextMonth(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("RecognizeMonthlyRevenueTranche")
        .WithSummary("Amortizes and recognizes the next monthly revenue tranche under ASC 606 guidelines");

        return endpoints;
    }
}
