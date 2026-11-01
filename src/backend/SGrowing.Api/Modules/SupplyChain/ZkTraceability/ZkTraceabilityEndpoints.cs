using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.ZkTraceability;

public static class ZkTraceabilityEndpoints
{
    public static IServiceCollection AddZkTraceabilityServices(this IServiceCollection services)
    {
        services.AddSingleton<ZeroKnowledgeOriginProofEngine>();
        services.AddSingleton<SanctionComplianceScreeningService>();
        services.AddSingleton<VerifiableBOMProvenanceService>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkTraceabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/zk-traceability")
            .WithTags("Supply Chain Zero-Knowledge Traceability");

        group.MapPost("/proofs/origin/generate", (
            GenerateOriginProofRequest request,
            ZeroKnowledgeOriginProofEngine engine) =>
        {
            var salts = request.Manifest.Items
                .Select(_ => Guid.NewGuid().ToString("N"))
                .ToList();

            var proof = engine.GenerateOriginProof(request, salts);
            return Results.Ok(proof);
        })
        .WithName("GenerateOriginProof")
        .WithSummary("Issues a zero-knowledge origin compliance proof for an assembled BOM manifest.");

        group.MapPost("/proofs/origin/verify", (
            VerifyOriginProofRequest request,
            ZeroKnowledgeOriginProofEngine engine) =>
        {
            var status = engine.VerifyProof(request);
            return Results.Ok(new { Status = status, IsValid = status == OriginVerificationStatus.Verified });
        })
        .WithName("VerifyOriginProof")
        .WithSummary("Verifies an incoming origin compliance proof against a public commitment root.");

        group.MapPost("/screening/sanctions/verify", (
            SanctionScreeningRequest request,
            SanctionComplianceScreeningService screeningService) =>
        {
            var result = screeningService.ScreenBlindedSuppliers(request);
            return Results.Ok(result);
        })
        .WithName("ScreenBlindedSuppliers")
        .WithSummary("Performs privacy-preserving anti-sanction screening on blinded supplier hashes.");

        group.MapGet("/screening/sanctions/version", (
            SanctionComplianceScreeningService screeningService) =>
        {
            return Results.Ok(new
            {
                Version = screeningService.GetSanctionListVersion(),
                ActiveSanctionedEntitiesCount = screeningService.GetActiveSanctionCount()
            });
        })
        .WithName("GetSanctionListVersion")
        .WithSummary("Retrieves the active international sanction database version stamp.");

        return app;
    }
}
