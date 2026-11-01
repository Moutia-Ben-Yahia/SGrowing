using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Minimal API endpoints and Dependency Injection extensions for
/// Quantum-Resistant Cryptography (PQC), Post-Quantum Key Exchange & Signature Suite.
/// </summary>
public static class PostQuantumEndpoints
{
    /// <summary>
    /// Registers Post-Quantum Cryptography services in the DI container.
    /// </summary>
    public static IServiceCollection AddPostQuantumServices(this IServiceCollection services)
    {
        services.AddSingleton<IPostQuantumKeyExchangeEngine, PostQuantumKeyExchangeEngine>();
        services.AddSingleton<IMlDsaQuantumSignatureService, MlDsaQuantumSignatureService>();
        services.AddSingleton<ICryptoAgilityMigrationGateway, CryptoAgilityMigrationGateway>();
        return services;
    }

    /// <summary>
    /// Maps Post-Quantum Cryptography Minimal API endpoints under /api/v1/security/pqc.
    /// </summary>
    public static IEndpointRouteBuilder MapPostQuantumEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/pqc")
            .WithTags("Post-Quantum Cryptography (PQC)");

        // 1. Generate Hybrid Key Pair (X25519 + ML-KEM-768)
        group.MapPost("/keys/hybrid", (
            [FromBody] GeneratePqcKeysRequest? request,
            [FromServices] IPostQuantumKeyExchangeEngine kemEngine,
            [FromServices] ICryptoAgilityMigrationGateway gateway,
            HttpContext httpContext) =>
        {
            string? suitesHeader = httpContext.Request.Headers["X-Crypto-Suites"];
            string? secPqcHeader = httpContext.Request.Headers["Sec-PQC-Supported"];
            gateway.NegotiateCipherSuite(suitesHeader, secPqcHeader);

            var keyPair = kemEngine.GenerateHybridKeyPair();
            return Results.Ok(keyPair);
        })
        .WithName("GenerateHybridKeys")
        .WithSummary("Generate a dual-defense hybrid key pair combining Curve25519 ECDH and ML-KEM-768.");

        // 2. KEM Encapsulate
        group.MapPost("/kem/encapsulate", (
            [FromBody] EncapsulateKeyRequest request,
            [FromServices] IPostQuantumKeyExchangeEngine kemEngine,
            [FromServices] ICryptoAgilityMigrationGateway gateway) =>
        {
            if (string.IsNullOrWhiteSpace(request.ClientClassicalPkHex))
            {
                return Results.BadRequest(new { error = "ClientClassicalPkHex is required." });
            }

            try
            {
                var result = kemEngine.EncapsulateKey(request.ClientClassicalPkHex, request.ClientPqcPkHex);
                gateway.RecordCommunication(PqcAlgorithm.HybridClassicalPqc);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (FormatException ex)
            {
                return Results.BadRequest(new { error = "Invalid hex string formatting: " + ex.Message });
            }
        })
        .WithName("KemEncapsulate")
        .WithSummary("Encapsulate hybrid shared secret against client public keys.");

        // 3. KEM Decapsulate
        group.MapPost("/kem/decapsulate", (
            [FromBody] DecapsulateKeyRequest request,
            [FromServices] IPostQuantumKeyExchangeEngine kemEngine,
            [FromServices] ICryptoAgilityMigrationGateway gateway) =>
        {
            if (string.IsNullOrWhiteSpace(request.CiphertextHex) || string.IsNullOrWhiteSpace(request.PrivateKeyHex))
            {
                return Results.BadRequest(new { error = "CiphertextHex and PrivateKeyHex are required." });
            }

            try
            {
                string sharedSecret = kemEngine.DecapsulateKey(request.CiphertextHex, request.PrivateKeyHex);
                gateway.RecordCommunication(PqcAlgorithm.HybridClassicalPqc);
                return Results.Ok(new DecapsulateKeyResponse(sharedSecret));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (FormatException ex)
            {
                return Results.BadRequest(new { error = "Invalid hex string formatting: " + ex.Message });
            }
        })
        .WithName("KemDecapsulate")
        .WithSummary("Decapsulate hybrid ciphertext and derive 256-bit symmetric session key.");

        // 4. Generate ML-DSA Signature Keys
        group.MapPost("/signatures/keys", (
            [FromServices] IMlDsaQuantumSignatureService dsaService,
            [FromServices] ICryptoAgilityMigrationGateway gateway) =>
        {
            var keyPair = dsaService.GenerateSignatureKeyPair();
            gateway.RecordCommunication(PqcAlgorithm.MlDsa65_CrystalsDilithium);
            return Results.Ok(keyPair);
        })
        .WithName("GenerateDsaKeys")
        .WithSummary("Generate NIST FIPS 204 ML-DSA-65 post-quantum signature key pair.");

        // 5. Sign Payload
        group.MapPost("/signatures/sign", (
            [FromBody] SignPayloadRequest request,
            [FromServices] IMlDsaQuantumSignatureService dsaService,
            [FromServices] ICryptoAgilityMigrationGateway gateway) =>
        {
            if (string.IsNullOrWhiteSpace(request.PrivateKeyHex) || string.IsNullOrWhiteSpace(request.SignerDid))
            {
                return Results.BadRequest(new { error = "PrivateKeyHex and SignerDid are required." });
            }

            byte[] payloadBytes;
            if (!string.IsNullOrEmpty(request.PayloadBase64))
            {
                try
                {
                    payloadBytes = Convert.FromBase64String(request.PayloadBase64);
                }
                catch (FormatException)
                {
                    return Results.BadRequest(new { error = "PayloadBase64 is not a valid Base64 string." });
                }
            }
            else if (request.PayloadText is not null)
            {
                payloadBytes = Encoding.UTF8.GetBytes(request.PayloadText);
            }
            else
            {
                return Results.BadRequest(new { error = "Either PayloadBase64 or PayloadText must be provided." });
            }

            try
            {
                var token = dsaService.SignPayload(payloadBytes, request.PrivateKeyHex, request.SignerDid);
                gateway.RecordCommunication(PqcAlgorithm.MlDsa65_CrystalsDilithium);
                return Results.Ok(new SignPayloadResponse(token));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (FormatException ex)
            {
                return Results.BadRequest(new { error = "Invalid hex formatting: " + ex.Message });
            }
        })
        .WithName("SignPayload")
        .WithSummary("Sign arbitrary payload using ML-DSA lattice rejection sampling.");

        // 6. Verify Signature
        group.MapPost("/signatures/verify", (
            [FromBody] VerifySignatureRequest request,
            [FromServices] IMlDsaQuantumSignatureService dsaService,
            [FromServices] ICryptoAgilityMigrationGateway gateway) =>
        {
            if (string.IsNullOrWhiteSpace(request.SignatureHex) || string.IsNullOrWhiteSpace(request.PublicKeyHex))
            {
                return Results.BadRequest(new { error = "SignatureHex and PublicKeyHex are required." });
            }

            byte[] payloadBytes;
            if (!string.IsNullOrEmpty(request.PayloadBase64))
            {
                try
                {
                    payloadBytes = Convert.FromBase64String(request.PayloadBase64);
                }
                catch (FormatException)
                {
                    return Results.BadRequest(new { error = "PayloadBase64 is not a valid Base64 string." });
                }
            }
            else if (request.PayloadText is not null)
            {
                payloadBytes = Encoding.UTF8.GetBytes(request.PayloadText);
            }
            else
            {
                return Results.BadRequest(new { error = "Either PayloadBase64 or PayloadText must be provided." });
            }

            var result = dsaService.VerifySignature(payloadBytes, request.SignatureHex, request.PublicKeyHex);
            gateway.RecordCommunication(PqcAlgorithm.MlDsa65_CrystalsDilithium);
            return Results.Ok(new VerifySignatureResponse(result));
        })
        .WithName("VerifySignature")
        .WithSummary("Verify ML-DSA lattice signature against payload bytes and public key.");

        // 7. Get Quantum Readiness
        group.MapGet("/readiness", ([FromServices] ICryptoAgilityMigrationGateway gateway) =>
        {
            var readiness = gateway.GetQuantumReadinessStatus();
            return Results.Ok(readiness);
        })
        .WithName("GetQuantumReadiness")
        .WithSummary("Retrieve real-time quantum-safe communications telemetry and migration strategy.");

        return app;
    }
}
