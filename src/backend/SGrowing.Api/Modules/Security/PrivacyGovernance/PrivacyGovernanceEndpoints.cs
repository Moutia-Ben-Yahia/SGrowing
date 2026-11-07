using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Security.PrivacyGovernance;

/// <summary>
/// Minimal API endpoints for Saudi PDPL &amp; GCC Regulatory Data Governance &amp; Privacy Rights Engine.
/// Auto-discovered by <see cref="IEndpointModule"/>.
/// </summary>
public class PrivacyGovernanceEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/privacy")
            .WithTags("Saudi PDPL & GCC Regulatory Privacy Governance");

        // 1. POST /api/v1/security/privacy/consents
        group.MapPost("/consents", (
            [FromBody] RecordConsentRequest request,
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context, request.TenantId);
            var sanitizedReq = request with { TenantId = tenantId };
            var record = service.RecordConsent(sanitizedReq);
            return Results.Ok(record);
        })
        .WithName("RecordPdplConsent")
        .WithSummary("Records an explicit consent entry with SHA-256 cryptographic signature under Saudi PDPL Article 5 & 13.");

        // 2. GET /api/v1/security/privacy/consents/{contactId}
        group.MapGet("/consents/{contactId:guid}", (
            [FromRoute] Guid contactId,
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context);
            var consents = service.GetConsents(tenantId, contactId);
            return Results.Ok(consents);
        })
        .WithName("GetContactPdplConsents")
        .WithSummary("Retrieves all historical and active PDPL consent authorizations for a given contact.");

        // Helper GET /api/v1/security/privacy/consents (all consents or filtered by contact)
        group.MapGet("/consents", (
            [FromQuery] Guid? contactId,
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context);
            var consents = contactId.HasValue
                ? service.GetConsents(tenantId, contactId.Value)
                : service.GetAllConsents(tenantId);
            return Results.Ok(consents);
        })
        .WithName("ListPdplConsents")
        .WithSummary("Lists sovereign PDPL consent ledger records for tenant.");

        // 3. POST /api/v1/security/privacy/requests
        group.MapPost("/requests", (
            [FromBody] SubmitPrivacyRequest request,
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context, request.TenantId);
            var sanitizedReq = request with { TenantId = tenantId };
            var result = service.SubmitPrivacyRequest(sanitizedReq);
            return Results.Ok(result);
        })
        .WithName("SubmitPrivacySubjectRequest")
        .WithSummary("Submits a regulatory privacy subject rights inquiry (RTBF Erasure, SAR Export, Rectification).");

        // 4. GET /api/v1/security/privacy/requests
        group.MapGet("/requests", (
            [FromQuery] PrivacyRequestStatus? status,
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context);
            var requests = service.GetPrivacyRequests(tenantId, status);
            return Results.Ok(requests);
        })
        .WithName("GetPrivacySubjectRequests")
        .WithSummary("Retrieves DPO queue of privacy subject rights inquiries for tenant.");

        // 5. POST /api/v1/security/privacy/requests/{requestId}/resolve
        group.MapPost("/requests/{requestId:guid}/resolve", (
            [FromRoute] Guid requestId,
            [FromBody] ResolvePrivacyRequestDto dto,
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context);
            var req = new ResolvePrivacyRequest(tenantId, requestId, dto.Resolution, dto.DpoNotes);
            var resolved = service.ResolvePrivacyRequest(req);
            return Results.Ok(resolved);
        })
        .WithName("ResolvePrivacySubjectRequest")
        .WithSummary("DPO resolution of privacy inquiry, triggering RTBF PII sanitization or SAR export compilation.");

        // 6. GET /api/v1/security/privacy/residency-audit
        group.MapGet("/residency-audit", (
            [FromServices] IPrivacyGovernanceService service,
            HttpContext context) =>
        {
            var tenantId = ResolveTenantId(context);
            var audit = service.GetDataResidencyAudit(tenantId);
            return Results.Ok(audit);
        })
        .WithName("GetDataResidencyAudit")
        .WithSummary("Generates real-time Saudi sovereign data residency and PDPL/NCA regulatory compliance scorecard.");
    }

    private static Guid ResolveTenantId(HttpContext context, Guid? requestedTenantId = null)
    {
        if (requestedTenantId.HasValue && requestedTenantId.Value != Guid.Empty)
        {
            return requestedTenantId.Value;
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal) &&
            Guid.TryParse(headerVal.FirstOrDefault(), out var parsedHeader) && parsedHeader != Guid.Empty)
        {
            return parsedHeader;
        }

        if (context.Request.Query.TryGetValue("tenantId", out var queryVal) &&
            Guid.TryParse(queryVal.FirstOrDefault(), out var parsedQuery) && parsedQuery != Guid.Empty)
        {
            return parsedQuery;
        }

        var tenantClaim = context.User.FindFirst("tenant_id")?.Value ?? context.User.FindFirst("TenantId")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedClaim) && parsedClaim != Guid.Empty)
        {
            return parsedClaim;
        }

        return PrivacyGovernanceService.DefaultTenantId;
    }
}
