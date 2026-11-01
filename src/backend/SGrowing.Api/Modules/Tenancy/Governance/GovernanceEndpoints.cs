using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Tenancy.Governance;

public sealed record DomainVerifyRequest(
    Guid TenantId,
    string Domain,
    AcmeChallengeType ChallengeType = AcmeChallengeType.Dns01,
    string? AccountThumbprint = null
);

public sealed record DomainVerifyResponse(
    bool Success,
    Guid ChallengeId,
    string Domain,
    AcmeChallengeType Type,
    AcmeChallengeStatus Status,
    string Token,
    string KeyAuthorization,
    string DnsTxtRecordName,
    string DnsTxtRecordValue,
    string Http01Path,
    string? ErrorMessage,
    DateTime TimestampUtc
);

public sealed record IssueCertificateApiRequest(
    Guid TenantId,
    string PrimaryDomain,
    List<string>? SubjectAlternativeNames = null,
    CertificateIssuer Issuer = CertificateIssuer.LetsEncrypt,
    List<string>? CipherSuites = null
);

public sealed record StartMigrationRequest(
    Guid TenantId,
    string SourceShard,
    string TargetShard
);

public sealed record CutoverApiRequest(
    Guid MigrationId,
    int? MaxPauseMilliseconds = 50
);

public sealed record ValidateBrandingRequest(
    Guid TenantId,
    string? CustomCss,
    string ForegroundColor,
    string BackgroundColor,
    bool IsLargeText = false
);

public sealed record BrandingValidationResponse(
    Guid TenantId,
    bool CssClean,
    IReadOnlyList<CssThreat> CssThreats,
    string SanitizedCss,
    ContrastEvaluationResult ContrastEvaluation,
    DateTime EvaluatedAtUtc
);

public static class GovernanceEndpoints
{
    public static IServiceCollection AddSovereignGovernanceServices(this IServiceCollection services)
    {
        services.AddSingleton<ICustomDomainAcmeSslAutopilot, CustomDomainAcmeSslAutopilot>();
        services.AddSingleton<IZeroDowntimeTenantMigrationMesh, ZeroDowntimeTenantMigrationMesh>();
        services.AddSingleton<IMultiTenantBrandingGovernanceService, MultiTenantBrandingGovernanceService>();
        return services;
    }

    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenancy/governance")
            .WithTags("Sovereign Multi-Tenant Custom Domain SSL Autopilot & Zero-Downtime Migration Mesh");

        // 1. POST /domains/verify
        group.MapPost("/domains/verify", async (
            [FromBody] DomainVerifyRequest request,
            ICustomDomainAcmeSslAutopilot autopilot) =>
        {
            try
            {
                var challenge = autopilot.CreateChallenge(
                    request.TenantId,
                    request.Domain,
                    request.ChallengeType,
                    request.AccountThumbprint
                );

                var verifyResult = await autopilot.VerifyChallengeAsync(challenge.ChallengeId);

                var response = new DomainVerifyResponse(
                    Success: verifyResult.Success,
                    ChallengeId: challenge.ChallengeId,
                    Domain: challenge.Domain,
                    Type: challenge.Type,
                    Status: verifyResult.Success ? AcmeChallengeStatus.Valid : AcmeChallengeStatus.Failed,
                    Token: challenge.Token,
                    KeyAuthorization: challenge.KeyAuthorization,
                    DnsTxtRecordName: challenge.DnsTxtRecordName,
                    DnsTxtRecordValue: challenge.DnsTxtRecordValue,
                    Http01Path: challenge.Http01Path,
                    ErrorMessage: verifyResult.ErrorMessage,
                    TimestampUtc: DateTime.UtcNow
                );

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("VerifyCustomDomainChallenge")
        .WithSummary("Generate and verify ACME HTTP-01 or DNS-01 challenge for sovereign custom domain");

        // 2. POST /domains/issue-certificate
        group.MapPost("/domains/issue-certificate", async (
            [FromBody] IssueCertificateApiRequest request,
            ICustomDomainAcmeSslAutopilot autopilot) =>
        {
            try
            {
                var cert = await autopilot.IssueCertificateAsync(
                    request.TenantId,
                    request.PrimaryDomain,
                    request.SubjectAlternativeNames,
                    request.Issuer,
                    request.CipherSuites
                );

                return Results.Ok(cert);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("IssueCustomDomainCertificate")
        .WithSummary("Issue modern TLS 1.3 X.509 certificate with Subject Alternative Names (SANs)");

        // 3. POST /migrations/start
        group.MapPost("/migrations/start", (
            [FromBody] StartMigrationRequest request,
            IZeroDowntimeTenantMigrationMesh mesh) =>
        {
            try
            {
                var job = mesh.StartMigration(request.TenantId, request.SourceShard, request.TargetShard);
                return Results.Created($"/api/tenancy/governance/migrations/{job.MigrationId}", job);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("StartZeroDowntimeMigration")
        .WithSummary("Initiate zero-downtime tenant shard migration mesh with dual-write CDC replication");

        // 4. POST /migrations/cutover
        group.MapPost("/migrations/cutover", async (
            [FromBody] CutoverApiRequest request,
            IZeroDowntimeTenantMigrationMesh mesh) =>
        {
            var maxPause = request.MaxPauseMilliseconds.HasValue
                ? TimeSpan.FromMilliseconds(request.MaxPauseMilliseconds.Value)
                : TimeSpan.FromMilliseconds(50);

            var result = await mesh.ExecuteCutoverAsync(request.MigrationId, maxPause);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ExecuteMigrationCutover")
        .WithSummary("Execute sub-50ms atomic cutover latch once Merkle tree drift reaches zero");

        // 5. POST /branding/validate
        group.MapPost("/branding/validate", (
            [FromBody] ValidateBrandingRequest request,
            IMultiTenantBrandingGovernanceService brandingService) =>
        {
            try
            {
                var cssResult = brandingService.ScanAndSanitizeCss(request.CustomCss);
                var contrastResult = brandingService.EvaluateContrast(
                    request.ForegroundColor,
                    request.BackgroundColor,
                    request.IsLargeText
                );

                var response = new BrandingValidationResponse(
                    TenantId: request.TenantId,
                    CssClean: cssResult.IsClean,
                    CssThreats: cssResult.DetectedThreats,
                    SanitizedCss: cssResult.SanitizedCss,
                    ContrastEvaluation: contrastResult,
                    EvaluatedAtUtc: DateTime.UtcNow
                );

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ValidateBrandingAndCssGovernance")
        .WithSummary("Audit white-label CSS for security vectors and evaluate WCAG 2.2 AA/AAA palette contrast");

        return app;
    }
}
