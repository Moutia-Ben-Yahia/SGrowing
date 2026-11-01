using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ConfidentialEnclave;

public static class EnclaveAttestationEndpoints
{
    public static IServiceCollection AddConfidentialEnclaveServices(this IServiceCollection services)
    {
        services.AddSingleton<ConfidentialEnclaveAttestationEngine>();
        services.AddSingleton<HsmKeyLifecycleCoordinatorService>();
        services.AddSingleton<MicroVmConfidentialMeshService>();
        return services;
    }

    public static IEndpointRouteBuilder MapConfidentialEnclaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/confidential-enclave")
            .WithTags("Confidential Micro-VM Enclaves & Hardware Security Modules");

        group.MapPost("/attestation/verify", (
            VerifyAttestationRequest request,
            ConfidentialEnclaveAttestationEngine attestationEngine) =>
        {
            var result = attestationEngine.VerifyReport(request.Report, request.ExpectedNonceHex);
            return Results.Ok(result);
        })
        .WithName("VerifyEnclaveAttestation")
        .WithSummary("Cryptographically validates AMD SEV-SNP, Intel SGX, or AWS Nitro launch measurement and certificates.");

        group.MapGet("/attestation/policy", (
            ConfidentialEnclaveAttestationEngine attestationEngine) =>
        {
            return Results.Ok(attestationEngine.GetPolicy());
        })
        .WithName("GetEnclaveAttestationPolicy")
        .WithSummary("Retrieves the active sovereign enclave attestation policy and golden digest whitelist.");

        group.MapPost("/hsm/keys/generate", (
            string alias,
            HsmKeyType keyType,
            HsmKeyLifecycleCoordinatorService hsmService) =>
        {
            var metadata = hsmService.GenerateKey(alias, keyType);
            return Results.Ok(metadata);
        })
        .WithName("GenerateHsmKey")
        .WithSummary("Generates a hardware-isolated cryptographic key inside the HSM enclave.");

        group.MapPost("/hsm/shamir/split", (
            ShamirSplitRequest request,
            HsmKeyLifecycleCoordinatorService hsmService) =>
        {
            var shares = hsmService.SplitSecret(request);
            return Results.Ok(shares);
        })
        .WithName("SplitSecretShamir")
        .WithSummary("Splits a master secret into M-of-N threshold shares.");

        group.MapPost("/hsm/shamir/reconstruct", (
            ShamirReconstructRequest request,
            HsmKeyLifecycleCoordinatorService hsmService) =>
        {
            var reconstructed = hsmService.ReconstructSecret(request);
            return Results.Ok(new { SecretDataHex = reconstructed });
        })
        .WithName("ReconstructSecretShamir")
        .WithSummary("Reconstructs the original secret from a valid M-of-N threshold subset of shares.");

        group.MapPost("/mesh/channels", (
            EstablishMeshChannelRequest request,
            MicroVmConfidentialMeshService meshService) =>
        {
            var channel = meshService.EstablishChannel(request);
            return Results.Created($"/api/v1/security/confidential-enclave/mesh/channels/{channel.ChannelId}", channel);
        })
        .WithName("EstablishConfidentialMeshChannel")
        .WithSummary("Establishes a mutually attested mATLS encrypted mesh channel between two micro-VM enclaves.");

        return app;
    }
}
