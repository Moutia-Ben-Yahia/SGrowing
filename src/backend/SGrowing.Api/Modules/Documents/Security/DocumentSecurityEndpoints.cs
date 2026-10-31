using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Documents.Security;

public static class DocumentSecurityEndpoints
{
    public static IEndpointRouteBuilder MapDocumentSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents/security")
            .WithTags("DocumentSecurity");

        group.MapPost("/redact", async (
            [FromBody] RedactPiiRequest request,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RedactPiiAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/watermark", async (
            [FromQuery] Guid tenantId,
            [FromQuery] Guid documentId,
            [FromBody] ForensicWatermarkConfig config,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GenerateWatermarkedPayloadAsync(tenantId, documentId, config, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/share-links", async (
            [FromBody] CreateShareLinkRequest request,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            try
            {
                var link = await service.CreateEphemeralShareLinkAsync(request, ct);
                return Results.Created($"/api/v1/documents/security/share-links/{link.LinkId}", link);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/access", async (
            [FromBody] AccessDocumentRequest request,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            try
            {
                var link = await service.AccessDocumentViaLinkAsync(request, ct);
                return Results.Ok(link);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/share-links/{id:guid}/revoke", async (
            Guid id,
            [FromQuery] Guid tenantId,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            var revoked = await service.RevokeShareLinkAsync(tenantId, id, ct);
            return revoked ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        group.MapGet("/share-links", async (
            [FromQuery] Guid tenantId,
            [FromQuery] Guid documentId,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            var links = await service.GetActiveShareLinksAsync(tenantId, documentId, ct);
            return Results.Ok(links);
        });

        group.MapGet("/audit/{documentId:guid}", async (
            Guid documentId,
            [FromQuery] Guid tenantId,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            var audit = await service.GetDocumentAuditLedgerAsync(tenantId, documentId, ct);
            return Results.Ok(audit);
        });

        group.MapGet("/metrics", async (
            [FromQuery] Guid tenantId,
            [FromServices] IDocumentSecurityService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetMetricsAsync(tenantId, ct);
            return Results.Ok(metrics);
        });

        return app;
    }
}
