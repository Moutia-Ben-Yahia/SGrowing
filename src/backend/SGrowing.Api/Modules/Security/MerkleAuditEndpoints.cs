using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security;

public record RecordAuditEventRequest(
    Guid? TenantId,
    Guid? ActorId,
    string ActorEmail,
    string Action,
    string Resource,
    string? MetadataJson
);

public record AuditEventListResponse(
    List<MerkleAuditEvent> Events,
    int TotalCount,
    int Limit,
    int Offset
);

public record VerifyProofRequest(
    string LeafHash,
    List<MerkleProofStep> ProofPath,
    string ExpectedRootHash
);

public record VerifyProofResponse(
    bool IsValid,
    string CalculatedRoot,
    string ExpectedRoot,
    string VerificationMessage
);

public record SealEpochRequest(
    Guid? TenantId,
    string? SealedBy
);

public record SimulateTamperRequest(
    Guid EventId,
    string? TamperedMetadata
);

public static class MerkleAuditEndpoints
{
    public static IEndpointRouteBuilder MapMerkleAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/merkle-audit")
            .WithTags("Tamper-Evident Merkle Audit Ledger & Compliance Sealer");

        // Helper to resolve service from DI or fallback to singleton
        IMerkleAuditLedgerService ResolveService(HttpContext ctx, IMerkleAuditLedgerService? injected) =>
            injected ?? ctx.RequestServices.GetService<IMerkleAuditLedgerService>() ?? MerkleAuditLedgerService.DefaultInstance;

        // 1. POST /events: Record an audit event
        group.MapPost("/events", async (
            [FromBody] RecordAuditEventRequest req,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            var tenantId = req.TenantId.GetValueOrDefault(MerkleAuditLedgerService.DefaultTenantId);
            var actorId = req.ActorId.GetValueOrDefault(Guid.NewGuid());

            if (string.IsNullOrWhiteSpace(req.ActorEmail) || string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.Resource))
            {
                return Results.BadRequest(new { error = "ActorEmail, Action, and Resource are required fields." });
            }

            var recorded = await service.RecordAuditEventAsync(
                tenantId: tenantId,
                actorId: actorId,
                actorEmail: req.ActorEmail,
                action: req.Action,
                resource: req.Resource,
                metadataJson: req.MetadataJson
            );

            return Results.Created($"/api/v1/security/merkle-audit/events/{recorded.Id}", recorded);
        })
        .WithName("RecordMerkleAuditEvent")
        .WithSummary("Appends a cryptographically hashed audit event to the tamper-evident ledger");

        // 2. GET /events: List audit events with pagination and filters
        group.MapGet("/events", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? action,
            [FromQuery] string? resource,
            [FromQuery] bool? isSealed,
            [FromQuery] int limit,
            [FromQuery] int offset,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            var effectiveTenantId = tenantId.GetValueOrDefault(MerkleAuditLedgerService.DefaultTenantId);
            int take = limit <= 0 ? 50 : Math.Min(limit, 200);
            int skip = Math.Max(offset, 0);

            var events = await service.GetEventsAsync(
                tenantId: effectiveTenantId,
                action: action,
                resource: resource,
                isSealed: isSealed,
                limit: take,
                offset: skip
            );

            // Fetch overall count for pagination metadata
            var allEvents = await service.GetEventsAsync(
                tenantId: effectiveTenantId,
                action: action,
                resource: resource,
                isSealed: isSealed,
                limit: 10000,
                offset: 0
            );

            return Results.Ok(new AuditEventListResponse(
                Events: events,
                TotalCount: allEvents.Count,
                Limit: take,
                Offset: skip
            ));
        })
        .WithName("ListMerkleAuditEvents")
        .WithSummary("Lists ledger events with optional filtering by tenant, action, resource, or sealed status");

        // 3. GET /events/{id}/proof: Retrieve Merkle inclusion proof for an event
        group.MapGet("/events/{id:guid}/proof", async (
            Guid id,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            try
            {
                var proof = await service.GetInclusionProofAsync(id);
                return Results.Ok(proof);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Audit event with id '{id}' was not found." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetMerkleEventInclusionProof")
        .WithSummary("Generates the minimal Merkle proof path required to verify an event's presence in the root");

        // 4. POST /verify-proof: Cryptographically verify a leaf hash against a proof path and expected root
        group.MapPost("/verify-proof", (
            [FromBody] VerifyProofRequest req,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);

            if (string.IsNullOrWhiteSpace(req.LeafHash) || string.IsNullOrWhiteSpace(req.ExpectedRootHash))
            {
                return Results.BadRequest(new { error = "LeafHash and ExpectedRootHash cannot be empty." });
            }

            bool isValid = service.VerifyInclusionProof(req.LeafHash, req.ProofPath ?? new(), req.ExpectedRootHash);

            string message = isValid
                ? "Cryptographic inclusion proof verified successfully. Leaf is guaranteed present in the target Merkle root."
                : "Proof verification failed! Sibling hash mismatch or tampered leaf digest detected.";

            return Results.Ok(new VerifyProofResponse(
                IsValid: isValid,
                CalculatedRoot: isValid ? req.ExpectedRootHash : "MISMATCH",
                ExpectedRoot: req.ExpectedRootHash,
                VerificationMessage: message
            ));
        })
        .WithName("VerifyMerkleInclusionProof")
        .WithSummary("Cryptographically validates a Merkle proof path against an expected root hash");

        // 5. POST /seal-epoch: Manually trigger epoch sealing for open events
        group.MapPost("/seal-epoch", async (
            [FromBody] SealEpochRequest? req,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            var tenantId = req?.TenantId.GetValueOrDefault(MerkleAuditLedgerService.DefaultTenantId) ?? MerkleAuditLedgerService.DefaultTenantId;
            var sealedBy = req?.SealedBy ?? "Security Operator (Manual Trigger)";

            try
            {
                var epoch = await service.SealCurrentEpochAsync(tenantId, sealedBy);
                return Results.Ok(epoch);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SealMerkleAuditEpoch")
        .WithSummary("Batches unsealed events into an immutable sealed epoch with Merkle root, HMAC seal, and hash chaining");

        // 6. GET /epochs: List sealed epochs with roots, signatures, and chained hashes
        group.MapGet("/epochs", async (
            [FromQuery] Guid? tenantId,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            var effectiveTenantId = tenantId.GetValueOrDefault(MerkleAuditLedgerService.DefaultTenantId);
            var epochs = await service.GetEpochsAsync(effectiveTenantId);
            return Results.Ok(epochs);
        })
        .WithName("ListMerkleEpochs")
        .WithSummary("Retrieves all sealed audit epochs, signatures, Merkle roots, and chained block hashes");

        // 7. GET /compliance-manifest: Export verifiable audit manifest for auditors
        group.MapGet("/compliance-manifest", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? epochId,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            var effectiveTenantId = tenantId.GetValueOrDefault(MerkleAuditLedgerService.DefaultTenantId);

            try
            {
                var manifest = await service.GenerateComplianceManifestAsync(effectiveTenantId, epochId);
                return Results.Ok(manifest);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetMerkleComplianceManifest")
        .WithSummary("Generates an exportable SOC 2 / GDPR verifiable cryptographic compliance manifest");

        // 8. POST /simulate-tamper: Test tamper detection by simulating modified event payload
        group.MapPost("/simulate-tamper", async (
            [FromBody] SimulateTamperRequest req,
            [FromServices] IMerkleAuditLedgerService? injectedService,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx, injectedService);
            try
            {
                var simulation = await service.SimulateTamperAsync(req.EventId, req.TamperedMetadata);
                return Results.Ok(simulation);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SimulateMerkleAuditTamper")
        .WithSummary("Demonstrates adversary tamper detection by simulating unauthorized metadata modification");

        return app;
    }
}
