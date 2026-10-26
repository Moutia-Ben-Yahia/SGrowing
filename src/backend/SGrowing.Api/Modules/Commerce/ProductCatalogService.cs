using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public class ProductCatalogService
{
    private readonly SGrowingDbContext _db;

    public ProductCatalogService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Product> CreateProductAsync(
        Guid tenantId,
        string name,
        string slug,
        string description,
        ProductType productType,
        decimal basePrice,
        string currency = "USD",
        CancellationToken cancellationToken = default)
    {
        var product = Product.Create(tenantId, name, slug, description, productType, basePrice, currency);
        await _db.Products.AddAsync(product, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<ProductVariant> AddVariantAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        string name,
        decimal price,
        int inventoryStock = 100,
        string attributesJson = "{}",
        CancellationToken cancellationToken = default)
    {
        var variant = ProductVariant.Create(tenantId, productId, sku, name, price, inventoryStock, attributesJson);
        await _db.ProductVariants.AddAsync(variant, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return variant;
    }

    public async Task<List<Product>> GetProductsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductVariant>> GetVariantsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        return await _db.ProductVariants
            .Where(v => v.TenantId == tenantId && v.ProductId == productId && v.IsActive)
            .OrderBy(v => v.Price)
            .ToListAsync(cancellationToken);
    }
}
