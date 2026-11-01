using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 31:
/// Autonomous Cross-Border Customs Clearance, Harmonized System (HS) Auto-Classification &amp; Dynamic Tariffs Engine.
/// </summary>
public static class CustomsEndpoints
{
    /// <summary>
    /// Registers all Phase 31 Autonomous Customs Clearance, HS Classification, and Tariff services.
    /// </summary>
    public static IServiceCollection AddCustomsServices(this IServiceCollection services)
    {
        services.AddSingleton<IHsCodeClassifierEngine, HsCodeClassifierEngine>();
        services.AddSingleton<IDynamicCustomsTariffService, DynamicCustomsTariffService>();
        services.AddSingleton<IDigitalCustomsDeclarationService, DigitalCustomsDeclarationService>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 31 Customs Minimal API endpoints under /api/v1/logistics/customs.
    /// </summary>
    public static IEndpointRouteBuilder MapCustomsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/customs")
            .WithTags("Cross-Border Customs & Harmonized System Engine");

        // 1. POST /api/v1/logistics/customs/classify
        group.MapPost("/classify", (
            [FromBody] ProductClassificationRequest request,
            [FromServices] IHsCodeClassifierEngine classifierEngine) =>
        {
            try
            {
                var result = classifierEngine.ClassifyProduct(request);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("ClassifyProductHsCode")
        .WithSummary("Hierarchically classifies a raw product title, material, and category into a 6-digit WCO HS code and national tariff lines.");

        // 2. POST /api/v1/logistics/customs/tariffs/calculate
        group.MapPost("/tariffs/calculate", (
            [FromBody] TariffCalculationRequest request,
            [FromServices] IDynamicCustomsTariffService tariffService) =>
        {
            try
            {
                var result = tariffService.CalculateLandedTariff(request);
                return Results.Ok(result);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("CalculateLandedTariff")
        .WithSummary("Calculates landed cost including CIF, preferential trade agreements, excise, VAT, customs fees, and Algerian DAPS.");

        // 3. POST /api/v1/logistics/customs/declarations
        group.MapPost("/declarations", (
            [FromBody] CreateDeclarationRequest request,
            [FromServices] IDigitalCustomsDeclarationService declarationService) =>
        {
            try
            {
                var declaration = declarationService.CreateDeclaration(request);
                return Results.Created($"/api/v1/logistics/customs/declarations/{declaration.DeclarationId}", declaration);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("CreateCustomsDeclaration")
        .WithSummary("Creates, cryptographically signs, and issues a digital Single Administrative Document (DAU), EUR.1 certificate, or ATA Carnet.");

        // 4. GET /api/v1/logistics/customs/declarations/{declarationId}
        group.MapGet("/declarations/{declarationId}", (
            [FromRoute] string declarationId,
            [FromServices] IDigitalCustomsDeclarationService declarationService) =>
        {
            var declaration = declarationService.GetDeclaration(declarationId);
            if (declaration is null)
            {
                return Results.NotFound(new { Message = $"Customs declaration '{declarationId}' was not found." });
            }

            return Results.Ok(declaration);
        })
        .WithName("GetCustomsDeclarationById")
        .WithSummary("Retrieves an authenticated digital customs declaration by ID.");

        // 5. POST /api/v1/logistics/customs/declarations/verify
        group.MapPost("/declarations/verify", (
            [FromBody] VerifyDeclarationQrRequest request,
            [FromServices] IDigitalCustomsDeclarationService declarationService) =>
        {
            var verificationResult = declarationService.VerifyDeclarationQr(request.QrPayload, request.SecretKey);
            return Results.Ok(verificationResult);
        })
        .WithName("VerifyCustomsDeclarationQr")
        .WithSummary("Border checkpoint QR payload verification for cryptographic signature, expiration, and revocation status.");

        // 6. GET /api/v1/logistics/customs/hs/{hsCode}
        group.MapGet("/hs/{hsCode}", (
            [FromRoute] string hsCode,
            [FromServices] IHsCodeClassifierEngine classifierEngine) =>
        {
            var details = classifierEngine.GetHsCodeDetails(hsCode);
            if (details is null)
            {
                return Results.NotFound(new { Message = $"HS code '{hsCode}' not found in customs knowledge base." });
            }

            return Results.Ok(details);
        })
        .WithName("GetHsCodeDetails")
        .WithSummary("Retrieves official WCO heading, national tariff lines, and default duty rates for an HS code.");

        // 7. GET /api/v1/logistics/customs/hs/suggest
        group.MapGet("/hs/suggest", (
            [FromQuery] string q,
            [FromServices] IHsCodeClassifierEngine classifierEngine) =>
        {
            var candidates = classifierEngine.SuggestHsCandidates(q);
            return Results.Ok(candidates);
        })
        .WithName("SuggestHsCandidates")
        .WithSummary("Suggests candidate Harmonized System codes for autocomplete search queries.");

        // 8. POST /api/v1/logistics/customs/declarations/{declarationId}/revoke
        group.MapPost("/declarations/{declarationId}/revoke", (
            [FromRoute] string declarationId,
            [FromBody] RevokeDeclarationRequest request,
            [FromServices] IDigitalCustomsDeclarationService declarationService) =>
        {
            var success = declarationService.RevokeDeclaration(declarationId, request.Reason);
            if (!success)
            {
                return Results.NotFound(new { Message = $"Declaration '{declarationId}' was not found or could not be revoked." });
            }

            return Results.Ok(new { Revoked = true, DeclarationId = declarationId, Reason = request.Reason });
        })
        .WithName("RevokeCustomsDeclaration")
        .WithSummary("Revokes an issued customs declaration, rendering its border QR pass invalid.");

        return group;
    }
}
