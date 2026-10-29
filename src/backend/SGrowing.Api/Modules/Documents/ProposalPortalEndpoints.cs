using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Documents;

public static class ProposalPortalEndpoints
{
    public static IServiceCollection AddProposalPortalServices(this IServiceCollection services)
    {
        services.AddScoped<IProposalPortalService, ProposalPortalService>();
        return services;
    }

    public static IEndpointRouteBuilder MapProposalPortalEndpoints(this IEndpointRouteBuilder app)
    {
        // Public Proposal Viewing & Signing API (No Authorization Required)
        var publicGroup = app.MapGroup("/api/v1/proposals/public")
            .WithTags("Public Proposal & Watermark Portal (Papermark & Documenso)");

        // 1. GET /api/v1/proposals/public/{token}
        publicGroup.MapGet("/{token}", async (
            string token,
            [FromQuery] string? accessKey,
            [FromHeader(Name = "X-Access-Key")] string? headerAccessKey,
            IProposalPortalService service) =>
        {
            var key = !string.IsNullOrWhiteSpace(accessKey) ? accessKey : headerAccessKey;
            try
            {
                var proposal = await service.GetPublicProposalByTokenAsync(token, key);
                return Results.Ok(proposal);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
            catch (UnauthorizedAccessException uae)
            {
                return Results.Json(new { error = uae.Message, isPasswordRequired = true }, statusCode: 401);
            }
            catch (InvalidOperationException ioe)
            {
                return Results.Json(new { error = ioe.Message, isExpired = true }, statusCode: 410); // 410 Gone for expired link
            }
        }).AllowAnonymous();

        // 2. POST /api/v1/proposals/public/{token}/beacon
        // Designed for navigator.sendBeacon and client-side page-turn telemetry
        publicGroup.MapPost("/{token}/beacon", async (
            string token,
            [FromBody] ProposalBeaconDto beacon,
            HttpContext httpContext,
            IProposalPortalService service) =>
        {
            try
            {
                var clientIp = !string.IsNullOrWhiteSpace(beacon.ViewerIp)
                    ? beacon.ViewerIp
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                var userAgent = !string.IsNullOrWhiteSpace(beacon.UserAgent)
                    ? beacon.UserAgent
                    : httpContext.Request.Headers.UserAgent.ToString();

                var enrichedBeacon = beacon with { ViewerIp = clientIp, UserAgent = userAgent };
                var analytics = await service.RecordViewerTelemetryBeaconAsync(token, enrichedBeacon);
                return Results.Ok(analytics);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).AllowAnonymous();

        // 3. POST /api/v1/proposals/public/{token}/accept
        publicGroup.MapPost("/{token}/accept", async (
            string token,
            [FromBody] AcceptProposalDto request,
            HttpContext httpContext,
            IProposalPortalService service) =>
        {
            try
            {
                var clientIp = !string.IsNullOrWhiteSpace(request.ClientIp)
                    ? request.ClientIp
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                var userAgent = !string.IsNullOrWhiteSpace(request.UserAgent)
                    ? request.UserAgent
                    : httpContext.Request.Headers.UserAgent.ToString();

                var enrichedRequest = request with { ClientIp = clientIp, UserAgent = userAgent };
                var result = await service.SignOrAcceptProposalAsync(token, enrichedRequest);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
            catch (InvalidOperationException ioe)
            {
                return Results.BadRequest(new { error = ioe.Message });
            }
            catch (ArgumentException ae)
            {
                return Results.BadRequest(new { error = ae.Message });
            }
        }).AllowAnonymous();

        // Document Owner & Deal Telemetry Analytics API
        var analyticsGroup = app.MapGroup("/api/v1/proposals")
            .WithTags("Proposal Telemetry & Engagement Analytics");

        // 4. GET /api/v1/proposals/{id:guid}/analytics
        analyticsGroup.MapGet("/{id:guid}/analytics", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProposalPortalService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var analytics = await service.GetProposalTelemetryAnalyticsAsync(effectiveTenantId, id);
            return Results.Ok(analytics);
        });

        // 5. POST /api/v1/proposals/share-token (Generate public proposal token)
        analyticsGroup.MapPost("/share-token", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] CreateProposalShareTokenRequest request,
            IProposalPortalService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? (request.TenantId != Guid.Empty ? request.TenantId : Guid.Empty);
            var enriched = request with { TenantId = effectiveTenantId };
            var token = await service.CreateProposalShareTokenAsync(enriched);
            return Results.Ok(new { token, publicUrl = $"/p/{token}" });
        });

        // Alternate route under documents: GET /api/v1/documents/{id:guid}/proposal-analytics
        app.MapGet("/api/v1/documents/{id:guid}/proposal-analytics", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProposalPortalService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var analytics = await service.GetProposalTelemetryAnalyticsAsync(effectiveTenantId, id);
            return Results.Ok(analytics);
        }).WithTags("Documents");

        return app;
    }
}
