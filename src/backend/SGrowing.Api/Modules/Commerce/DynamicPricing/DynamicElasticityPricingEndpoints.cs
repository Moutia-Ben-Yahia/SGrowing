using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Commerce.DynamicPricing;

#region API Request Contracts

public record CalculateElasticityApiRequest(
    decimal PriceInitial,
    decimal QuantityInitial,
    decimal PriceFinal,
    decimal QuantityFinal,
    decimal? UnitCost = null,
    decimal? DaysOfSupply = null,
    int? HourOfDay = null,
    decimal? TrafficIndex = null);

public record CalculateElasticityApiResponse(
    ArcElasticityResult ElasticityResult,
    OptimalPriceResult? OptimalPricing,
    VelocityAndTrafficAdjustment? VelocityAndTrafficAdjustment);

public record RecordCompetitorObservationApiRequest(
    Guid? TenantId,
    string Sku,
    string CompetitorName,
    decimal ObservedPrice,
    bool InStock,
    decimal? PromotionalPrice = null,
    decimal? ShippingFee = null,
    string Currency = "DZD",
    decimal? SellerRating = null,
    List<CompetitorPriceObservation>? BatchObservations = null);

public record RecordCompetitorObservationApiResponse(
    IReadOnlyList<CompetitorPriceObservation> RecordedObservations,
    MarketMetrics UpdatedMarketMetrics);

public record RepriceApiRequest(
    Guid? TenantId,
    string Sku,
    decimal CurrentPrice,
    RepricingStrategy Strategy,
    MarginCostProfile CostProfile,
    decimal UndercutDelta = 0.05m,
    decimal? DaysOfSupply = null,
    int? HourOfDay = null,
    decimal? TrafficIndex = null,
    decimal? BaselineDemand = null,
    decimal? EstimatedElasticity = null);

public record ValidateMarginGuardrailApiRequest(
    decimal ProposedPrice,
    MarginCostProfile CostProfile,
    bool StrictEnforce = false);

#endregion

/// <summary>
/// Minimal API endpoints and DI service registration for Phase 17:
/// Real-Time Dynamic Pricing, Competitor Scraper Mesh & Elasticity Engine.
/// </summary>
public static class DynamicElasticityPricingEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Registers all dynamic elasticity pricing, competitor scraper mesh,
    /// margin guardrail, and BuyBox repricer services.
    /// </summary>
    public static IServiceCollection AddDynamicElasticityPricingServices(this IServiceCollection services)
    {
        services.AddSingleton<IPriceElasticityEngine, PriceElasticityEngine>();
        services.AddSingleton<ICompetitorScraperMeshService, CompetitorScraperMeshService>();
        services.AddSingleton<IMarginGuardrailValidator, MarginGuardrailValidator>();
        services.AddScoped<IDynamicBuyBoxRepricerService, DynamicBuyBoxRepricerService>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 17 Minimal API endpoints under /api/v1/commerce/dynamic-pricing.
    /// </summary>
    public static IEndpointRouteBuilder MapDynamicElasticityPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/dynamic-pricing")
            .WithTags("Real-Time Dynamic Pricing, Competitor Scraper Mesh & Elasticity Engine");

        // 1. POST /api/v1/commerce/dynamic-pricing/elasticity/calculate
        group.MapPost("/elasticity/calculate", (
            [FromBody] CalculateElasticityApiRequest req,
            [FromServices] IPriceElasticityEngine engine) =>
        {
            var elasticityResult = engine.CalculateArcElasticity(
                req.PriceInitial, req.QuantityInitial, req.PriceFinal, req.QuantityFinal);

            OptimalPriceResult? optimal = null;
            if (req.UnitCost.HasValue && req.PriceFinal > 0 && req.QuantityFinal > 0)
            {
                optimal = engine.ComputeOptimalPrice(new OptimalPriceSearchRequest(
                    BasePrice: req.PriceFinal,
                    BaseDemand: req.QuantityFinal,
                    UnitCost: req.UnitCost.Value,
                    Elasticity: elasticityResult.Elasticity));
            }

            VelocityAndTrafficAdjustment? adjustment = null;
            if (req.DaysOfSupply.HasValue)
            {
                adjustment = engine.AdjustPriceForVelocityAndTraffic(
                    req.PriceFinal,
                    req.DaysOfSupply.Value,
                    req.HourOfDay,
                    req.TrafficIndex);
            }

            return Results.Ok(new CalculateElasticityApiResponse(elasticityResult, optimal, adjustment));
        })
        .WithName("CalculatePriceElasticity")
        .WithSummary("Compute arc price elasticity, classify category, estimate optimal margin price point, and adjust for stock velocity.");

        // 2. POST /api/v1/commerce/dynamic-pricing/competitor/record
        group.MapPost("/competitor/record", async (
            [FromBody] RecordCompetitorObservationApiRequest req,
            [FromServices] ICompetitorScraperMeshService scraperMesh,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var recorded = new List<CompetitorPriceObservation>();

            if (req.BatchObservations is { Count: > 0 })
            {
                var batchResult = await scraperMesh.RecordObservationsBatchAsync(effectiveTenantId, req.BatchObservations, ct);
                recorded.AddRange(batchResult);
            }
            else
            {
                var singleObs = new CompetitorPriceObservation(
                    ObservationId: Guid.NewGuid(),
                    TenantId: effectiveTenantId,
                    Sku: req.Sku,
                    CompetitorName: req.CompetitorName,
                    ObservedPrice: req.ObservedPrice,
                    InStock: req.InStock,
                    PromotionalPrice: req.PromotionalPrice,
                    ShippingFee: req.ShippingFee,
                    Currency: req.Currency,
                    ObservedAtUtc: DateTime.UtcNow,
                    SellerRating: req.SellerRating);

                var result = await scraperMesh.RecordObservationAsync(effectiveTenantId, singleObs, ct);
                recorded.Add(result);
            }

            var updatedMetrics = await scraperMesh.GetMarketMetricsAsync(effectiveTenantId, req.Sku, ct);

            return Results.Ok(new RecordCompetitorObservationApiResponse(recorded, updatedMetrics));
        })
        .WithName("RecordCompetitorObservation")
        .WithSummary("Ingest real-time scraped competitor price signals, update scraper mesh intelligence, and compute updated market metrics.");

        // 3. POST /api/v1/commerce/dynamic-pricing/reprice
        group.MapPost("/reprice", async (
            [FromBody] RepriceApiRequest req,
            [FromServices] IDynamicBuyBoxRepricerService repricer,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);

            var repricingRequest = new RepricingRequest(
                TenantId: effectiveTenantId,
                Sku: req.Sku,
                CurrentPrice: req.CurrentPrice,
                Strategy: req.Strategy,
                CostProfile: req.CostProfile,
                UndercutDelta: req.UndercutDelta,
                DaysOfSupply: req.DaysOfSupply,
                HourOfDay: req.HourOfDay,
                TrafficIndex: req.TrafficIndex,
                BaselineDemand: req.BaselineDemand,
                EstimatedElasticity: req.EstimatedElasticity);

            var recommendation = await repricer.RepriceAsync(repricingRequest, ct);
            return Results.Ok(recommendation);
        })
        .WithName("RepriceBuyBox")
        .WithSummary("Execute real-time BuyBox repricing across AggressiveUndercut, MatchMarketMedian, or MarginMaximizer strategies.");

        // 4. POST /api/v1/commerce/dynamic-pricing/guardrails/validate
        group.MapPost("/guardrails/validate", (
            [FromBody] ValidateMarginGuardrailApiRequest req,
            [FromServices] IMarginGuardrailValidator validator) =>
        {
            if (req.StrictEnforce)
            {
                try
                {
                    decimal validatedPrice = validator.EnforceGuardrail(req.ProposedPrice, req.CostProfile);
                    var result = validator.Validate(validatedPrice, req.CostProfile);
                    return Results.Ok(result);
                }
                catch (MarginGuardrailViolationException ex)
                {
                    return Results.UnprocessableEntity(ex.ValidationResult);
                }
            }

            var validationResult = validator.Validate(req.ProposedPrice, req.CostProfile);
            return Results.Ok(validationResult);
        })
        .WithName("ValidateMarginGuardrails")
        .WithSummary("Evaluate proposed price against COGS, estimated shipping, payment fees, and minimum required margin floor.");

        return app;
    }
}
