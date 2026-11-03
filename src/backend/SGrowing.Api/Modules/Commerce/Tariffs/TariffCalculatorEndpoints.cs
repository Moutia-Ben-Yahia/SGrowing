using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.Tariffs;

public static class TariffCalculatorEndpoints
{
    public static IServiceCollection AddTariffCalculatorServices(this IServiceCollection services)
    {
        services.AddSingleton<ITariffCalculatorEngine, TariffCalculatorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTariffCalculatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/commerce/tariffs")
            .WithTags("Commerce - Global Tariffs & HS Customs");

        group.MapPost("/classify-hs", (string productDescription, ITariffCalculatorEngine engine) =>
        {
            var classification = engine.ClassifyProduct(productDescription);
            return Results.Ok(classification);
        })
        .WithName("ClassifyProductHsCode")
        .WithSummary("Classifies product description into WCO Harmonized System HS code");

        group.MapPost("/calculate-landed-cost", (CalculateLandedCostRequest request, ITariffCalculatorEngine engine) =>
        {
            var breakdown = engine.CalculateLandedCost(request);
            return Results.Ok(breakdown);
        })
        .WithName("CalculateLandedCost")
        .WithSummary("Calculates cross-border landed cost with duties, VAT/GST, and DDP terms");

        group.MapGet("/vat-rate/{countryCode}", (string countryCode, ITariffCalculatorEngine engine) =>
        {
            var rate = engine.GetCountryVatRate(countryCode);
            return Results.Ok(new { Country = countryCode.ToUpperInvariant(), VatRatePercentage = rate });
        })
        .WithName("GetCountryVatRate")
        .WithSummary("Retrieves statutory VAT or GST percentage for destination country");

        return endpoints;
    }
}
