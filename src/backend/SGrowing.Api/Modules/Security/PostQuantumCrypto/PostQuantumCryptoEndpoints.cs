using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.PostQuantumCrypto;

public static class PostQuantumCryptoEndpoints
{
    public static IServiceCollection AddPostQuantumCryptoServices(this IServiceCollection services)
    {
        services.AddSingleton<PostQuantumHybridKemEngine>();
        services.AddSingleton<PostQuantumEnvelopeSealerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapPostQuantumCryptoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/post-quantum-crypto")
            .WithTags("Autonomous Quantum-Resistant Hybrid TLS & Kyber/Dilithium Envelope Encryption");

        group.MapPost("/seal", (
            SealPayloadRequest request,
            PostQuantumEnvelopeSealerService sealer) =>
        {
            var envelope = sealer.SealPayload(request);
            return Results.Ok(envelope);
        });

        group.MapPost("/open", (
            OpenEnvelopeRequest request,
            PostQuantumEnvelopeSealerService sealer) =>
        {
            var (plaintext, verification) = sealer.OpenEnvelope(request);
            return Results.Ok(new OpenEnvelopeApiResponse(
                PlaintextBase64: Convert.ToBase64String(plaintext),
                Verification: verification
            ));
        });

        group.MapPost("/encapsulate", (
            KemEncapsulationApiRequest request,
            PostQuantumHybridKemEngine engine) =>
        {
            var (packet, sharedKey) = engine.Encapsulate(
                request.RecipientClassicalPublicKeySpki,
                request.RecipientLatticePublicKey
            );

            return Results.Ok(new KemEncapsulationApiResponse(
                Packet: packet,
                SharedKeyHex: Convert.ToHexString(sharedKey)
            ));
        });

        return app;
    }
}

public sealed record OpenEnvelopeApiResponse(
    string PlaintextBase64,
    PqcVerificationResult Verification
);

public sealed record KemEncapsulationApiRequest(
    byte[] RecipientClassicalPublicKeySpki,
    byte[] RecipientLatticePublicKey
);

public sealed record KemEncapsulationApiResponse(
    HybridEncapsulationPacket Packet,
    string SharedKeyHex
);
