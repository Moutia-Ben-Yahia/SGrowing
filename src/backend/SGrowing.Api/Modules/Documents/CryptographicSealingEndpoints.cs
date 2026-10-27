using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Documents;

public static class CryptographicSealingEndpoints
{
    public static IEndpointRouteBuilder MapCryptographicSealingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents")
            .WithTags("Cryptographic PDF Sealing & E-Signatures");

        group.MapPost("/{id:guid}/cryptographic-seal", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromBody] SealDocumentRequest request,
            ICryptographicPdfSealingService service) =>
        {
            var rawBytes = request.PdfBase64 != null ? Convert.FromBase64String(request.PdfBase64) : Array.Empty<byte>();
            var result = await service.SealDocumentWithPkcs7Async(tenantId, id, rawBytes, request.SignerEmail);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}/completion-certificate", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            ICryptographicPdfSealingService service) =>
        {
            var result = await service.GenerateCertificateOfCompletionAsync(tenantId, id);
            return Results.Ok(result);
        });

        return app;
    }
}

public record SealDocumentRequest(
    string? PdfBase64,
    string SignerEmail
);
