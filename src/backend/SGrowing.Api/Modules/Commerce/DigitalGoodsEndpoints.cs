using Microsoft.AspNetCore.Mvc;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record CreateDigitalAssetRequest(
    Guid TenantId,
    string Title,
    string FileName,
    string StoragePath,
    string MimeType,
    long FileSizeBytes,
    int MaxAllowedDownloads = 5,
    int ExpiryHours = 48
);

public record IssueTokenRequest(
    Guid TenantId,
    Guid AssetId,
    Guid? OrderId,
    Guid? ContactId
);

public record GenerateLicensesRequest(
    Guid TenantId,
    string ProductSku,
    Guid? OrderId,
    int Count = 1,
    int SeatsPerKey = 1
);

public record GenerateMatrixRequest(
    Guid TenantId,
    string BaseSku,
    decimal BasePrice,
    Dictionary<string, List<string>> Attributes
);

public static class DigitalGoodsEndpoints
{
    public static IEndpointRouteBuilder MapDigitalGoodsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/digital")
            .WithTags("Digital Goods & PIM Matrix");

        // 1. Digital Assets
        group.MapPost("/assets", async ([FromBody] CreateDigitalAssetRequest req, IDigitalGoodsService service) =>
        {
            var asset = await service.CreateDigitalAssetAsync(
                req.TenantId, req.Title, req.FileName, req.StoragePath, req.MimeType, req.FileSizeBytes, req.MaxAllowedDownloads, req.ExpiryHours);
            return Results.Ok(asset);
        });

        group.MapGet("/assets", async ([FromQuery] Guid tenantId, IDigitalGoodsService service) =>
        {
            var assets = await service.GetDigitalAssetsAsync(tenantId);
            return Results.Ok(assets);
        });

        // 2. Download Token Issuance & Consumption
        group.MapPost("/tokens/issue", async ([FromBody] IssueTokenRequest req, IDigitalGoodsService service) =>
        {
            var token = await service.IssueDownloadTokenAsync(req.TenantId, req.AssetId, req.OrderId, req.ContactId);
            return Results.Ok(token);
        });

        group.MapGet("/download/{token}", async (string token, IDigitalGoodsService service) =>
        {
            var (isValid, message, asset) = await service.ValidateAndConsumeDownloadAsync(token);
            if (!isValid || asset == null)
            {
                return Results.BadRequest(new { error = message });
            }

            return Results.Ok(new
            {
                status = "DownloadGranted",
                fileName = asset.FileName,
                mimeType = asset.MimeType,
                fileSizeBytes = asset.FileSizeBytes,
                downloadUrl = $"https://s3.sgrowing.io/{asset.StoragePath}?token={token}"
            });
        });

        // 3. License Keys
        group.MapPost("/licenses", async ([FromBody] GenerateLicensesRequest req, IDigitalGoodsService service) =>
        {
            var keys = await service.GenerateLicenseKeysAsync(req.TenantId, req.ProductSku, req.OrderId, req.Count, req.SeatsPerKey);
            return Results.Ok(keys);
        });

        group.MapGet("/licenses", async ([FromQuery] Guid tenantId, [FromQuery] string? sku, IDigitalGoodsService service) =>
        {
            var keys = await service.GetLicenseKeysAsync(tenantId, sku);
            return Results.Ok(keys);
        });

        // 4. PIM Variant Matrix
        group.MapPost("/variants/matrix-generate", async ([FromBody] GenerateMatrixRequest req, IDigitalGoodsService service) =>
        {
            var matrix = await service.GenerateVariantMatrixAsync(req.TenantId, req.BaseSku, req.BasePrice, req.Attributes);
            return Results.Ok(matrix);
        });

        group.MapGet("/variants/matrix", async ([FromQuery] Guid tenantId, [FromQuery] string baseSku, IDigitalGoodsService service) =>
        {
            var matrix = await service.GetVariantMatrixAsync(tenantId, baseSku);
            return Results.Ok(matrix);
        });

        // 5. Automated Product Feeds
        routes.MapGet("/api/catalog/feeds/google-merchant.xml", async ([FromQuery] Guid tenantId, IDigitalGoodsService service) =>
        {
            var xml = await service.GenerateGoogleMerchantFeedAsync(tenantId);
            return Results.Text(xml, "application/xml", System.Text.Encoding.UTF8);
        }).WithTags("Product Feeds");

        routes.MapGet("/api/catalog/feeds/meta-catalog.csv", async ([FromQuery] Guid tenantId, IDigitalGoodsService service) =>
        {
            var csv = await service.GenerateMetaCatalogFeedAsync(tenantId);
            return Results.Text(csv, "text/csv", System.Text.Encoding.UTF8);
        }).WithTags("Product Feeds");

        return routes;
    }
}
