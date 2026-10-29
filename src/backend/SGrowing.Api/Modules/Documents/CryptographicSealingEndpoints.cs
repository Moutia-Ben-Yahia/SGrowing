using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Documents;

public static class CryptographicSealingEndpoints
{
    public static IEndpointRouteBuilder MapCryptographicSealingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents")
            .WithTags("Cryptographic PDF Sealing & E-Signatures");

        // 1. Seal document with ISO 32000-1 PKCS#7 & RFC 3161 TSA
        group.MapPost("/{id:guid}/cryptographic-seal", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] SealDocumentRequest request,
            ICryptographicPdfSealingService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var rawBytes = request.PdfBase64 != null ? Convert.FromBase64String(request.PdfBase64) : Array.Empty<byte>();
            var result = await service.SealDocumentWithPkcs7Async(effectiveTenantId, id, rawBytes, request.SignerEmail);
            return Results.Ok(result);
        });

        // 2. Generate Completion Certificate
        group.MapGet("/{id:guid}/completion-certificate", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            ICryptographicPdfSealingService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var result = await service.GenerateCertificateOfCompletionAsync(effectiveTenantId, id);
            return Results.Ok(result);
        });

        // 3. Document Telemetry: Record Page Dwell Time
        group.MapPost("/{id:guid}/telemetry/dwell", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] RecordDwellTelemetryRequest request,
            HttpContext httpContext,
            IDocumentTelemetryService telemetryService) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var viewerIp = !string.IsNullOrWhiteSpace(request.ViewerIp)
                ? request.ViewerIp
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var record = await telemetryService.RecordPageDwellTimeAsync(
                effectiveTenantId,
                id,
                request.PageNumber,
                request.SecondsDwell,
                viewerIp,
                request.ViewerEmail,
                request.MaxScrollPercent ?? 100
            );

            return Results.Ok(record);
        });

        // 4. Document Telemetry: Get Document Telemetry Summary & Drop-Off Heatmap
        group.MapGet("/{id:guid}/telemetry", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            IDocumentTelemetryService telemetryService) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var summary = await telemetryService.GetDocumentTelemetrySummaryAsync(effectiveTenantId, id);
            return Results.Ok(summary);
        });

        // 5. Verify Cryptographic Seal (ISO 32000-1 PKCS#7 & byte-range tamper detection)
        group.MapGet("/{id:guid}/verify-seal", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromQuery] string? token,
            [FromQuery] string? expectedHash,
            ICryptographicPdfSealingService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            var result = await service.VerifySealAsync(effectiveTenantId, id, token, null);
            return Results.Ok(result);
        });

        group.MapPost("/{id:guid}/verify-seal", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] VerifySealRequest? request,
            ICryptographicPdfSealingService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? Guid.Empty;
            byte[]? rawBytes = null;
            if (request?.PdfBase64 != null)
            {
                rawBytes = Convert.FromBase64String(request.PdfBase64);
            }
            var result = await service.VerifySealAsync(effectiveTenantId, id, request?.VerificationToken, rawBytes);
            return Results.Ok(result);
        });

        return app;
    }
}

public record SealDocumentRequest(
    string? PdfBase64,
    string SignerEmail
);

public record VerifySealRequest(
    string? VerificationToken,
    string? PdfBase64
);
