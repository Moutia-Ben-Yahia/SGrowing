using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for
/// Multi-Party Confidential Computing & Homomorphic Data Collaboration Mesh.
/// </summary>
public static class ConfidentialEndpoints
{
    private const string DefaultConsortiumSecret = "SGrowing-Confidential-Mesh-Consortium-Secret-v1";

    public static IServiceCollection AddConfidentialServices(this IServiceCollection services)
    {
        services.AddSingleton<IPaillierHomomorphicEncryptionEngine, PaillierHomomorphicEncryptionEngine>();
        services.AddSingleton<IPrivateSetIntersectionService, PrivateSetIntersectionService>();
        services.AddSingleton<IConfidentialAuditFederationService, ConfidentialAuditFederationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapConfidentialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/confidential")
            .WithTags("Confidential Computing & Homomorphic Mesh");

        // 1. Generate Paillier Keypair
        group.MapPost("/keys/generate", ([FromBody] GenerateKeysRequest? request, [FromServices] IPaillierHomomorphicEncryptionEngine engine) =>
        {
            int keyBits = request?.KeySizeBits > 0 ? request.KeySizeBits : 512;
            try
            {
                var keyPair = engine.GenerateKeyPair(keyBits);
                return Results.Ok(new GenerateKeysResponse { KeyPair = keyPair });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GeneratePaillierKeys")
        .WithSummary("Generate a new Paillier key pair for additive homomorphic encryption.");

        // 2. Homomorphic Encrypt
        group.MapPost("/homomorphic/encrypt", ([FromBody] EncryptValueRequest request, [FromServices] IPaillierHomomorphicEncryptionEngine engine) =>
        {
            if (request?.PublicKey is null)
            {
                return Results.BadRequest(new { error = "PublicKey is required." });
            }

            try
            {
                var encrypted = engine.Encrypt(request.Plaintext, request.PublicKey);
                return Results.Ok(new EncryptValueResponse { Encrypted = encrypted });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("HomomorphicEncrypt")
        .WithSummary("Encrypt a numerical value using the Paillier public key.");

        // 3. Homomorphic Add
        group.MapPost("/homomorphic/add", ([FromBody] HomomorphicAddRequest request, [FromServices] IPaillierHomomorphicEncryptionEngine engine) =>
        {
            if (request?.PublicKey is null ||
                string.IsNullOrWhiteSpace(request.Ciphertext1Hex) ||
                string.IsNullOrWhiteSpace(request.Ciphertext2Hex))
            {
                return Results.BadRequest(new { error = "Ciphertext1Hex, Ciphertext2Hex, and PublicKey are required." });
            }

            try
            {
                string resultHex = engine.AddEncrypted(request.Ciphertext1Hex, request.Ciphertext2Hex, request.PublicKey);
                return Results.Ok(new HomomorphicAddResponse
                {
                    ResultCiphertextHex = resultHex,
                    PublicKey = request.PublicKey
                });
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("HomomorphicAdd")
        .WithSummary("Perform homomorphic addition on two ciphertexts: c_sum = c1 * c2 mod n^2.");

        // 4. Homomorphic Multiply Scalar
        group.MapPost("/homomorphic/multiply-scalar", ([FromBody] HomomorphicScalarMultiplyRequest request, [FromServices] IPaillierHomomorphicEncryptionEngine engine) =>
        {
            if (request?.PublicKey is null || string.IsNullOrWhiteSpace(request.CiphertextHex))
            {
                return Results.BadRequest(new { error = "CiphertextHex and PublicKey are required." });
            }

            try
            {
                string resultHex = engine.MultiplyScalar(request.CiphertextHex, request.Scalar, request.PublicKey);
                return Results.Ok(new HomomorphicScalarMultiplyResponse
                {
                    ResultCiphertextHex = resultHex,
                    PublicKey = request.PublicKey
                });
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("HomomorphicMultiplyScalar")
        .WithSummary("Perform homomorphic scalar multiplication: c_prod = c^k mod n^2.");

        // 5. Homomorphic Decrypt
        group.MapPost("/homomorphic/decrypt", ([FromBody] DecryptValueRequest request, [FromServices] IPaillierHomomorphicEncryptionEngine engine) =>
        {
            if (request?.PrivateKey is null || string.IsNullOrWhiteSpace(request.CiphertextHex))
            {
                return Results.BadRequest(new { error = "CiphertextHex and PrivateKey are required." });
            }

            try
            {
                long plaintext = engine.Decrypt(request.CiphertextHex, request.PrivateKey);
                return Results.Ok(new DecryptValueResponse { Plaintext = plaintext });
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("HomomorphicDecrypt")
        .WithSummary("Decrypt a Paillier ciphertext back to signed 64-bit plaintext.");

        // 6. Private Set Intersection (PSI)
        group.MapPost("/psi/intersect", ([FromBody] ComputePsiRequest request, [FromServices] IPrivateSetIntersectionService psiService) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.ConsortiumSessionId))
            {
                return Results.BadRequest(new { error = "ConsortiumSessionId and candidate sets are required." });
            }

            try
            {
                var result = psiService.ComputeIntersection(
                    request.SetA,
                    request.SetB,
                    request.ConsortiumSessionId,
                    request.ParticipantIds);

                return Results.Ok(new ComputePsiResponse { Result = result });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ComputePrivateSetIntersection")
        .WithSummary("Compute blind Private Set Intersection (A ∩ B) across consortium participants.");

        // 7. Issue Confidential Attestation
        group.MapPost("/attestations/issue", (
            [FromBody] IssueAttestationRequest request,
            [FromHeader(Name = "X-Consortium-Secret")] string? consortiumSecret,
            [FromServices] IConfidentialAuditFederationService auditService) =>
        {
            if (request is null ||
                string.IsNullOrWhiteSpace(request.ConsortiumId) ||
                string.IsNullOrWhiteSpace(request.ComputationType) ||
                string.IsNullOrWhiteSpace(request.InputHash) ||
                string.IsNullOrWhiteSpace(request.ResultCommitment))
            {
                return Results.BadRequest(new { error = "ConsortiumId, ComputationType, InputHash, and ResultCommitment are required." });
            }

            string secret = string.IsNullOrWhiteSpace(consortiumSecret) ? DefaultConsortiumSecret : consortiumSecret;

            try
            {
                var token = auditService.IssueAttestation(request, secret);
                return Results.Ok(token);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("IssueConfidentialAttestation")
        .WithSummary("Issue a non-repudiable HMAC-SHA256 signed audit attestation for confidential computation.");

        // 8. Verify Confidential Attestation
        group.MapPost("/attestations/verify", ([FromBody] VerifyAttestationRequest request, [FromServices] IConfidentialAuditFederationService auditService) =>
        {
            if (request?.Token is null || string.IsNullOrWhiteSpace(request.ConsortiumSecretKey))
            {
                return Results.BadRequest(new { error = "Token and ConsortiumSecretKey are required." });
            }

            bool isValid = auditService.VerifyAttestation(request.Token, request.ConsortiumSecretKey);
            return Results.Ok(new VerifyAttestationResponse
            {
                IsValid = isValid,
                AttestationId = request.Token.AttestationId,
                Reason = isValid
                    ? "Attestation verified successfully against consortium secret key."
                    : "Cryptographic signature validation failed or payload tampered."
            });
        })
        .WithName("VerifyConfidentialAttestation")
        .WithSummary("Verify cryptographic integrity and signature of a confidential attestation token.");

        return app;
    }
}
