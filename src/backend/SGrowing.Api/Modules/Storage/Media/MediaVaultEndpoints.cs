using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Storage.Media;

public static class MediaVaultEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapMediaVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/media/vault")
            .WithTags("Direct-to-S3 Pre-Signed Object Storage Vault");

        group.MapPost("/upload-url", async (
            [FromBody] CreateUploadUrlRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GeneratePreSignedUploadUrlAsync(effectiveTenantId, req, ct);
            return Results.Ok(result);
        })
        .WithName("GeneratePreSignedUploadUrl")
        .WithSummary("Generate HMAC pre-signed PUT URL for direct client-to-S3 upload");

        group.MapPost("/confirm-upload", async (
            [FromBody] ConfirmUploadRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            bool success = await service.ConfirmUploadAsync(effectiveTenantId, req, ct);
            if (!success) return Results.NotFound(new { error = "Media record not found" });
            return Results.Ok(new { success = true, mediaId = req.MediaId, status = "Ready" });
        })
        .WithName("ConfirmMediaUpload")
        .WithSummary("Confirm successful direct-to-S3 payload upload");

        group.MapGet("/download-url/{id:guid}", async (
            Guid id,
            [FromQuery] int? expiryMinutes,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GeneratePreSignedDownloadUrlAsync(effectiveTenantId, id, expiryMinutes ?? 60, ct);
            if (result == null) return Results.NotFound(new { error = "Media record not found" });
            return Results.Ok(result);
        })
        .WithName("GeneratePreSignedDownloadUrl")
        .WithSummary("Generate HMAC pre-signed secure GET download URL with TTL");

        group.MapGet("/items", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var items = await service.GetMediaItemsAsync(effectiveTenantId, ct);
            return Results.Ok(items);
        })
        .WithName("GetMediaVaultItems")
        .WithSummary("List tenant media records stored in direct S3 vault");

        group.MapPost("/pod-signature-url", async (
            [FromQuery] Guid deliveryId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GeneratePodSignatureUploadUrlAsync(effectiveTenantId, deliveryId, ct);
            return Results.Ok(result);
        })
        .WithName("GeneratePodSignatureUploadUrl")
        .WithSummary("Generate pre-signed S3 URL specifically for courier delivery POD glass signature");

        group.MapPost("/call-recording-url", async (
            [FromQuery] Guid callId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GenerateCallRecordingUploadUrlAsync(effectiveTenantId, callId, ct);
            return Results.Ok(result);
        })
        .WithName("GenerateCallRecordingUploadUrl")
        .WithSummary("Generate pre-signed S3 URL for tele-operator softphone call recording audio");

        group.MapPost("/whatsapp-media-url", async (
            [FromQuery] string instanceName,
            [FromQuery] string fileName,
            [FromQuery] string? contentType,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMediaVaultService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GenerateWhatsAppMediaUploadUrlAsync(effectiveTenantId, instanceName, fileName, contentType ?? "application/octet-stream", ct);
            return Results.Ok(result);
        })
        .WithName("GenerateWhatsAppMediaUploadUrl")
        .WithSummary("Generate pre-signed S3 URL for WhatsApp inbound/outbound audio and photo media");

        return app;
    }
}
