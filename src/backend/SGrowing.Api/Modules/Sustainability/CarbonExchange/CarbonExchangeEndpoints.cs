using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sustainability.CarbonExchange;

public static class CarbonExchangeEndpoints
{
    public static IServiceCollection AddCarbonExchangeServices(this IServiceCollection services)
    {
        services.AddSingleton<GhgEmissionsCalculatorEngine>();
        services.AddSingleton<RecTokenizationAndMatchingService>();
        services.AddSingleton<NetZeroOptimizationRecommender>();
        return services;
    }

    public static IEndpointRouteBuilder MapCarbonExchangeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sustainability/carbon-exchange")
            .WithTags("Autonomous Carbon Offsetting & REC Trading Exchange");

        group.MapPost("/assessments", (
            CalculateFootprintRequest request,
            GhgEmissionsCalculatorEngine engine) =>
        {
            var result = engine.CalculateFootprint(request);
            return Results.Ok(result);
        });

        group.MapPost("/recommendations", (
            GhgAssessmentResult assessment,
            NetZeroOptimizationRecommender recommender) =>
        {
            var recommendations = recommender.GenerateRecommendations(assessment);
            return Results.Ok(recommendations);
        });

        group.MapGet("/certificates", (RecTokenizationAndMatchingService service) =>
        {
            return Results.Ok(service.GetActiveCertificates());
        });

        group.MapPost("/orders", (
            PlaceOrderApiRequest request,
            RecTokenizationAndMatchingService service) =>
        {
            var (order, matches) = service.PlaceOrder(
                request.TenantId,
                request.Side,
                request.QuantityTonnes,
                request.LimitPriceEur,
                request.Standard);

            return Results.Ok(new { Order = order, Matches = matches });
        });

        group.MapPost("/retirements", (
            RetireCertificateApiRequest request,
            RecTokenizationAndMatchingService service) =>
        {
            var receipt = service.RetireCertificate(request.CertificateId, request.TenantId, request.Reason);
            return Results.Ok(receipt);
        });

        return app;
    }
}

public sealed record PlaceOrderApiRequest(
    string TenantId,
    OrderBookSide Side,
    double QuantityTonnes,
    decimal LimitPriceEur,
    CreditCertificateStandard Standard
);

public sealed record RetireCertificateApiRequest(
    string CertificateId,
    string TenantId,
    string Reason
);
