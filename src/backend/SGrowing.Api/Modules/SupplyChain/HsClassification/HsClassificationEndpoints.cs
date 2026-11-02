using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.HsClassification;

public static class HsClassificationEndpoints
{
    public static IServiceCollection AddHsClassificationServices(this IServiceCollection services)
    {
        services.AddSingleton<HsTariffClassificationEngine>();
        services.AddSingleton<CrossBorderDutyCalculatorService>();
        services.AddSingleton<CustomsDeclarationGeneratorService>();
        return services;
    }

    public static IEndpointRouteBuilder MapHsClassificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/hs-classification")
            .WithTags("Multi-Tenant Cross-Border Customs Harmonization & HS Code Machine Learning");

        group.MapPost("/classify", (
            ClassifyProductApiRequest request,
            HsTariffClassificationEngine engine) =>
        {
            var candidates = engine.ClassifyProduct(request.ProductDescription, request.MaxCandidates ?? 3);
            return Results.Ok(candidates);
        });

        group.MapPost("/landed-cost", (
            LandedCostCalculationRequest request,
            CrossBorderDutyCalculatorService calculator) =>
        {
            var breakdown = calculator.CalculateLandedCost(request);
            return Results.Ok(breakdown);
        });

        group.MapPost("/declarations/generate", (
            GenerateDeclarationApiRequest request,
            CustomsDeclarationGeneratorService generator) =>
        {
            var sad = generator.GenerateDeclaration(
                request.ImporterTaxId,
                request.ExporterName,
                request.CountryOfExport,
                request.CountryOfDestination,
                request.CustomsOfficeCode,
                request.Items);

            return Results.Ok(sad);
        });

        return app;
    }
}

public sealed record ClassifyProductApiRequest(
    string ProductDescription,
    int? MaxCandidates = 3
);

public sealed record GenerateDeclarationApiRequest(
    string ImporterTaxId,
    string ExporterName,
    string CountryOfExport,
    string CountryOfDestination,
    string CustomsOfficeCode,
    IReadOnlyList<SadLineItem> Items
);
