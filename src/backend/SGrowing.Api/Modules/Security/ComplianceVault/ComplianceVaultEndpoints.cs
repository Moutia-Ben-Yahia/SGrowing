namespace SGrowing.Api.Modules.Security.ComplianceVault;

public static class ZeroTrustVaultEndpoints
{
    public static IServiceCollection AddZeroTrustVaultServices(this IServiceCollection services)
    {
        services.AddSingleton<IComplianceVaultEngine, ComplianceVaultEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZeroTrustVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/compliance")
            .WithTags("Enterprise Zero-Trust PII Redaction & Merkle Audit Vault");

        group.MapPost("/scan-pii", (IComplianceVaultEngine engine, ScanPiiRequest request) =>
        {
            var result = engine.RedactPii(request);
            return Results.Ok(result);
        })
        .WithName("ScanAndRedactPii")
        .WithSummary("In-flight zero-trust PII masking and synthetic tokenization for safe LLM inference");

        group.MapPost("/record-audit", (IComplianceVaultEngine engine, RecordAuditRequest request) =>
        {
            var leaf = engine.RecordAuditLeaf(request.ActionType, request.ActorId, request.TenantId, request.Payload);
            return Results.Created($"/api/v1/security/compliance/audit/{leaf.LeafId}", leaf);
        })
        .WithName("RecordMerkleAuditLeaf")
        .WithSummary("Append an immutable SHA-256 audit log entry to the platform Merkle tree");

        group.MapGet("/merkle-verify", (IComplianceVaultEngine engine) =>
        {
            var verification = engine.VerifyMerkleRoot();
            return Results.Ok(verification);
        })
        .WithName("VerifyMerkleAuditRoot")
        .WithSummary("Generate cryptographic root hash and tamper-evident proof chain");

        group.MapGet("/residency-boundaries", (IComplianceVaultEngine engine) =>
        {
            var boundaries = engine.ListDataResidencyBoundaries();
            return Results.Ok(boundaries);
        })
        .WithName("ListDataResidencyBoundaries")
        .WithSummary("List geographic data residency isolation zones (GDPR EU, HIPAA US, PDPA APAC)");

        return app;
    }
}

public record RecordAuditRequest(string ActionType, string ActorId, string TenantId, string Payload);
