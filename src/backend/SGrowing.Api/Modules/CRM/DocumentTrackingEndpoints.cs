using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM;

public record CreateEnvelopeRequest(
    Guid TenantId,
    string Title,
    string DocumentUrl,
    string RawContent,
    bool IsSequential = true
);

public record AddSignerRequest(
    Guid TenantId,
    string Name,
    string Email,
    SignerRole Role = SignerRole.Signer,
    int OrderIndex = 1
);

public record SubmitSignatureRequest(
    Guid TenantId,
    string SignerEmail,
    string SignatureData,
    string? IpAddress = null
);

public record RecordDwellRequest(
    Guid TenantId,
    Guid DocumentId,
    int PageNumber,
    string ViewerEmail,
    int DwellSeconds,
    int MaxScrollPercent = 100
);

public record ConfigureGateRequest(
    Guid TenantId,
    Guid DocumentId,
    bool RequireEmail = true,
    string? Passcode = null,
    bool EnableWatermark = true,
    string WatermarkPattern = "CONFIDENTIAL - {VIEWER_EMAIL} - {IP}"
);

public static class DocumentTrackingEndpoints
{
    public static IEndpointRouteBuilder MapDocumentTrackingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/documents")
            .WithTags("E-Signature & Document Tracking Intelligence (documenso & papermark)");

        // 1. Envelopes
        group.MapPost("/envelopes", async ([FromBody] CreateEnvelopeRequest req, IDocumentTrackingService service) =>
        {
            var envelope = await service.CreateEnvelopeAsync(req.TenantId, req.Title, req.DocumentUrl, req.RawContent, req.IsSequential);
            return Results.Ok(envelope);
        });

        group.MapGet("/envelopes/{id:guid}", async (Guid id, [FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var envelope = await db.DocumentEnvelopes
                .Include(e => e.Signers)
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id);
            return envelope != null ? Results.Ok(envelope) : Results.NotFound();
        });

        group.MapPost("/envelopes/{id:guid}/signers", async (Guid id, [FromBody] AddSignerRequest req, IDocumentTrackingService service) =>
        {
            var signer = await service.AddSignerAsync(req.TenantId, id, req.Name, req.Email, req.Role, req.OrderIndex);
            return Results.Ok(signer);
        });

        group.MapPost("/envelopes/{id:guid}/sign", async (Guid id, [FromBody] SubmitSignatureRequest req, HttpContext ctx, IDocumentTrackingService service) =>
        {
            var clientIp = req.IpAddress ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var envelope = await service.SubmitSignatureAsync(req.TenantId, id, req.SignerEmail, req.SignatureData, clientIp);
            return Results.Ok(envelope);
        });

        group.MapGet("/envelopes/{id:guid}/certificate", async (Guid id, [FromQuery] Guid tenantId, IDocumentTrackingService service) =>
        {
            var cert = await service.GetEnvelopeCertificateAsync(tenantId, id);
            return cert != null ? Results.Ok(cert) : Results.NotFound(new { error = "Certificate not generated or envelope incomplete." });
        });

        // 2. Document Page Telemetry (papermark style)
        group.MapPost("/telemetry/dwell", async ([FromBody] RecordDwellRequest req, HttpContext ctx, IDocumentTrackingService service) =>
        {
            var clientIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var record = await service.RecordPageDwellAsync(req.TenantId, req.DocumentId, req.PageNumber, req.ViewerEmail, clientIp, req.DwellSeconds, req.MaxScrollPercent);
            return Results.Ok(record);
        });

        group.MapGet("/telemetry/{documentId:guid}/report", async (Guid documentId, [FromQuery] Guid tenantId, IDocumentTrackingService service) =>
        {
            var report = await service.GetDocumentDwellReportAsync(tenantId, documentId);
            return Results.Ok(report);
        });

        // 3. Access Gate
        group.MapPost("/gates", async ([FromBody] ConfigureGateRequest req, IDocumentTrackingService service) =>
        {
            var gate = await service.ConfigureAccessGateAsync(req.TenantId, req.DocumentId, req.RequireEmail, req.Passcode, req.EnableWatermark, req.WatermarkPattern);
            return Results.Ok(gate);
        });

        return routes;
    }
}
