using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Documents;

public static class DocumentTelemetryEndpoints
{
    public static IServiceCollection AddDocumentTelemetryHeatmapServices(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentTelemetryHeatmapService, DocumentTelemetryHeatmapService>();
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents/telemetry")
            .WithTags("Document Telemetry & Heatmap Analytics (Horizon 33)");

        // 1. GET /api/v1/documents/telemetry/documents
        group.MapGet("/documents", async (
            [FromQuery] Guid? tenantId,
            IDocumentTelemetryHeatmapService service) =>
        {
            var docs = await service.GetTrackedDocumentsAsync(tenantId);
            return Results.Ok(docs);
        });

        // 2. POST /api/v1/documents/telemetry/links
        group.MapPost("/links", async (
            [FromBody] CreateShareLinkRequest request,
            IDocumentTelemetryHeatmapService service) =>
        {
            try
            {
                var link = await service.CreateShareLinkAsync(request);
                return Results.Created($"/api/v1/documents/telemetry/links/{link.Token}/meta", link);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // 3. GET /api/v1/documents/telemetry/links/{token}/meta
        group.MapGet("/links/{token}/meta", async (
            string token,
            IDocumentTelemetryHeatmapService service) =>
        {
            try
            {
                var meta = await service.GetShareLinkMetaAsync(token);
                return Results.Ok(meta);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
            catch (InvalidOperationException ioe)
            {
                var isExpired = ioe.Message.Contains("expired", StringComparison.OrdinalIgnoreCase);
                return Results.Json(new { error = ioe.Message, isExpired, isActive = false }, statusCode: 410);
            }
        });

        // 4. POST /api/v1/documents/telemetry/sessions/heartbeat
        group.MapPost("/sessions/heartbeat", async (
            [FromBody] HeartbeatPingRequest request,
            HttpContext httpContext,
            IDocumentTelemetryHeatmapService service) =>
        {
            try
            {
                var ip = !string.IsNullOrWhiteSpace(request.VisitorIp)
                    ? request.VisitorIp
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                var userAgent = !string.IsNullOrWhiteSpace(request.UserAgent)
                    ? request.UserAgent
                    : httpContext.Request.Headers.UserAgent.ToString();

                var enriched = request with { VisitorIp = ip, UserAgent = userAgent };
                var result = await service.RecordHeartbeatAsync(enriched);
                return Results.Ok(result);
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
                var isExpired = ioe.Message.Contains("expired", StringComparison.OrdinalIgnoreCase);
                return Results.Json(new { error = ioe.Message, isExpired }, statusCode: 410);
            }
            catch (ArgumentOutOfRangeException aor)
            {
                return Results.BadRequest(new { error = aor.Message });
            }
        });

        // 5. GET /api/v1/documents/telemetry/documents/{id}/heatmap
        group.MapGet("/documents/{id:guid}/heatmap", async (
            Guid id,
            IDocumentTelemetryHeatmapService service) =>
        {
            try
            {
                var heatmap = await service.GetDocumentHeatmapAsync(id);
                return Results.Ok(heatmap);
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { error = knf.Message });
            }
        });

        // 6. GET /api/v1/documents/telemetry/documents/{id}/sessions
        group.MapGet("/documents/{id:guid}/sessions", async (
            Guid id,
            IDocumentTelemetryHeatmapService service) =>
        {
            var sessions = await service.GetDocumentSessionsAsync(id);
            return Results.Ok(sessions);
        });

        // 7. GET /api/v1/documents/telemetry/alerts
        group.MapGet("/alerts", async (
            [FromQuery] Guid? tenantId,
            IDocumentTelemetryHeatmapService service) =>
        {
            var alerts = await service.GetHotLeadAlertsAsync(tenantId);
            return Results.Ok(alerts);
        });

        // 8. GET /api/v1/documents/telemetry/summary
        group.MapGet("/summary", async (
            [FromQuery] Guid? tenantId,
            IDocumentTelemetryHeatmapService service) =>
        {
            var summary = await service.GetTelemetrySummaryAsync(tenantId);
            return Results.Ok(summary);
        });

        // 9. DELETE /api/v1/documents/telemetry/links/{token}
        group.MapDelete("/links/{token}", async (
            string token,
            IDocumentTelemetryHeatmapService service) =>
        {
            var revoked = await service.RevokeShareLinkAsync(token);
            if (!revoked)
            {
                return Results.NotFound(new { error = $"Share link '{token}' was not found." });
            }
            return Results.Ok(new { success = true, message = $"Share link '{token}' was revoked." });
        });

        return app;
    }
}
