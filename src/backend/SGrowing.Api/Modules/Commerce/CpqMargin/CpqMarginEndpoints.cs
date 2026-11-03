using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.CpqMargin;

public static class CpqMarginEndpoints
{
    public static IServiceCollection AddCpqMarginServices(this IServiceCollection services)
    {
        services.AddSingleton<ICpqMarginEngine, CpqMarginEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCpqMarginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/commerce/cpq-margin")
            .WithTags("Commerce - CPQ Margin Guardrails");

        group.MapPost("/quote", (QuotePricingRequest request, ICpqMarginEngine engine) =>
        {
            var result = engine.CalculateQuotePricing(request);
            return Results.Ok(result);
        })
        .WithName("CalculateQuotePricing")
        .WithSummary("Calculate multi-year enterprise quote pricing and enforce margin guardrails");

        group.MapGet("/{quoteId}", (string quoteId, ICpqMarginEngine engine) =>
        {
            var quote = engine.GetQuoteById(quoteId);
            return quote is not null ? Results.Ok(quote) : Results.NotFound();
        })
        .WithName("GetQuotePricingById")
        .WithSummary("Retrieve quote breakdown with cryptographic audit hash");

        return endpoints;
    }
}
