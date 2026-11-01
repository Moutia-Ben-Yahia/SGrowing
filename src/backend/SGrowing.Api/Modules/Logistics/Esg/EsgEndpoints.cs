using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 46:
/// Autonomous Energy Grid Integration, Carbon Offsetting & Scope-3 ESG Ledger.
/// </summary>
public static class EsgEndpoints
{
    /// <summary>
    /// Registers all Phase 46 ESG ledger, carbon emissions engine, and carbon offset tokenization services.
    /// </summary>
    public static IServiceCollection AddEsgServices(this IServiceCollection services)
    {
        services.AddSingleton<IEsgComplianceAuditLedger, EsgComplianceAuditLedger>();
        services.AddSingleton<ICarbonOffsetTokenizationService, CarbonOffsetTokenizationService>();
        services.AddSingleton<IScope3CarbonEmissionsEngine, Scope3CarbonEmissionsEngine>();

        return services;
    }

    /// <summary>
    /// Maps Phase 46 ESG Minimal API endpoints under /api/v1/logistics/esg.
    /// </summary>
    public static IEndpointRouteBuilder MapEsgEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/esg")
            .WithTags("Autonomous Energy & Scope-3 ESG Ledger");

        // 1. POST /api/v1/logistics/esg/emissions/calculate
        group.MapPost("/emissions/calculate", (
            [FromBody] CalculateCarbonRequest request,
            [FromServices] IScope3CarbonEmissionsEngine engine) =>
        {
            try
            {
                var footprint = engine.CalculateShipmentFootprint(request);
                return Results.Ok(footprint);
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
        .WithName("CalculateCarbonFootprint")
        .WithSummary("Calculates certified Scope-1, Scope-2, and Scope-3 carbon emissions for a consignment.");

        // 2. POST /api/v1/logistics/esg/offsets/retire
        group.MapPost("/offsets/retire", (
            [FromBody] PurchaseOffsetRequest request,
            [FromServices] ICarbonOffsetTokenizationService tokenizationService) =>
        {
            try
            {
                var certificate = tokenizationService.RetireOffsetCredits(
                    request.TenantId,
                    request.KgToOffset,
                    request.ProjectType,
                    request.AuthoritySecretKey);

                return Results.Ok(certificate);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { Error = ex.Message });
            }
        })
        .WithName("RetireCarbonOffsets")
        .WithSummary("Permanently retires carbon offset credits and issues a cryptographically signed non-fungible certificate.");

        // 3. GET /api/v1/logistics/esg/offsets/{certificateId}
        group.MapGet("/offsets/{certificateId}", (
            [FromRoute] string certificateId,
            [FromServices] ICarbonOffsetTokenizationService tokenizationService) =>
        {
            var certificate = tokenizationService.GetCertificate(certificateId);
            if (certificate is null)
            {
                return Results.NotFound(new { Message = $"Carbon offset certificate '{certificateId}' not found." });
            }

            return Results.Ok(certificate);
        })
        .WithName("GetCarbonOffsetCertificate")
        .WithSummary("Retrieves a verified carbon offset certificate by its unique certificate ID.");

        // 4. GET /api/v1/logistics/esg/balance-sheet/{tenantId}/{year:int}
        group.MapGet("/balance-sheet/{tenantId}/{year:int}", (
            [FromRoute] string tenantId,
            [FromRoute] int year,
            [FromQuery] string? authorityKey,
            [FromServices] IEsgComplianceAuditLedger auditLedger) =>
        {
            try
            {
                var key = string.IsNullOrWhiteSpace(authorityKey) ? "DEFAULT_AUTHORITY_KEY" : authorityKey;
                var sheet = auditLedger.GenerateAuditBalanceSheet(tenantId, year, key);
                return Results.Ok(sheet);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("GetEsgBalanceSheet")
        .WithSummary("Generates an annual corporate ESG balance sheet with cryptographic Merkle tree root.");

        // 5. POST /api/v1/logistics/esg/balance-sheet/verify
        group.MapPost("/balance-sheet/verify", (
            [FromBody] VerifyAuditRequest request,
            [FromServices] IEsgComplianceAuditLedger auditLedger) =>
        {
            try
            {
                var status = auditLedger.VerifyAuditCertificate(request.CertificateId, request.SecretKey);
                var isValid = status == AuditCertificationStatus.VerifiedAudited;
                var details = status switch
                {
                    AuditCertificationStatus.VerifiedAudited => "Certificate successfully verified with valid cryptographic HMAC and intact Merkle ledger chain.",
                    AuditCertificationStatus.TamperDetected => "Verification failed: Signature mismatch or ledger chain tampering detected.",
                    _ => "Certificate status is unverified or self-reported."
                };

                var response = new AuditVerificationResponse(
                    CertificateId: request.CertificateId,
                    Status: status,
                    IsValid: isValid,
                    Details: details,
                    VerifiedAtUtc: DateTime.UtcNow);

                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("VerifyEsgCertificate")
        .WithSummary("Cryptographically verifies certificate proof hash and immutable ledger integrity.");

        return app;
    }
}
