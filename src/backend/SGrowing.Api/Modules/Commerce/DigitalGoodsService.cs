using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IDigitalGoodsService
{
    Task<DigitalAsset> CreateDigitalAssetAsync(Guid tenantId, string title, string fileName, string storagePath, string mimeType, long sizeBytes, int maxDownloads = 5, int expiryHours = 48);
    Task<List<DigitalAsset>> GetDigitalAssetsAsync(Guid tenantId);
    Task<DigitalDownloadToken> IssueDownloadTokenAsync(Guid tenantId, Guid assetId, Guid? orderId = null, Guid? contactId = null);
    Task<(bool IsValid, string Message, DigitalAsset? Asset)> ValidateAndConsumeDownloadAsync(string token);
    Task<List<ProductLicenseKey>> GenerateLicenseKeysAsync(Guid tenantId, string sku, Guid? orderId, int count = 1, int seatsPerKey = 1);
    Task<List<ProductLicenseKey>> GetLicenseKeysAsync(Guid tenantId, string? sku = null);
    Task<List<PimVariantMatrix>> GenerateVariantMatrixAsync(Guid tenantId, string baseSku, decimal basePrice, Dictionary<string, List<string>> optionAttributes);
    Task<List<PimVariantMatrix>> GetVariantMatrixAsync(Guid tenantId, string baseSku);
    Task<string> GenerateGoogleMerchantFeedAsync(Guid tenantId);
    Task<string> GenerateMetaCatalogFeedAsync(Guid tenantId);
}

public class DigitalGoodsService : IDigitalGoodsService
{
    private readonly SGrowingDbContext _db;

    public DigitalGoodsService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<DigitalAsset> CreateDigitalAssetAsync(Guid tenantId, string title, string fileName, string storagePath, string mimeType, long sizeBytes, int maxDownloads = 5, int expiryHours = 48)
    {
        var asset = DigitalAsset.Create(tenantId, title, fileName, storagePath, mimeType, sizeBytes, maxDownloads, expiryHours);
        _db.DigitalAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    public async Task<List<DigitalAsset>> GetDigitalAssetsAsync(Guid tenantId)
    {
        return await _db.DigitalAssets
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<DigitalDownloadToken> IssueDownloadTokenAsync(Guid tenantId, Guid assetId, Guid? orderId = null, Guid? contactId = null)
    {
        var asset = await _db.DigitalAssets.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == assetId);
        if (asset == null) throw new InvalidOperationException("Digital asset not found.");

        var token = DigitalDownloadToken.Create(tenantId, assetId, orderId, contactId, asset.MaxAllowedDownloads, asset.ExpiryHours);
        _db.DigitalDownloadTokens.Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    public async Task<(bool IsValid, string Message, DigitalAsset? Asset)> ValidateAndConsumeDownloadAsync(string token)
    {
        var record = await _db.DigitalDownloadTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (record == null)
            return (false, "Download link invalid or expired.", null);

        if (record.IsRevoked)
            return (false, "Download link has been revoked.", null);

        if (DateTime.UtcNow > record.ExpiresAtUtc)
            return (false, "Download link has expired.", null);

        if (record.DownloadCount >= record.MaxAllowedDownloads)
            return (false, $"Download limit of {record.MaxAllowedDownloads} attempts exceeded.", null);

        var asset = await _db.DigitalAssets.FirstOrDefaultAsync(a => a.Id == record.AssetId);
        if (asset == null)
            return (false, "Underlying digital file could not be located.", null);

        record.DownloadCount++;
        await _db.SaveChangesAsync();

        return (true, "Download authorized.", asset);
    }

    public async Task<List<ProductLicenseKey>> GenerateLicenseKeysAsync(Guid tenantId, string sku, Guid? orderId, int count = 1, int seatsPerKey = 1)
    {
        var keys = new List<ProductLicenseKey>();
        for (int i = 0; i < count; i++)
        {
            keys.Add(ProductLicenseKey.Create(tenantId, sku, orderId, seatsPerKey));
        }

        _db.ProductLicenseKeys.AddRange(keys);
        await _db.SaveChangesAsync();
        return keys;
    }

    public async Task<List<ProductLicenseKey>> GetLicenseKeysAsync(Guid tenantId, string? sku = null)
    {
        var query = _db.ProductLicenseKeys.Where(k => k.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var cleanSku = sku.Trim().ToUpperInvariant();
            query = query.Where(k => k.ProductSku == cleanSku);
        }

        return await query.OrderByDescending(k => k.CreatedAtUtc).ToListAsync();
    }

    public async Task<List<PimVariantMatrix>> GenerateVariantMatrixAsync(Guid tenantId, string baseSku, decimal basePrice, Dictionary<string, List<string>> optionAttributes)
    {
        baseSku = baseSku.Trim().ToUpperInvariant();

        // Remove existing matrix for this SKU
        var existing = await _db.PimVariantMatrices.Where(m => m.TenantId == tenantId && m.BaseSku == baseSku).ToListAsync();
        if (existing.Count != 0)
        {
            _db.PimVariantMatrices.RemoveRange(existing);
        }

        var combinations = GenerateCartesianProduct(optionAttributes);
        var matrixRecords = new List<PimVariantMatrix>();

        int index = 1;
        foreach (var combo in combinations)
        {
            var suffix = string.Join("-", combo.Values.Select(v => v.Replace(" ", "").ToUpperInvariant()));
            var variantSku = $"{baseSku}-{suffix}";

            matrixRecords.Add(PimVariantMatrix.Create(
                tenantId,
                baseSku,
                variantSku,
                combo,
                basePrice,
                stockQuantity: 100
            ));
            index++;
        }

        _db.PimVariantMatrices.AddRange(matrixRecords);
        await _db.SaveChangesAsync();
        return matrixRecords;
    }

    public async Task<List<PimVariantMatrix>> GetVariantMatrixAsync(Guid tenantId, string baseSku)
    {
        var cleanSku = baseSku.Trim().ToUpperInvariant();
        return await _db.PimVariantMatrices
            .Where(m => m.TenantId == tenantId && m.BaseSku == cleanSku)
            .OrderBy(m => m.VariantSku)
            .ToListAsync();
    }

    public async Task<string> GenerateGoogleMerchantFeedAsync(Guid tenantId)
    {
        var variants = await _db.PimVariantMatrices.Where(m => m.TenantId == tenantId).ToListAsync();

        XNamespace g = "http://base.google.com/ns/1.0";
        var doc = new XDocument(
            new XElement("rss",
                new XAttribute(XNamespace.Xmlns + "g", g.NamespaceName),
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", "SGrowing Store Product Feed"),
                    new XElement("link", "https://sgrowing.io/store"),
                    new XElement("description", "High-performance automated Google Shopping catalog feed"),
                    variants.Select(v => new XElement("item",
                        new XElement(g + "id", v.VariantSku),
                        new XElement(g + "title", $"{v.BaseSku} ({v.VariantSku})"),
                        new XElement(g + "description", $"Product variant attributes: {v.AttributesJson}"),
                        new XElement(g + "link", $"https://sgrowing.io/products/{v.BaseSku.ToLowerInvariant()}?variant={v.VariantSku}"),
                        new XElement(g + "image_link", "https://sgrowing.io/assets/product-placeholder.jpg"),
                        new XElement(g + "availability", v.StockQuantity > 0 ? "in_stock" : "out_of_stock"),
                        new XElement(g + "price", $"{v.Price:F2} USD"),
                        new XElement(g + "condition", "new")
                    ))
                )
            )
        );

        return doc.ToString();
    }

    public async Task<string> GenerateMetaCatalogFeedAsync(Guid tenantId)
    {
        var variants = await _db.PimVariantMatrices.Where(m => m.TenantId == tenantId).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id,title,description,availability,condition,price,link,image_link,brand");

        foreach (var v in variants)
        {
            var title = $"\"{v.BaseSku} - {v.VariantSku}\"";
            var desc = $"\"Product attributes: {v.AttributesJson.Replace("\"", "'")}\"";
            var avail = v.StockQuantity > 0 ? "in stock" : "out of stock";
            var price = $"{v.Price:F2} USD";
            var link = $"https://sgrowing.io/products/{v.BaseSku.ToLowerInvariant()}?sku={v.VariantSku}";
            var img = "https://sgrowing.io/assets/product-placeholder.jpg";
            var brand = "SGrowing Brand";

            sb.AppendLine($"{v.VariantSku},{title},{desc},{avail},new,{price},{link},{img},{brand}");
        }

        return sb.ToString();
    }

    private static List<Dictionary<string, string>> GenerateCartesianProduct(Dictionary<string, List<string>> optionAttributes)
    {
        var result = new List<Dictionary<string, string>> { new() };

        foreach (var (key, values) in optionAttributes)
        {
            var next = new List<Dictionary<string, string>>();
            foreach (var existing in result)
            {
                foreach (var value in values)
                {
                    var updated = new Dictionary<string, string>(existing)
                    {
                        [key] = value
                    };
                    next.Add(updated);
                }
            }
            result = next;
        }

        return result;
    }
}
