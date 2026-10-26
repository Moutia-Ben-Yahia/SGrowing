using System.Text.Json;

namespace SGrowing.Core.Entities;

public class DigitalAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public int MaxAllowedDownloads { get; set; } = 5;
    public int ExpiryHours { get; set; } = 48;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DigitalAsset Create(
        Guid tenantId,
        string title,
        string fileName,
        string storagePath,
        string mimeType,
        long fileSizeBytes,
        int maxAllowedDownloads = 5,
        int expiryHours = 48)
    {
        return new DigitalAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            FileName = fileName.Trim(),
            StoragePath = storagePath.Trim(),
            MimeType = mimeType.Trim(),
            FileSizeBytes = fileSizeBytes,
            MaxAllowedDownloads = maxAllowedDownloads,
            ExpiryHours = expiryHours,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class DigitalDownloadToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public Guid AssetId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ContactId { get; set; }
    public int DownloadCount { get; set; } = 0;
    public int MaxAllowedDownloads { get; set; } = 5;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(48);
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DigitalDownloadToken Create(
        Guid tenantId,
        Guid assetId,
        Guid? orderId = null,
        Guid? contactId = null,
        int maxDownloads = 5,
        int expiryHours = 48)
    {
        return new DigitalDownloadToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8],
            AssetId = assetId,
            OrderId = orderId,
            ContactId = contactId,
            DownloadCount = 0,
            MaxAllowedDownloads = maxDownloads,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(expiryHours),
            IsRevoked = false,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class ProductLicenseKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public int MaxSeats { get; set; } = 1;
    public int ActivatedSeats { get; set; } = 0;
    public string Status { get; set; } = "Active"; // Active, Revoked, Expired
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static ProductLicenseKey Create(
        Guid tenantId,
        string productSku,
        Guid? orderId = null,
        int maxSeats = 1,
        DateTime? expiresAtUtc = null)
    {
        var randomPart = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        var formattedKey = $"SGROW-{randomPart[..4]}-{randomPart[4..8]}-{randomPart[8..12]}-{randomPart[12..16]}";

        return new ProductLicenseKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = formattedKey,
            OrderId = orderId,
            ProductSku = productSku.Trim().ToUpperInvariant(),
            MaxSeats = maxSeats,
            ActivatedSeats = 0,
            Status = "Active",
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class PimVariantMatrix
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string BaseSku { get; set; } = string.Empty;
    public string VariantSku { get; set; } = string.Empty;
    public string AttributesJson { get; set; } = "{}"; // e.g. {"Size": "XL", "Color": "Navy Blue"}
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static PimVariantMatrix Create(
        Guid tenantId,
        string baseSku,
        string variantSku,
        Dictionary<string, string> attributes,
        decimal price,
        int stockQuantity)
    {
        return new PimVariantMatrix
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseSku = baseSku.Trim().ToUpperInvariant(),
            VariantSku = variantSku.Trim().ToUpperInvariant(),
            AttributesJson = JsonSerializer.Serialize(attributes),
            Price = price,
            StockQuantity = stockQuantity,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
