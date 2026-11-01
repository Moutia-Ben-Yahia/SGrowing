using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.VerifiableIdentity;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 34:
/// Decentralized Identity (DID), W3C Verifiable Credentials & Biometric Courier Onboarding.
/// </summary>
public static class VerifiableIdentityEndpoints
{
    public const string DefaultAuthoritySecret = "SGrowing-AlgeriaPost-MasterAuthoritySecret-2026";

    /// <summary>
    /// Registers verifiable identity, credential engine, courier onboarding, and biometric services.
    /// </summary>
    public static IServiceCollection AddVerifiableIdentityServices(this IServiceCollection services)
    {
        services.AddSingleton<IVerifiableCredentialEngine, VerifiableCredentialEngine>();
        services.AddSingleton<ICourierOnboardingAttestationService, CourierOnboardingAttestationService>();
        services.AddSingleton<IBiometricCustodyUnlockService, BiometricCustodyUnlockService>();
        return services;
    }

    /// <summary>
    /// Maps all Verifiable Identity and Biometric Custody endpoints under '/api/v1/identity/verifiable'.
    /// </summary>
    public static IEndpointRouteBuilder MapVerifiableIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity/verifiable")
            .WithTags("Verifiable Identity, W3C Credentials & Biometric Courier Custody");

        // 1. POST /dids - Generate W3C compliant did:key Document
        group.MapPost("/dids", ([FromBody] CreateDidRequest? request, [FromServices] IVerifiableCredentialEngine engine) =>
        {
            string keyType = request?.KeyType ?? "Ed25519";
            var didDoc = engine.CreateDid(keyType);
            return Results.Ok(didDoc);
        })
        .WithName("CreateDid")
        .WithSummary("Generate a new W3C-compliant did:key identifier and DID Document.");

        // 2. POST /credentials/issue - Issue cryptographically signed W3C Verifiable Credential
        group.MapPost("/credentials/issue", (
            [FromBody] IssueCredentialRequest request,
            [FromServices] IVerifiableCredentialEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(request.IssuerDid) ||
                string.IsNullOrWhiteSpace(request.SubjectDid) ||
                string.IsNullOrWhiteSpace(request.CredentialType))
            {
                return Results.BadRequest(new { error = "IssuerDid, SubjectDid, and CredentialType are required." });
            }

            TimeSpan? validity = request.ValidityDays.HasValue
                ? TimeSpan.FromDays(request.ValidityDays.Value)
                : null;

            var vc = engine.IssueCredential(
                request.IssuerDid,
                request.SubjectDid,
                request.CredentialType,
                request.Claims,
                request.SecretKey,
                validity);

            return Results.Ok(vc);
        })
        .WithName("IssueCredential")
        .WithSummary("Issue a tamper-evident W3C Verifiable Credential signed with HMAC-SHA256.");

        // 3. POST /credentials/verify - Verify VC signature, expiration, and revocation status
        group.MapPost("/credentials/verify", (
            [FromBody] VerifyCredentialRequest request,
            [FromServices] IVerifiableCredentialEngine engine) =>
        {
            if (request.Credential is null || string.IsNullOrWhiteSpace(request.SecretKey))
            {
                return Results.BadRequest(new { error = "Credential and SecretKey are required for verification." });
            }

            var result = engine.VerifyCredential(request.Credential, request.SecretKey);
            return Results.Ok(result);
        })
        .WithName("VerifyCredential")
        .WithSummary("Verify cryptographic signature, expiration date, and revocation status of a Verifiable Credential.");

        // 4. POST /credentials/{credentialId}/revoke - Revoke a credential with a timestamped reason
        group.MapPost("/credentials/{credentialId}/revoke", (
            [FromRoute] string credentialId,
            [FromBody] RevokeCredentialRequest request,
            [FromServices] IVerifiableCredentialEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(credentialId) || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "CredentialId and Reason are required." });
            }

            var record = engine.RevokeCredential(credentialId, request.Reason);
            return Results.Ok(record);
        })
        .WithName("RevokeCredential")
        .WithSummary("Revoke an issued credential and publish audit entry to the revocation registry.");

        // 5. POST /couriers/onboard - Onboard courier, validate government IDs, and issue badge VC
        group.MapPost("/couriers/onboard", (
            [FromBody] CourierOnboardingRequest request,
            [FromQuery] string? issuerSecretKey,
            [FromServices] ICourierOnboardingAttestationService courierService) =>
        {
            try
            {
                string secret = string.IsNullOrWhiteSpace(issuerSecretKey) ? DefaultAuthoritySecret : issuerSecretKey;
                var result = courierService.OnboardCourier(request, secret);
                var response = new OnboardCourierResponse
                {
                    Badge = result.Badge,
                    Credential = result.Credential,
                    CourierDidDocument = result.CourierDidDocument
                };
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("OnboardCourier")
        .WithSummary("Validate government NIN & driver license, clear background check, and issue W3C courier credential.");

        // 6. GET /couriers/{courierDid}/badge - Retrieve courier badge
        group.MapPost("/couriers/{courierDid}/float-tier", (
            [FromRoute] string courierDid,
            [FromBody] UpdateCashFloatTierRequest request,
            [FromServices] ICourierOnboardingAttestationService courierService) =>
        {
            try
            {
                var updated = courierService.UpdateCashFloatLimit(courierDid, request.NewTier);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Courier badge not found for DID '{courierDid}'." });
            }
        })
        .WithName("UpdateCourierFloatTier")
        .WithSummary("Update authorized Cash-on-Delivery float tier for courier.");

        group.MapGet("/couriers/{courierDid}/badge", (
            [FromRoute] string courierDid,
            [FromServices] ICourierOnboardingAttestationService courierService) =>
        {
            var badge = courierService.GetCourierBadge(courierDid);
            return badge is not null
                ? Results.Ok(badge)
                : Results.NotFound(new { error = $"Courier badge not found for DID '{courierDid}'." });
        })
        .WithName("GetCourierBadge")
        .WithSummary("Retrieve courier credential badge details and cash float limits.");

        // 7. POST /custody/challenge - Generate single-use biometric liveness challenge
        group.MapPost("/custody/challenge", (
            [FromBody] GenerateChallengeRequest request,
            [FromServices] IBiometricCustodyUnlockService custodyService) =>
        {
            if (string.IsNullOrWhiteSpace(request.CourierDid))
            {
                return Results.BadRequest(new { error = "CourierDid is required." });
            }

            var challenge = custodyService.GenerateLivenessChallenge(request.CourierDid);
            return Results.Ok(challenge);
        })
        .WithName("GenerateBiometricChallenge")
        .WithSummary("Issue dynamic single-use biometric liveness challenge with 3-minute TTL.");

        // 8. POST /custody/unlock - Verify liveness + geofence and unlock custody
        group.MapPost("/custody/unlock", (
            [FromBody] CustodyUnlockRequest request,
            [FromQuery] string? authoritySecretKey,
            [FromServices] IBiometricCustodyUnlockService custodyService) =>
        {
            string secret = string.IsNullOrWhiteSpace(authoritySecretKey) ? DefaultAuthoritySecret : authoritySecretKey;
            var result = custodyService.AuthorizeCustodyUnlock(request, secret);
            return result.IsAuthorized ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("AuthorizeCustodyUnlock")
        .WithSummary("Verify biometric challenge response, 50m geofence, and authorize custody handover.");

        // 9. GET /custody/certificates/{certificateId} - Retrieve signed handover certificate
        group.MapGet("/custody/certificates/{certificateId}", (
            [FromRoute] string certificateId,
            [FromServices] IBiometricCustodyUnlockService custodyService) =>
        {
            var cert = custodyService.GetHandoverCertificate(certificateId);
            return cert is not null
                ? Results.Ok(cert)
                : Results.NotFound(new { error = $"Handover certificate '{certificateId}' not found." });
        })
        .WithName("GetHandoverCertificate")
        .WithSummary("Retrieve immutable signed custody handover certificate.");

        return app;
    }
}

/// <summary>
/// Request model for credential verification endpoint.
/// </summary>
public record VerifyCredentialRequest
{
    public required VerifiableCredential Credential { get; init; }
    public required string SecretKey { get; init; }
}
