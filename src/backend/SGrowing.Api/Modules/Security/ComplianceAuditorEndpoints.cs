using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security;

public record CollectEvidenceRequest(
    string? Actor
);

public record SealVaultRequest(
    string? AuditorOrganization,
    string? SealedBy,
    int? ValidityDays
);

public record VerifyVaultPackageRequest(
    AuditorVaultPackage? Package
);

public static class ComplianceAuditorEndpoints
{
    public static IEndpointRouteBuilder MapComplianceAuditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/compliance-auditor")
            .WithTags("Autonomous Continuous Compliance & SOC 2 / ISO 27001 Auditor Vault");

        // Helper to resolve service from DI or fallback to singleton
        IContinuousComplianceAuditorService ResolveService(HttpContext ctx, IContinuousComplianceAuditorService? injected) =>
            injected ?? ctx.RequestServices.GetService<IContinuousComplianceAuditorService>() ?? ContinuousComplianceAuditorService.DefaultInstance;

        // 1. GET /scorecard - Overall compliance readiness score and category breakdown
        group.MapGet("/scorecard", (
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            var scorecard = service.GetScorecard();
            return Results.Ok(scorecard);
        })
        .WithName("GetComplianceScorecard")
        .WithSummary("Retrieve overall continuous compliance readiness score and Trust Services Criteria category breakdown.");

        // 2. GET /controls - List of 20+ security controls
        group.MapGet("/controls", (
            [FromQuery] TrustServicesCategory? category,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            var controls = service.GetControls(category);
            return Results.Ok(new
            {
                totalCount = controls.Count,
                categoryFilter = category?.ToString(),
                controls
            });
        })
        .WithName("GetComplianceControls")
        .WithSummary("Retrieve list of SOC 2 / ISO 27001 security controls with current evaluation status.");

        // 2b. GET /controls/{controlId} - Single control details with associated evidence
        group.MapGet("/controls/{controlId}", (
            string controlId,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            var control = service.GetControl(controlId);
            if (control == null)
            {
                return Results.NotFound(new { message = $"Control '{controlId}' not found." });
            }

            var evidence = service.GetEvidenceList(controlId: controlId);
            return Results.Ok(new
            {
                control,
                evidenceCount = evidence.Count,
                evidence
            });
        })
        .WithName("GetComplianceControlDetails")
        .WithSummary("Retrieve specific security control definition and collected evidence items.");

        // 3. POST /collect-evidence - Run automated evidence harvest sweep
        group.MapPost("/collect-evidence", async (
            [FromBody] CollectEvidenceRequest? request,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService,
            CancellationToken ct) =>
        {
            var service = ResolveService(ctx, injectedService);
            string actor = !string.IsNullOrWhiteSpace(request?.Actor) ? request.Actor : "ComplianceAutomationEngine";
            var sweepResult = await service.RunEvidenceHarvestSweepAsync(actor, ct);
            return Results.Ok(sweepResult);
        })
        .WithName("RunComplianceEvidenceSweep")
        .WithSummary("Trigger an automated evidence harvest sweep across Merkle ledger, rate limiter, encryption, and privacy subsystems.");

        // 4. POST /seal-vault - Generate a sealed, cryptographically hashed Auditor Vault Package
        group.MapPost("/seal-vault", (
            [FromBody] SealVaultRequest? request,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            string auditorOrg = !string.IsNullOrWhiteSpace(request?.AuditorOrganization)
                ? request.AuditorOrganization
                : "AICPA / Schellman & Co.";
            string sealedBy = !string.IsNullOrWhiteSpace(request?.SealedBy)
                ? request.SealedBy
                : "Chief Information Security Officer (CISO)";
            int validityDays = request?.ValidityDays ?? 90;

            var package = service.GenerateSealedVaultPackage(auditorOrg, sealedBy, validityDays);
            return Results.Created($"/api/v1/security/compliance-auditor/vault/{package.PackageId}", package);
        })
        .WithName("GenerateSealedAuditorVaultPackage")
        .WithSummary("Compile and cryptographically seal an AICPA SOC 2 / ISO 27001 Auditor Vault Package.");

        // 5. GET /vault/{packageId} - Download or inspect auditor package
        group.MapGet("/vault/{packageId:guid}", (
            Guid packageId,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            var package = service.GetVaultPackage(packageId);
            if (package == null)
            {
                return Results.NotFound(new { message = $"Auditor vault package '{packageId}' was not found." });
            }
            return Results.Ok(package);
        })
        .WithName("GetAuditorVaultPackage")
        .WithSummary("Retrieve or inspect an existing sealed Auditor Vault Package by PackageId.");

        // 5b. GET /vault - List all sealed packages
        group.MapGet("/vault", (
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            var packages = service.GetVaultPackages();
            return Results.Ok(new
            {
                totalPackages = packages.Count,
                packages
            });
        })
        .WithName("ListAuditorVaultPackages")
        .WithSummary("List all historically sealed Auditor Vault Packages.");

        // 5c. POST /vault/verify/{packageId} - Verify tamper integrity of package in vault
        group.MapPost("/vault/verify/{packageId:guid}", (
            Guid packageId,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            var service = ResolveService(ctx, injectedService);
            var verification = service.VerifyVaultPackage(packageId);
            return Results.Ok(verification);
        })
        .WithName("VerifyAuditorVaultPackageById")
        .WithSummary("Perform cryptographic tamper verification on a stored auditor vault package.");

        // 5d. POST /vault/verify - Verify tamper integrity of supplied package payload
        group.MapPost("/vault/verify", (
            [FromBody] VerifyVaultPackageRequest request,
            HttpContext ctx,
            [FromServices] IContinuousComplianceAuditorService? injectedService) =>
        {
            if (request?.Package == null)
            {
                return Results.BadRequest(new { message = "Package payload is required for verification." });
            }

            var service = ResolveService(ctx, injectedService);
            var verification = service.VerifyVaultPackage(request.Package);
            return Results.Ok(verification);
        })
        .WithName("VerifyAuditorVaultPackagePayload")
        .WithSummary("Verify cryptographic integrity of an externally provided Auditor Vault Package.");

        return app;
    }
}
