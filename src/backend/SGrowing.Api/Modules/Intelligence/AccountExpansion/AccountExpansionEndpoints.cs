using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.AccountExpansion;

public static class AccountExpansionEndpoints
{
    public static IServiceCollection AddAccountExpansionServices(this IServiceCollection services)
    {
        services.AddSingleton<IAccountExpansionEngine, AccountExpansionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAccountExpansionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/account-expansion")
            .WithTags("Intelligence - Account Expansion & Upsell Radar");

        group.MapGet("/opportunities", (IAccountExpansionEngine engine) =>
        {
            var list = engine.GetOpportunities();
            return Results.Ok(list);
        })
        .WithName("GetExpansionOpportunities")
        .WithSummary("Retrieves enterprise accounts with high seat/quota saturation and expansion propensity");

        group.MapPost("/generate-quote", (GenerateExpansionQuoteRequest request, IAccountExpansionEngine engine) =>
        {
            try
            {
                var quote = engine.GenerateQuote(request);
                return Results.Ok(quote);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("GenerateExpansionQuote")
        .WithSummary("Generates an automated tailored expansion add-on or tier upgrade proposal");

        group.MapPost("/ingest-signal", (IngestUtilizationSignalRequest request, IAccountExpansionEngine engine) =>
        {
            var updated = engine.IngestUtilization(request);
            return Results.Ok(updated);
        })
        .WithName("IngestExpansionUtilizationSignal")
        .WithSummary("Ingests usage burst signals and updates account expansion propensity index");

        return endpoints;
    }
}
