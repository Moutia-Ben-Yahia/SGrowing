using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for
/// Zero-Knowledge Privacy, Private B2B Invoicing & Proof-of-Solvency.
/// </summary>
public static class ZeroKnowledgeEndpoints
{
    public static IServiceCollection AddZeroKnowledgeSolvencyServices(this IServiceCollection services)
    {
        services.AddSingleton<IZkSolvencyEngine, ZkSolvencyEngine>();
        services.AddSingleton<IZkPrivateInvoiceService, ZkPrivateInvoiceService>();
        services.AddSingleton<IMerkleSolvencyLedgerService, MerkleSolvencyLedgerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapZeroKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/zero-knowledge")
            .WithTags("Decentralized Zero-Knowledge (ZK) Proofs & Proof-of-Solvency");

        // 1. Generate Pedersen Commitment
        group.MapPost("/commitments/create", (
            [FromBody] CreateCommitmentRequest request,
            [FromServices] IZkSolvencyEngine engine) =>
        {
            var commitment = engine.Commit(request.Value, request.BlindingFactorHex);
            var response = new CreateCommitmentResponse
            {
                Commitment = commitment,
                Note = request.Purpose
            };
            return Results.Ok(response);
        })
        .WithName("CreatePedersenCommitment")
        .WithSummary("Generate a high-security Pedersen commitment C = g^v * h^r (mod p) concealing monetary value.");

        // 2. Generate Zero-Knowledge Proof of Solvency
        group.MapPost("/solvency/prove", (
            [FromBody] GenerateSolvencyProofRequest request,
            [FromServices] IZkSolvencyEngine engine,
            [FromServices] IMerkleSolvencyLedgerService ledger) =>
        {
            if (string.IsNullOrWhiteSpace(request.TenantId))
            {
                return Results.BadRequest(new { error = "TenantId is required." });
            }

            try
            {
                var statement = engine.GenerateSolvencyProof(
                    request.TenantId,
                    request.TotalAssets,
                    request.TotalLiabilities,
                    request.RequiredReserveRatio,
                    request.AssetsBlindingHex,
                    request.LiabilitiesBlindingHex);

                ledger.RegisterSolvencyStatement(statement);

                return Results.Ok(statement);
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new
                {
                    error = ex.Message,
                    tenantId = request.TenantId,
                    isSolvent = false
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GenerateProofOfSolvency")
        .WithSummary("Generate certified zero-knowledge statement proving Assets >= Liabilities without revealing balance numbers.");

        // 3. Verify Solvency Statement
        group.MapPost("/solvency/verify", (
            [FromBody] VerifySolvencyProofRequest request,
            [FromServices] IZkSolvencyEngine engine) =>
        {
            if (request?.Statement is null)
            {
                return Results.BadRequest(new { error = "Statement payload is required." });
            }

            var isVerified = engine.VerifySolvencyStatement(request.Statement);
            var response = new VerifySolvencyProofResponse
            {
                IsVerified = isVerified,
                TenantId = request.Statement.TenantId,
                Reason = isVerified ? "Proof of solvency certified valid." : "Cryptographic verification failed.",
                VerifiedAtUtc = DateTime.UtcNow
            };

            return Results.Ok(response);
        })
        .WithName("VerifyProofOfSolvency")
        .WithSummary("Verify zero-knowledge proof of solvency purely over public commitments.");

        // 4. Verify Private B2B Invoice
        group.MapPost("/invoices/verify-private", (
            [FromBody] VerifyPrivateInvoiceRequest request,
            [FromServices] IZkPrivateInvoiceService invoiceService) =>
        {
            if (string.IsNullOrWhiteSpace(request?.InvoiceHash))
            {
                return Results.BadRequest(new { error = "InvoiceHash is required." });
            }

            var result = invoiceService.VerifyPrivateInvoice(request);
            return Results.Ok(result);
        })
        .WithName("VerifyPrivateInvoice")
        .WithSummary("Validate private B2B invoice against purchase order commitment without disclosing buyer identity or pricing.");

        // 5. Retrieve Public Merkle Solvency Root and Optional Inclusion Proof
        group.MapGet("/merkle/solvency-root", (
            [FromQuery] string? tenantId,
            [FromServices] IMerkleSolvencyLedgerService ledger) =>
        {
            var summary = ledger.GetLedgerSummary();

            MerkleSolvencyProof? inclusionProof = null;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                try
                {
                    inclusionProof = ledger.GenerateInclusionProof(tenantId);
                }
                catch (KeyNotFoundException)
                {
                    // Tenant not registered yet; return summary without proof
                }
            }

            var response = new MerkleSolvencyRootResponse
            {
                RootHash = summary.RootHash,
                TotalTenants = summary.TotalTenants,
                TenantIds = summary.TenantIds,
                GeneratedAtUtc = summary.GeneratedAtUtc,
                InclusionProof = inclusionProof
            };

            return Results.Ok(response);
        })
        .WithName("GetMerkleSolvencyRoot")
        .WithSummary("Retrieve public Merkle solvency tree root and optional inclusion proof for a merchant.");

        // 6. Zero-Knowledge Engine Health
        group.MapGet("/health", (
            [FromServices] IZkSolvencyEngine engine) =>
        {
            var health = engine.GetHealth();
            return Results.Ok(health);
        })
        .WithName("GetZeroKnowledgeHealth")
        .WithSummary("Retrieve cryptographic engine health and security parameters.");

        return app;
    }
}
