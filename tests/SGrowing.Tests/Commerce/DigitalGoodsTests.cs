using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class DigitalGoodsTests
{
    private readonly SGrowingDbContext _db;
    private readonly DigitalGoodsService _service;

    public DigitalGoodsTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new DigitalGoodsService(_db);
    }

    [Fact]
    public async Task CreateAsset_And_IssueDownloadToken_ShouldValidateAndEnforceLimits()
    {
        var tenantId = Guid.NewGuid();

        // 1. Create asset
        var asset = await _service.CreateDigitalAssetAsync(
            tenantId,
            "Mastering Tunisian COD E-Commerce (eBook)",
            "mastering-tunisian-cod.pdf",
            "assets/ebooks/mastering-cod.pdf",
            "application/pdf",
            sizeBytes: 15_400_000,
            maxDownloads: 2,
            expiryHours: 24
        );

        Assert.NotNull(asset);
        Assert.Equal("mastering-tunisian-cod.pdf", asset.FileName);

        // 2. Issue Token
        var token = await _service.IssueDownloadTokenAsync(tenantId, asset.Id);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrEmpty(token.Token));

        // 3. First consumption attempt -> Success
        var (valid1, msg1, asset1) = await _service.ValidateAndConsumeDownloadAsync(token.Token);
        Assert.True(valid1);
        Assert.NotNull(asset1);
        Assert.Equal(asset.Id, asset1.Id);

        // 4. Second consumption attempt -> Success
        var (valid2, msg2, asset2) = await _service.ValidateAndConsumeDownloadAsync(token.Token);
        Assert.True(valid2);

        // 5. Third consumption attempt -> Should fail (Limit exceeded)
        var (valid3, msg3, asset3) = await _service.ValidateAndConsumeDownloadAsync(token.Token);
        Assert.False(valid3);
        Assert.Contains("exceeded", msg3);
        Assert.Null(asset3);
    }

    [Fact]
    public async Task GenerateLicenseKeys_ShouldCreateFormattedKeysWithSeats()
    {
        var tenantId = Guid.NewGuid();

        var keys = await _service.GenerateLicenseKeysAsync(tenantId, "SOFT-COD-AUTO", null, count: 3, seatsPerKey: 5);

        Assert.Equal(3, keys.Count);
        Assert.All(keys, k =>
        {
            Assert.StartsWith("SGROW-", k.LicenseKey);
            Assert.Equal("SOFT-COD-AUTO", k.ProductSku);
            Assert.Equal(5, k.MaxSeats);
            Assert.Equal("Active", k.Status);
        });

        var retrieved = await _service.GetLicenseKeysAsync(tenantId, "SOFT-COD-AUTO");
        Assert.Equal(3, retrieved.Count);
    }

    [Fact]
    public async Task GenerateVariantMatrix_And_ProductFeeds_ShouldGenerateXmlAndCsv()
    {
        var tenantId = Guid.NewGuid();

        var options = new Dictionary<string, List<string>>
        {
            ["Size"] = new() { "M", "L" },
            ["Color"] = new() { "Black", "White" }
        };

        // 2 sizes * 2 colors = 4 variants
        var matrix = await _service.GenerateVariantMatrixAsync(tenantId, "HOODIE-EXP", 89.99m, options);

        Assert.Equal(4, matrix.Count);
        Assert.Contains(matrix, v => v.VariantSku == "HOODIE-EXP-M-BLACK");
        Assert.Contains(matrix, v => v.VariantSku == "HOODIE-EXP-M-WHITE");
        Assert.Contains(matrix, v => v.VariantSku == "HOODIE-EXP-L-BLACK");
        Assert.Contains(matrix, v => v.VariantSku == "HOODIE-EXP-L-WHITE");

        // Google Merchant XML feed
        var xmlFeed = await _service.GenerateGoogleMerchantFeedAsync(tenantId);
        Assert.Contains("<rss", xmlFeed);
        Assert.Contains("<g:id>HOODIE-EXP-M-BLACK</g:id>", xmlFeed);
        Assert.Contains("89.99 USD", xmlFeed);

        // Meta Catalog CSV feed
        var csvFeed = await _service.GenerateMetaCatalogFeedAsync(tenantId);
        Assert.Contains("id,title,description,availability,condition,price,link,image_link,brand", csvFeed);
        Assert.Contains("HOODIE-EXP-M-BLACK", csvFeed);
        Assert.Contains("in stock", csvFeed);
    }
}
