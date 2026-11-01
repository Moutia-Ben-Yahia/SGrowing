using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Security.Vault;

public record RecordAuditBlockRequest(
    Guid TenantId,
    string Actor,
    string Action,
    string Resource,
    string? MetadataJson = null
);

public record GdprErasureCascadeRequest(
    Guid TenantId,
    Guid ContactId,
    string DataSubjectEmail,
    string RequesterAgent
);

public record EncryptSensitiveFieldRequest(
    Guid TenantId,
    string Plaintext,
    string FieldPurpose
);

public record DecryptSensitiveFieldRequest(
    Guid TenantId,
    EncryptedPayloadDto Payload
);

public record DecryptSensitiveFieldResponse(
    string Plaintext,
    bool IsSuccess
);

public static class ComplianceVaultEndpoints
{
    public static IEndpointRouteBuilder MapComplianceVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance/vault")
            .WithTags("SOC 2 & GDPR Sovereign Compliance Vault & Cryptographic Ledger");

        // 1. POST /audit/record - Append an immutable audit block into the Merkle compliance ledger
        group.MapPost("/audit/record", async (
            [FromBody] RecordAuditBlockRequest request,
            [FromServices] IMerkleComplianceAuditLedgerService ledger) =>
        {
            if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Actor) ||
                string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Resource))
            {
                return Results.BadRequest(new { error = "TenantId, Actor, Action, and Resource are required." });
            }

            var block = await ledger.AppendAuditBlockAsync(
                request.TenantId,
                request.Actor,
                request.Action,
                request.Resource,
                request.MetadataJson);

            return Results.Ok(block);
        })
        .WithName("RecordAuditBlock")
        .WithSummary("Appends an immutable chained SHA-256 block into the tenant's Merkle compliance audit ledger.");

        // 2. GET /audit/verify - Cryptographically verify ledger integrity and detect tampering
        group.MapGet("/audit/verify", async (
            [FromQuery] Guid tenantId,
            [FromServices] IMerkleComplianceAuditLedgerService ledger) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Valid tenantId query parameter is required." });
            }

            var report = await ledger.VerifyLedgerIntegrityAsync(tenantId);
            return Results.Ok(report);
        })
        .WithName("VerifyLedgerIntegrity")
        .WithSummary("Traverses all ledger blocks and cryptographically verifies sequential hash chaining and pairwise Merkle roots.");

        // 3. POST /gdpr/erasure-cascade - Execute GDPR Right-to-be-Forgotten cascade
        group.MapPost("/gdpr/erasure-cascade", async (
            [FromBody] GdprErasureCascadeRequest request,
            [FromServices] IGdprRightToBeForgottenCascadeService cascadeService,
            CancellationToken cancellationToken) =>
        {
            if (request.TenantId == Guid.Empty || request.ContactId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.DataSubjectEmail))
            {
                return Results.BadRequest(new { error = "TenantId, ContactId, and DataSubjectEmail are required." });
            }

            var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(
                request.TenantId,
                request.ContactId,
                request.DataSubjectEmail,
                request.RequesterAgent ?? "ComplianceVaultApi",
                cancellationToken);

            return Results.Ok(result);
        })
        .WithName("ExecuteGdprErasureCascade")
        .WithSummary("Executes a cascading GDPR Right-to-be-Forgotten across relational tables while preserving statutory financial ledgers.");

        // 4. POST /encryption/encrypt - Field-level envelope encryption with per-tenant DEK
        group.MapPost("/encryption/encrypt", async (
            [FromBody] EncryptSensitiveFieldRequest request,
            [FromServices] IEnvelopeEncryptionService encryptionService) =>
        {
            if (request.TenantId == Guid.Empty || request.Plaintext is null ||
                string.IsNullOrWhiteSpace(request.FieldPurpose))
            {
                return Results.BadRequest(new { error = "TenantId, Plaintext, and FieldPurpose are required." });
            }

            var payload = await encryptionService.EncryptSensitiveFieldAsync(
                request.TenantId,
                request.Plaintext,
                request.FieldPurpose);

            return Results.Ok(payload);
        })
        .WithName("EncryptSensitiveField")
        .WithSummary("Encrypts sensitive PII using AES-256-GCM and a fresh DEK wrapped by a tenant-specific derived KEK.");

        // 5. POST /encryption/decrypt - Field-level envelope decryption
        group.MapPost("/encryption/decrypt", async (
            [FromBody] DecryptSensitiveFieldRequest request,
            [FromServices] IEnvelopeEncryptionService encryptionService) =>
        {
            if (request.TenantId == Guid.Empty || request.Payload is null)
            {
                return Results.BadRequest(new { error = "TenantId and Payload are required." });
            }

            try
            {
                var plaintext = await encryptionService.DecryptSensitiveFieldAsync(request.TenantId, request.Payload);
                return Results.Ok(new DecryptSensitiveFieldResponse(plaintext, true));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Decryption failed or payload tampered.", details = ex.Message });
            }
        })
        .WithName("DecryptSensitiveField")
        .WithSummary("Unwraps the DEK and decrypts the sensitive payload with cryptographic tamper verification.");

        // 6. GET /certificate/export - Export verifiable SOC 2 / ISO 27001 audit bundle
        group.MapGet("/certificate/export", async (
            [FromQuery] Guid tenantId,
            [FromQuery] string? standard,
            [FromQuery] string? auditor,
            [FromServices] IComplianceCertificateGeneratorService certificateGenerator) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Valid tenantId query parameter is required." });
            }

            var bundle = await certificateGenerator.GenerateCertificateBundleAsync(
                tenantId,
                standard ?? "SOC2_TYPE_II",
                auditor);

            return Results.Ok(bundle);
        })
        .WithName("ExportComplianceCertificate")
        .WithSummary("Generates an internationally verifiable SOC 2 Type II or ISO 27001 compliance audit bundle.");

        return app;
    }
}
